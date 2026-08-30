
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
        // Texture Content
        ResourceSystem.Instance.RegisterTexture(ModId, "stone", Resources.Load<Texture2D>("Textures/Blocks/stone"));
        ResourceSystem.Instance.RegisterTexture(ModId, "dirt", Resources.Load<Texture2D>("Textures/Blocks/dirt"));
        ResourceSystem.Instance.RegisterTexture(ModId, "grass", Resources.Load<Texture2D>("Textures/Blocks/grass"));
        ResourceSystem.Instance.RegisterTexture(ModId, "grass_side", Resources.Load<Texture2D>("Textures/Blocks/grass_side"));
        ResourceSystem.Instance.RegisterTexture(ModId, "diamond_sword", Resources.Load<Texture2D>("Textures/Items/diamond_sword"));
        ResourceSystem.Instance.RegisterTexture(ModId, "firefly", Resources.Load<Texture2D>("Textures/Entities/firefly"));
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
            ModelId = cube.FullName,
            TextureIds = new(){}
        };
        BlockDefinition stoneDefinition = new()
        {
            modId = ModId,
            name = "stone",
            ModelId = cube.FullName,
            TextureIds = new()
            {
                ["top"]    = $"{ModId}:stone",
                ["bottom"] = $"{ModId}:stone",
                ["front"]  = $"{ModId}:stone",
                ["back"]   = $"{ModId}:stone",
                ["left"]   = $"{ModId}:stone",
                ["right"]  = $"{ModId}:stone"
            },
            // AABBs = new()
            // {
            //     new AABB(0, 0, 0, 1, 1, 1)
            // }
        };

        BlockDefinition dirtDefinition = new()
        {
            modId = ModId,
            name = "dirt",
            ModelId = cube.FullName,
            TextureIds = new()
            {
                ["top"]    = $"{ModId}:dirt",
                ["bottom"] = $"{ModId}:dirt",
                ["front"]  = $"{ModId}:dirt",
                ["back"]   = $"{ModId}:dirt",
                ["left"]   = $"{ModId}:dirt",
                ["right"]  = $"{ModId}:dirt"
            },
            // AABBs = new()
            // {
            //     new AABB(0, 0, 0, 1, 1, 1)
            // }
        };

        BlockDefinition grassDefinition = new()
        {
            modId = ModId,
            name = "grass",
            ModelId = cube.FullName,
            TextureIds = new()
            {
                ["top"]    = $"{ModId}:grass",
                ["bottom"] = $"{ModId}:dirt",
                ["front"]  = $"{ModId}:grass_side",
                ["back"]   = $"{ModId}:grass_side",
                ["left"]   = $"{ModId}:grass_side",
                ["right"]  = $"{ModId}:grass_side"
            },
            // AABBs = new()
            // {
            //     new AABB(0, 0, 0, 1, 1, 1)
            // }
        };

        ResourceSystem.Instance.BlockDefinitions.Register(air);
        ResourceSystem.Instance.RegisterBlock(stoneDefinition);
        ResourceSystem.Instance.RegisterBlock(dirtDefinition);
        ResourceSystem.Instance.RegisterBlock(grassDefinition);

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

        

        // ResourceSystem.Instance.Textures.Register("stone",  Resources.Load<Texture2D>("Textures/Blocks/stone"))
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
        for(int y = topY; y >= minY; y--)
        {
            if(density.GetDensity(x, y, z) <= 0)continue;
            ushort id = y == topY ? grassId : y >= topY - 3 ? dirtId : stoneId;
            chunk.TrySetBlockAt(new Vector3Int(x, y, z), id);
        }
    }

    // 山地填充:全部石头。
    private static void FillMountainsColumn(DensityField density, Vector2Int chunkCoord, int x, int z)
    {
        var chunk = density.Chunk;
        int minY = density.MinY;
        int maxY = minY + density.Height;
        for(int y = maxY - 1; y >= minY; y--)
        {
            if(density.GetDensity(x, y, z) > 0)
                chunk.TrySetBlockAt(new Vector3Int(x, y, z), stoneId);
        }
    }
}