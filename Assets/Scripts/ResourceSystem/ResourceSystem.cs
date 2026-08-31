using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceSystem
{
    public static ResourceSystem Instance {get; } = new ResourceSystem();
    // Model Info
    public ResourceRegistryTable<CustomModel> CustomModels {get;} = new();
    // Block Definition
    public ResourceRegistryTable<BlockDefinition> BlockDefinitions {get;} = new();
    // Per-block states (built from BlockDefinition.Properties before freeze).
    public BlockStateRegistry BlockStates {get;} = new();
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

    // Block entity definitions (render mode + module params) & module types (factories)
    public ResourceRegistryTable<BlockEntityDefinition> BlockEntityDefinitions {get;} = new();
    public ResourceRegistryTable<BlockEntityModuleDefinition> BlockEntityModuleDefinitions {get;} = new();

    // Recipes: categories + concrete recipes
    public ResourceRegistryTable<RecipeType> RecipeTypes {get;} = new();
    public ResourceRegistryTable<RecipeDefinition> Recipes {get;} = new();

    // Texture
    public ResourceRegistryTable<TextureResource> Textures {get; } = new();
    private Texture2D blockAtlas;
    public Texture2D BlockAtlas => blockAtlas;

    // BlockMaterial
    public Material BlockMaterial {get;} = new Material(Shader.Find("Universal Render Pipeline/Lit"));

    public bool RegisterTexture(string modId, string name, Texture2D source)
        => Textures.Register(new TextureResource
        {
            modId = modId,
            name = name,
            Atlas = source
        });

    // Lock all resource tables. Called by GameBootstrap after mod registration:
    // run-time registration is a boot bug and would corrupt atlas UVs / lookups.
    public void Freeze()
    {
        CustomModels.Freeze();
        BlockDefinitions.Freeze();
        ItemDefinitions.Freeze();
        ItemBehaviors.Freeze();
        DimensionDefinitions.Freeze();
        DimensionGenerator.Freeze();
        BiomeDefinitions.Freeze();
        EntityModels.Freeze();
        EntityAnimations.Freeze();
        Textures.Freeze();
        BlockEntityDefinitions.Freeze();
        BlockEntityModuleDefinitions.Freeze();
        RecipeTypes.Freeze();
        Recipes.Freeze();
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
}