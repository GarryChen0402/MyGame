using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Recipe triad (design doc): parsers define recipe types (their full name IS
// the recipeType id), contents are the concrete recipes, checkers gate them.
public partial class ResourceSystem
{
    public static ResourceSystem Instance {get; } = new ResourceSystem();
    // Model Info
    public ResourceRegistryTable<CustomModel> CustomModels {get;} = new();
    // Block Definition
    public ResourceRegistryTable<BlockDefinition> BlockDefinitions {get;} = new();
    // Per-block states, built from BlockDefinition.Properties inside Freeze;
    // the registry number id is the global state id (0 = air).
    public ResourceRegistryTable<BlockState> BlockStates {get;} = new();
    // Dimension Definiton
    public ResourceRegistryTable<DimensionDefinition> DimensionDefinitions {get;} = new();
    public ResourceRegistryTable<DimensionGeneratorResource> DimensionGenerator {get;} = new();
    // Biome Definition
    public ResourceRegistryTable<BiomeDefinition> BiomeDefinitions {get;} = new();
    // Item Definition
    public ResourceRegistryTable<ItemDefinition> ItemDefinitions {get;} = new();
    // Item Behavior Definition
    public ResourceRegistryTable<ItemBehaivor> ItemBehaviors {get;} = new();

    // Entity model & animation
    public ResourceRegistryTable<EntityModel> EntityModels {get;} = new();
    public ResourceRegistryTable<EntityAnimation> EntityAnimations {get;} = new();


    // Recipes: the "three legs" - parsers (recipeType id), contents and checkers
    public ResourceRegistryTable<RecipeParserDefinition> RecipeParsers {get;} = new();
    public ResourceRegistryTable<RecipeContent> Recipes {get;} = new();
    public ResourceRegistryTable<RequirementCheckerDefinition> RequirementCheckers {get;} = new();

    // Block entity system: container types + BE definitions
    public ResourceRegistryTable<DataContainerDefinition> DataContainerDefinitions {get;} = new();
    public ResourceRegistryTable<WorkContainerDefinition> WorkContainerDefinitions {get;} = new();
    public ResourceRegistryTable<BlockEntityDefinition> BlockEntityDefinitions {get;} = new();

    // UI Register
    public ResourceRegistryTable<UIDefinition> UIDefinitions {get;} = new();
    // Input Handler
    public ResourceRegistryTable<IInputHandler> InputHandlers {get;} = new();
    // Input actions (physical key -> action table, MC KeyMapping style). P5
    // splits the single table by action class (world-interaction vs UI); the
    // legacy table stays for the JEI three until P7 migrates them.
    public ResourceRegistryTable<KeyBinding> KeyBindings {get;} = new();
    public ResourceRegistryTable<WorldAction> WorldActions {get;} = new();
    public ResourceRegistryTable<UIAction> UIActions {get;} = new();
    //MobDefinition
    public ResourceRegistryTable<MobDefinition> MobDefinitions {get;} = new();
    // Mob AI behavior specs: assembly logic + per-species behavior numbers
    public ResourceRegistryTable<AIDefinition> AIDefinitions {get;} = new();
    // Buff types: one registry entry per buff type (rule §3.8)
    public ResourceRegistryTable<BuffDefinition> BuffDefinitions {get;} = new();
    // Loot tables (block/mob definitions reference them by FullName)
    public ResourceRegistryTable<LootTableDefinition> LootTables {get;} = new();
    // Commands (T segment): [Command]-scanned classes; string-keyed lookups only
    public ResourceRegistryTable<CommandBase> Commands {get;} = new();

    // Texture
    public ResourceRegistryTable<TextureResource> Textures {get; } = new();
    private Texture2D blockAtlas;
    public Texture2D BlockAtlas => blockAtlas;

    // BlockMaterial: URP/Lit with alpha clipping switched on, so the atlas's
    // cutout textures (leaves holes, the sapling cross, future plants) discard
    // their transparent pixels instead of rendering them black. Clipping stays
    // on the opaque pass - no blending, no sorting - and alpha=1 pixels (every
    // pre-existing texture) are never clipped (design doc 树木系统 §8).
    // Caveat for player builds: _ALPHATEST_ON is a local shader-feature
    // keyword, so the variant has to survive stripping (shader variant
    // collection, or a material asset with the toggle on).
    public Material BlockMaterial {get;} = CreateBlockMaterial();

    private static Material CreateBlockMaterial()
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", 0.5f);
        material.EnableKeyword("_ALPHATEST_ON");
        return material;
    }

    // Mob hurt-flash material: the block atlas under Entity/HurtFlash
    // (Resources/Shaders/EntityHurtFlash.shader), so mob shells can be tinted
    // red through _FlashAmount during the hurt stun. Falls back to Lit until
    // the shader asset finishes importing - a non-flashing mob beats a broken
    // material.
    public Material MobFlashMaterial {get;} = new Material(LoadHurtFlashShader() ?? Shader.Find("Universal Render Pipeline/Lit"));

    private static Shader LoadHurtFlashShader() => Resources.Load<Shader>("Shaders/EntityHurtFlash");

    // blockId -> first global state id / default state id; filled by BuildAllBlockStates.
    private ushort[] offsetByBlockId;
    private ushort[] defaultStateByBlockId;

    public bool RegisterTexture(string modId, string name, Texture2D source)
        => Textures.Register(new TextureResource
        {
            modId = modId,
            name = name,
            Atlas = source
        });

    // Lock all resource tables. Called by GameBootstrap after mod registration:
    // run-time registration is a boot bug and would corrupt atlas UVs / lookups.
    // Block states are built here, from the now-frozen block definitions, and
    // their own table is frozen last so the registration inside isn't rejected.
    public void Freeze()
    {
        if(!BlockDefinitions.ContainsValue("minecraft:air"))
            Debug.LogError("[GameBootstrap] missing required block : minecraft:air");
        CustomModels.Freeze();
        BlockDefinitions.Freeze();
        RecipeParsers.Freeze();          // recipe types (parsers) freeze before item lookups validate against them
        ItemDefinitions.Freeze();        // item references become checkable
        RequirementCheckers.Freeze();
        ItemBehaviors.Freeze();
        DimensionDefinitions.Freeze();
        DimensionGenerator.Freeze();
        BiomeDefinitions.Freeze();
        EntityModels.Freeze();
        EntityAnimations.Freeze();
        Textures.Freeze();
        ValidateRecipeReferences();      // item / RecipeType<->Kind / checker references; aborts startup on errors
        BuildRecipeTypeIndex();          // recipeType -> recipe list buckets (runtime query index)
        Recipes.Freeze();
        DataContainerDefinitions.Freeze();
        WorkContainerDefinitions.Freeze();
        BlockEntityDefinitions.Freeze();
        KeyBindings.Freeze();
        WorldActions.Freeze();
        UIActions.Freeze();
        MobDefinitions.Freeze();      // historically unfrozen; frozen now with AIDefinitions
        AIDefinitions.Freeze();
        BuffDefinitions.Freeze();
        ValidateLootReferences();     // loot refs log errors, do not abort startup
        LootTables.Freeze();
        Commands.Freeze();
    }

    public void PostFreeze()
    {
        BuildAtlas();
        BuildAllBlockStates();
        BlockStates.Freeze();
    }

    public void BuildAtlas()
    {
        var all = new List<Texture2D>();
        foreach(var tex in Textures.Values)all.Add(tex.Atlas);
        Texture2D packer = new(2, 2);

        Rect[] rects = packer.PackTextures(all.ToArray(), 0, 4096);
        int i=0;
        foreach(var tex in Textures.Values)tex.AtlasUVRect = rects[i++];

        packer.filterMode = FilterMode.Point;
        packer.wrapMode = TextureWrapMode.Clamp;

        blockAtlas = packer;
        BlockMaterial.SetTexture("_BaseMap", blockAtlas);
        MobFlashMaterial.SetTexture("_BaseMap", blockAtlas);
        // Object.DontDestroyOnLoad(blockAtlas);
    }


    public bool RegisterBlock(BlockDefinition blockDefinition)
    {
        var registerRes = BlockDefinitions.Register(blockDefinition);
        if(!registerRes)return false;
        ItemDefinition blockItemDef = new()
        {
            modId = blockDefinition.modId,
            name = blockDefinition.name,
            MaxStack = 64,
            BlockFullName = blockDefinition.FullName,
            ItemBehaivorId = "Universal:block_item_behavior",
            IsBlockItem = true
        };
        ItemDefinitions.Register(blockItemDef);
        return true;
    }

    // ---- block state queries (read-only after Freeze) ----

    public BlockState GetState(ushort stateId)
        => BlockStates.TryGetResourceWithNumberId(stateId, out var s) ? s : null;

    public ushort GetDefaultState(ushort blockId)
        => blockId < defaultStateByBlockId.Length ? defaultStateByBlockId[blockId] : (ushort)0;

    // State id of the given property value combination (little-endian encoding).
    public ushort GetStateId(BlockDefinition def, int[] propertyValueIndices)
    {
        int combo = 0, stride = 1;
        for (int i = 0; i < propertyValueIndices.Length; i++)
        {
            combo += propertyValueIndices[i] * stride;
            stride *= def.Properties[i].Values.Length;
        }
        if (!BlockDefinitions.TryGetNumberId(def.FullName, out ushort blockId)) return 0;
        return (ushort)(offsetByBlockId[blockId] + combo);
    }

    // "minecraft:oak_stairs[facing=north,half=bottom]" -> state id. Blocks
    // without properties are plain full names (compatible with save format v1).
    // Unknown block -> false (caller treats it as air); unknown/missing
    // property values fall back to the block default with a warning.
    public bool TryParseStateString(string stateString, out ushort stateId)
    {
        stateId = 0;
        if (BlockStates.TryGetResourceWithFullName(stateString, out var state))
        {
            stateId = state.StateId;
            return true;
        }
        // Legacy v1 saves hold plain block names; unknown property values fall
        // back to the default state. Either way the block must exist.
        int br = stateString.IndexOf('[');
        string blockName = br >= 0 ? stateString[..br] : stateString;
        if (!BlockDefinitions.TryGetNumberId(blockName, out ushort blockId)) return false;
        if (br >= 0)
            Debug.LogWarning($"[ResourceSystem] state string '{stateString}' has unknown or missing properties; defaults used");
        stateId = GetDefaultState(blockId);
        return true;
    }

    // ---- state expansion (once, at the end of Freeze) ----

    // Expands every block definition into its state list. Requires that
    // minecraft:air was registered first (it becomes state id 0).
    private void BuildAllBlockStates()
    {
        int blockCount = BlockDefinitions.Count;
        offsetByBlockId = new ushort[blockCount];
        defaultStateByBlockId = new ushort[blockCount];
        for (ushort blockId = 0; blockId < blockCount; blockId++)
        {
            if (!BlockDefinitions.TryGetResourceWithNumberId(blockId, out var def)) continue;
            offsetByBlockId[blockId] = (ushort)BlockStates.Count;
            BuildStatesFor(def, blockId);
            defaultStateByBlockId[blockId] = offsetByBlockId[blockId];
        }
        if (BlockStates.Count > 0
            && BlockStates.TryGetResourceWithNumberId(0, out var s0) && s0.BlockId != 0)
            Debug.LogError("[ResourceSystem] state id 0 must be minecraft:air (register it first)");
    }

    private void BuildStatesFor(BlockDefinition def, ushort blockId)
    {
        if (def.Variants == null || def.Variants.Count == 0)
        {
            Debug.LogError($"[ResourceSystem] {def.FullName} has no variants (every block needs at least one; put the model there)");
            return;
        }
        if (def.Properties == null || def.Properties.Count == 0)
        {
            CreateState(def, blockId, new int[0], def.Variants[0]);
            return;
        }
        int total = 1;
        foreach (var p in def.Properties)
        {
            if (p.Values == null || p.Values.Length == 0)
            {
                Debug.LogError($"[ResourceSystem] property '{p.Name}' of {def.FullName} has no values");
                return;
            }
            total *= p.Values.Length;
        }
        if ((long)BlockStates.Count + total > ushort.MaxValue)
        {
            Debug.LogError($"[ResourceSystem] {def.FullName}: state count exceeds ushort range, block skipped");
            return;
        }
        for (int combo = 0; combo < total; combo++)
        {
            int[] indices = DecodeCombo(def, combo);
            BlockStateVariant v = MatchVariant(def, indices);
            if (v == null)
            {
                Debug.LogError($"[ResourceSystem] {def.FullName} state combo {string.Join(",", indices)} matches no variant; using the first one");
                v = def.Variants[0];
            }
            CreateState(def, blockId, indices, v);
        }
    }

    // Little-endian combo encoding: property 0 is the least significant digit.
    private static int[] DecodeCombo(BlockDefinition def, int combo)
    {
        var indices = new int[def.Properties.Count];
        for (int i = 0; i < def.Properties.Count; i++)
        {
            indices[i] = combo % def.Properties[i].Values.Length;
            combo /= def.Properties[i].Values.Length;
        }
        return indices;
    }

    private static BlockStateVariant MatchVariant(BlockDefinition def, int[] indices)
    {
        if (def.Variants == null) return null;
        foreach (var v in def.Variants)
        {
            bool match = true;
            foreach (var kv in v.Properties)
            {
                int pIdx = -1;
                for (int i = 0; i < def.Properties.Count; i++)
                    if (def.Properties[i].Name == kv.Key) { pIdx = i; break; }
                if (pIdx < 0 || def.Properties[pIdx].IndexOfValue(kv.Value) != indices[pIdx]) { match = false; break; }
            }
            if (match) return v;
        }
        return null;
    }

    private void CreateState(BlockDefinition def, ushort blockId, int[] indices, BlockStateVariant variant)
    {
        string stateString = BuildStateString(def, indices);
        var state = new BlockState
        {
            modId = def.modId,
            name = stateString[(def.modId.Length + 1)..],
            StateId = (ushort)BlockStates.Count,
            BlockId = blockId,
            Block = def,
            LocalStateId = BlockStates.Count - offsetByBlockId[blockId],
            PropertyValueIndices = indices,
            ModelId = variant.ModelId,
            RotationX = variant?.RotationX ?? 0,
            RotationY = variant?.RotationY ?? 0,
            RotationZ = variant?.RotationZ ?? 0,
            AABBs = variant?.AABBs ?? def.AABBs
        };
        state.AABBs = RotateAABBs(state.AABBs, state.RotationX, state.RotationY, state.RotationZ);
        if (!BlockStates.Register(state))
            Debug.LogError($"[ResourceSystem] duplicate block state '{state.FullName}'");
    }

    private static string BuildStateString(BlockDefinition def, int[] indices)
    {
        if (def.Properties == null || def.Properties.Count == 0) return def.FullName;
        var sb = new StringBuilder(def.FullName);
        sb.Append('[');
        for (int i = 0; i < def.Properties.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(def.Properties[i].Name).Append('=').Append(def.Properties[i].Values[indices[i]]);
        }
        sb.Append(']');
        return sb.ToString();
    }

    // Rotates the boxes around the block center; boxes stay axis-aligned because
    // the rotations are 90-degree multiples. Returns null when boxes are null.
    private static List<AABB> RotateAABBs(List<AABB> boxes, int rotX, int rotY, int rotZ)
    {
        if (boxes == null || boxes.Count == 0 || (rotX == 0 && rotY == 0 && rotZ == 0)) return boxes;
        Quaternion rot = Quaternion.Euler(rotX, rotY, rotZ);   // same quaternion as the model rotation
        Vector3 center = new(0.5f, 0.5f, 0.5f);
        var result = new List<AABB>(boxes.Count);
        foreach (var b in boxes)
        {
            Vector3 min = rot * (b.MinRange - center) + center;
            Vector3 max = rot * (b.MaxRange - center) + center;
            result.Add(new AABB(
                new Vector3(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Min(min.z, max.z)),
                new Vector3(Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y), Mathf.Max(min.z, max.z))));
        }
        return result;
    }
}
