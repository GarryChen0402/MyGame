
using System.Collections.Generic;
using UnityEngine;

public class Minecraft : IMod
{
    public static readonly string ModId = "Minecraft".ToLower();

    string IMod.ModId => ModId;
    public int LoadPriority => 0;

    // Cached block ids used by biome fill columns.
    private static ushort grassId, dirtId, stoneId;

    private static DimensionDefinition testDi = new()
    {
        modId = ModId,
        name = "test_dim",
        MinSubChunkIndex = -4,
        // Biome surfaces reach up to ~106 (mountains 92 + scale 14); the chunk
        // range must cover them or the world renders as a flat cap at y=32.
        MaxSubChunkIndex = 7,
        DimensionGeneratorName = $"{ModId}:biome_dim_generator"
    };

    public void RegisterAllResources()
    {
        // CustomModel Content
        CustomModel cube = BlockModelParser.Parser(Resources.Load<TextAsset>("Models/full_cube").text);
        cube.modId = ModId;
        cube.name = "full_block";
        ResourceSystem.Instance.CustomModels.Register(cube);
        CustomModel stairModel = BlockModelParser.Parser(Resources.Load<TextAsset>("Models/stair").text);
        stairModel.modId = ModId;
        stairModel.name = "stair";
        ResourceSystem.Instance.CustomModels.Register(stairModel);
        var allIds = stairModel.GetAllFaceId();
        // Texture Content
        ResourceSystem.Instance.RegisterTexture(ModId, "stone", Resources.Load<Texture2D>("Textures/Blocks/stone"));
        ResourceSystem.Instance.RegisterTexture(ModId, "dirt", Resources.Load<Texture2D>("Textures/Blocks/dirt"));
        ResourceSystem.Instance.RegisterTexture(ModId, "grass", Resources.Load<Texture2D>("Textures/Blocks/grass"));
        ResourceSystem.Instance.RegisterTexture(ModId, "grass_side", Resources.Load<Texture2D>("Textures/Blocks/grass_side"));
        ResourceSystem.Instance.RegisterTexture(ModId, "diamond_sword", Resources.Load<Texture2D>("Textures/Items/diamond_sword"));
        ResourceSystem.Instance.RegisterTexture(ModId, "firefly", Resources.Load<Texture2D>("Textures/Entities/firefly"));
        // Furnace textures use the "furance" spelling on disk.
        ResourceSystem.Instance.RegisterTexture(ModId, "cobblestone", Resources.Load<Texture2D>("Textures/Blocks/cobblestone"));
        ResourceSystem.Instance.RegisterTexture(ModId, "furance_front_nowork", Resources.Load<Texture2D>("Textures/Blocks/furance_front_nowork"));
        ResourceSystem.Instance.RegisterTexture(ModId, "furance_front_working", Resources.Load<Texture2D>("Textures/Blocks/furance_front_working"));
        ResourceSystem.Instance.RegisterTexture(ModId, "furance_side", Resources.Load<Texture2D>("Textures/Blocks/furance_side"));
        ResourceSystem.Instance.RegisterTexture(ModId, "furance_top_bottom", Resources.Load<Texture2D>("Textures/Blocks/furance_top_bottom"));
        ResourceSystem.Instance.RegisterTexture(ModId, "coal", Resources.Load<Texture2D>("Textures/Items/coal"));
        // EntityModel & EntityAnimation Content
        EntityModel playerModel = EntityModelParser.Parse(Resources.Load<TextAsset>("Models/entity/player").text);
        ResourceSystem.Instance.EntityModels.Register(playerModel);
        ResourceSystem.Instance.EntityAnimations.Register(EntityAnimationParser.Parse(Resources.Load<TextAsset>("Animations/player_walk").text));
        ResourceSystem.Instance.EntityAnimations.Register(EntityAnimationParser.Parse(Resources.Load<TextAsset>("Animations/player_attack").text));
        // BlockDefinition Content
        BlockDefinition air = new ()
        {
            modId = ModId,
            name = "air",
            TextureIds = new(){},
            Variants = new() { new BlockStateVariant { ModelId = cube.FullName } }
        };
        BlockDefinition stoneDefinition = new()
        {
            modId = ModId,
            name = "stone",
            TextureIds = new()
            {
                ["top"]    = $"{ModId}:stone",
                ["bottom"] = $"{ModId}:stone",
                ["front"]  = $"{ModId}:stone",
                ["back"]   = $"{ModId}:stone",
                ["left"]   = $"{ModId}:stone",
                ["right"]  = $"{ModId}:stone"
            },
            Variants = new() { new BlockStateVariant { ModelId = cube.FullName } },
            Hardness = 1.5f
            // AABBs = new()
            // {
            //     new AABB(0, 0, 0, 1, 1, 1)
            // }
        };

        BlockDefinition dirtDefinition = new()
        {
            modId = ModId,
            name = "dirt",
            TextureIds = new()
            {
                ["top"]    = $"{ModId}:dirt",
                ["bottom"] = $"{ModId}:dirt",
                ["front"]  = $"{ModId}:dirt",
                ["back"]   = $"{ModId}:dirt",
                ["left"]   = $"{ModId}:dirt",
                ["right"]  = $"{ModId}:dirt"
            },
            Variants = new() { new BlockStateVariant { ModelId = cube.FullName } },
            Hardness = 1,
            // AABBs = new()
            // {
            //     new AABB(0, 0, 0, 1, 1, 1)
            // }
        };

        BlockDefinition grassDefinition = new()
        {
            modId = ModId,
            name = "grass",
            TextureIds = new()
            {
                ["top"]    = $"{ModId}:grass",
                ["bottom"] = $"{ModId}:dirt",
                ["front"]  = $"{ModId}:grass_side",
                ["back"]   = $"{ModId}:grass_side",
                ["left"]   = $"{ModId}:grass_side",
                ["right"]  = $"{ModId}:grass_side"
            },
            Variants = new() { new BlockStateVariant { ModelId = cube.FullName } },
            // AABBs = new()
            // {
            //     new AABB(0, 0, 0, 1, 1, 1)
            // }
        };

        // Vanilla-style stairs: 4 facings x 4 halves = 16 states. The stair model
        // faces +Z (south) in its base orientation. half=top/upper flip around X
        // (step on top), which also mirrors the facing, so a Y=180 is added to
        // undo the south<->north swap (rotation applies Y before X). Variants
        // with more property entries must come first.
        var stoneStair = new Dictionary<string, string>();
        foreach(var id in allIds)stoneStair[id] = $"{ModId}:stone";
        BlockDefinition stairDefinition = new()
        {
            modId = ModId,
            name = "stone_stair",
            IsFullCube = false,
            TextureIds = stoneStair,
            Properties = new()
            {
                new BlockPropertyDefinition { Name = "facing", Values = new[] { "north", "south", "east", "west" } },
                new BlockPropertyDefinition { Name = "half", Values = new[] { "top", "lower", "upper", "bottom" } }
            },
            Variants = new()
            {
                // half = top / upper: step on top (X=180 flips the bottom shape up)
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "south", ["half"] = "top" }, RotationY = 0   },
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "north", ["half"] = "top" }, RotationY = 180 },
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "east", ["half"] = "top" },  RotationY = 90  },
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "west", ["half"] = "top" },  RotationY = 270 },

                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "south", ["half"] = "upper" }, RotationZ = 180, RotationY = 0   },
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "north", ["half"] = "upper" }, RotationZ = 180, RotationY = 180 },
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "east", ["half"] = "upper" },  RotationZ = 180, RotationY = 90  },
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "west", ["half"] = "upper" },  RotationZ = 180, RotationY = 270 },
                // half = bottom / lower: step on bottom (base shape, no X flip)
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "south", ["half"] = "bottom" }, RotationY = 0   , RotationZ = 180},
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "north", ["half"] = "bottom" }, RotationY = 180 , RotationZ = 180},
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "east", ["half"] = "bottom" },  RotationY = 90  , RotationZ = 180},
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "west", ["half"] = "bottom" },  RotationY = 270 , RotationZ = 180},

                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "south", ["half"] = "lower" }, RotationZ = 0, RotationY = 0   },
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "north", ["half"] = "lower" }, RotationZ = 0, RotationY = 180 },
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "east", ["half"] = "lower" },  RotationZ = 0, RotationY = 90  },
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "west", ["half"] = "lower" },  RotationZ = 0, RotationY = 270 }
            },
            // Base (north-facing, bottom half) shape: full-height back slab plus
            // half-height front step; top variants derive from the X=180 rotation.
            AABBs = new()
            {
                new AABB(new Vector3(0, 0, 0), new Vector3(1, 1, 0.5f)),
                new AABB(new Vector3(0, 0, 0.5f), new Vector3(1, 0.5f, 1))
            }
        };

        BlockDefinition cobblestoneDefinition = new()
        {
            modId = ModId,
            name = "cobblestone",
            TextureIds = new()
            {
                ["top"]    = $"{ModId}:cobblestone",
                ["bottom"] = $"{ModId}:cobblestone",
                ["front"]  = $"{ModId}:cobblestone",
                ["back"]   = $"{ModId}:cobblestone",
                ["left"]   = $"{ModId}:cobblestone",
                ["right"]  = $"{ModId}:cobblestone"
            },
            Variants = new() { new BlockStateVariant { ModelId = cube.FullName } }
        };

        // Furnace: static block rendering + a BlockEntity (input/fuel/output
        // inventories + processing module) declared in the BE definition below.
        BlockDefinition furnaceDefinition = new()
        {
            modId = ModId,
            name = "furnace",
            TextureIds = new()
            {
                ["top"]    = $"{ModId}:furance_top_bottom",
                ["bottom"] = $"{ModId}:furance_top_bottom",
                ["front"]  = $"{ModId}:furance_front_nowork",
                ["back"]   = $"{ModId}:furance_side",
                ["left"]   = $"{ModId}:furance_side",
                ["right"]  = $"{ModId}:furance_side"
            },
            Variants = new() { new BlockStateVariant { ModelId = cube.FullName } },
            HasBlockEntity = true,
            BlockEntityDefinitionFullName = $"{ModId}:furnace"
        };

        ResourceSystem.Instance.BlockDefinitions.Register(air);
        ResourceSystem.Instance.RegisterBlock(stoneDefinition);
        ResourceSystem.Instance.RegisterBlock(dirtDefinition);
        ResourceSystem.Instance.RegisterBlock(grassDefinition);
        ResourceSystem.Instance.RegisterBlock(stairDefinition);
        ResourceSystem.Instance.RegisterBlock(cobblestoneDefinition);
        ResourceSystem.Instance.RegisterBlock(furnaceDefinition);

        ResourceSystem.Instance.BlockDefinitions.TryGetNumberId($"{ModId}:grass", out grassId);
        ResourceSystem.Instance.BlockDefinitions.TryGetNumberId($"{ModId}:dirt", out dirtId);
        ResourceSystem.Instance.BlockDefinitions.TryGetNumberId($"{ModId}:stone", out stoneId);
        // ResourceSystem.Instance.BlockDefinitions.Register(stoneDefinition);
        // ResourceSystem.Instance.BlockDefinitions.Register(dirtDefinition);
        // ResourceSystem.Instance.BlockDefinitions.Register(grassDefinition);

        DimensionGeneratorResource testGenerator = new()
        {
            modId = ModId,
            name = "test_dim_generator",
            GetNewGenerator = ()=> new TestDimensionGenerator()
        };
        ResourceSystem.Instance.DimensionGenerator.Register(testGenerator);
        DimensionGeneratorResource biomeGenerator = new()
        {
            modId = ModId,
            name = "biome_dim_generator",
            GetNewGenerator = () => new BiomeDimensionGenerator()
        };
        ResourceSystem.Instance.DimensionGenerator.Register(biomeGenerator);
        ResourceSystem.Instance.DimensionDefinitions.Register(testDi);

        // Biome Definitions
        ResourceSystem.Instance.BiomeDefinitions.Register(new BiomeDefinition()
        {
            modId = ModId,
            name = "plains",
            DimensionId = testDi.FullName,
            SpawnRange = 96f,
            SpawnProbability = 1f,
            BaseHeight = 67f,
            Scale = 3f,
            MinGradientMagnitude = 0.4f,
            MaxGradientMagnitude = 0.8f,
            TerrainLayerStrength = 0.15f,
            FillColumn = FillPlainsColumn
        });
        ResourceSystem.Instance.BiomeDefinitions.Register(new BiomeDefinition()
        {
            modId = ModId,
            name = "mountains",
            DimensionId = testDi.FullName,
            SpawnRange = 128f,
            SpawnProbability = 1f,
            BaseHeight = 92f,
            Scale = 14f,
            MinGradientMagnitude = 0.8f,
            MaxGradientMagnitude = 1.4f,
            TerrainLayerStrength = 0.35f,
            FillColumn = FillMountainsColumn
        });


        ResourceSystem.Instance.ItemBehaviors.Register(new UniversalBlockItemBehavior()
        {
            modId = "Universal",
            name = "block_item_behavior"
        });

        ResourceSystem.Instance.ItemDefinitions.Register(new ItemDefinition()
        {
            modId = ModId,
            name = "diamond_sword",
            LayerTextures = new string[]
            {
                "minecraft:diamond_sword"
            },
            MaxStack = 1
        });

        // Coal: fuel-tagged item, accepted by the furnace fuel slot filter.
        ResourceSystem.Instance.ItemDefinitions.Register(new ItemDefinition()
        {
            modId = ModId,
            name = "coal",
            LayerTextures = new string[] { $"{ModId}:coal" },
            MaxStack = 64,
            Tags = new() { "fuel" }
        });

        

        // Recipe category + the minimal furnace recipe: 1 cobblestone -> 1 stone.
        ResourceSystem.Instance.RecipeTypes.Register(new RecipeType { modId = ModId, name = "furance" });
        ResourceSystem.Instance.Recipes.Register(new RecipeDefinition
        {
            modId = ModId,
            name = "smelt_cobblestone",
            RecipeTypeFullName = $"{ModId}:furance",
            Inputs = new() { new ItemStackAmount { itemId = $"{ModId}:cobblestone", amount = 1 } },
            Outputs = new() { new ItemStackAmount { itemId = $"{ModId}:stone", amount = 1 } },
            ProcessingTickTime = 20   // short for validation
        });

        // Container types (global once) + the furnace BE definition.
        ResourceSystem.Instance.DataContainerDefinitions.Register(new DataContainerDefinition
        { modId = "universal", name = "inventory", Factory = cfg => new InventoryDataContainer(cfg) });
        ResourceSystem.Instance.WorkContainerDefinitions.Register(new WorkContainerDefinition
        { modId = "universal", name = "processing", Factory = cfg => new ProcessingWorkContainer(cfg) });

        ResourceSystem.Instance.BlockEntityDefinitions.Register(new BlockEntityDefinition
        {
            modId = ModId,
            name = "furnace",
            UIFullName = FurnaceUI.furanceUIDefinition.FullName,
            DataContainers = new()
            {
                new DataContainerConfig
                {
                    Name = "input", TypeFullname = "universal:inventory",
                    Parameters = JsonUtility.ToJson(new InventoryDataContainer.Config { Capacity = 1 })
                },
                new DataContainerConfig
                {
                    Name = "fuel", TypeFullname = "universal:inventory",
                    Parameters = JsonUtility.ToJson(new InventoryDataContainer.Config
                    {
                        Capacity = 1,
                        AllowedTags = new() { "fuel" },
                        ExtractPolicy = ContainerAccess.Any
                    })
                },
                new DataContainerConfig
                {
                    Name = "output", TypeFullname = "universal:inventory",
                    Parameters = JsonUtility.ToJson(new InventoryDataContainer.Config
                    {
                        Capacity = 1,
                        InsertPolicy = ContainerAccess.Module,
                        ExtractPolicy = ContainerAccess.Any
                    })
                }
            },
            WorkContainers = new()
            {
                new WorkContainerConfig
                {
                    TypeFullname = "universal:processing",
                    RecipeType = $"{ModId}:furance",
                    Parameters = JsonUtility.ToJson(new ProcessingWorkContainer.Config
                    {
                        Input = "input",
                        Fuel = "fuel",
                        Output = "output"
                    })
                }
            }
        });


        ResourceSystem.Instance.UIDefinitions.Register(CrosshairUI.CrosshairUIDefinition);
        ResourceSystem.Instance.UIDefinitions.Register(HotBarUI.hotbarDefinition);
        ResourceSystem.Instance.UIDefinitions.Register(HeldItemUI.heldItemUIDefinition);
        ResourceSystem.Instance.UIDefinitions.Register(PlayerInventoryUI.playerInvUIDefinition);
        ResourceSystem.Instance.UIDefinitions.Register(FurnaceUI.furanceUIDefinition);


        ResourceSystem.Instance.InputHandlers.Register(new PlayerInputHandler());
        ResourceSystem.Instance.InputHandlers.Register(new UIInputHandler());
    }

    // 平原填充:地表草方块,下 3 格泥土,再下石头;洞穴(密度 ≤ 0)处留空。
    private static void FillPlainsColumn(DensityField density, Vector2Int chunkCoord, int x, int z)
    {
        var chunk = density.Chunk;
        int minY = density.MinY;
        int maxY = minY + density.Height;
        int topY = -1;
        for(int y = maxY - 1; y >= minY; y--)
        {
            if(density.GetDensity(x, y, z) > 0) { topY = y; break; }
        }
        if(topY < minY)return;
        // Chunk data stores state ids; blocks without properties use their default state.
        ushort grassState = ResourceSystem.Instance.GetDefaultState(grassId);
        ushort dirtState = ResourceSystem.Instance.GetDefaultState(dirtId);
        ushort stoneState = ResourceSystem.Instance.GetDefaultState(stoneId);
        for(int y = topY; y >= minY; y--)
        {
            if(density.GetDensity(x, y, z) <= 0)continue;
            ushort id = y == topY ? grassState : y >= topY - 3 ? dirtState : stoneState;
            chunk.TrySetBlockAt(new Vector3Int(x, y, z), id);
        }
    }

    // 山地填充:全部石头。
    private static void FillMountainsColumn(DensityField density, Vector2Int chunkCoord, int x, int z)
    {
        var chunk = density.Chunk;
        int minY = density.MinY;
        int maxY = minY + density.Height;
        ushort stoneState = ResourceSystem.Instance.GetDefaultState(stoneId);
        for(int y = maxY - 1; y >= minY; y--)
        {
            if(density.GetDensity(x, y, z) > 0)
                chunk.TrySetBlockAt(new Vector3Int(x, y, z), stoneState);
        }
    }
}