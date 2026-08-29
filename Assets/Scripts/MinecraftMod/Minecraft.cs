
using UnityEngine;

public class Minecraft : IMod
{
    public static readonly string ModId = "Minecraft".ToLower();

    string IMod.ModId => ModId;
    public int LoadPriority => 0;

    private static DimensionDefinition testDi = new()
    {
        modId = ModId,
        name = "test_dim",
        MinSubChunkIndex = -4,
        MaxSubChunkIndex = 1,
        DimensionGeneratorName = $"{ModId}:test_dim_generator"
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
        ResourceSystem.Instance.DimensionDefinitions.Register(testDi);


        ResourceSystem.Instance.ItemBehaviors.Register(new UniversalBlockItemBehavior()
        {
            modId = "Universal",
            name = "block_item_behavior"
        });
        // ResourceSystem.Instance.Textures.Register("stone",  Resources.Load<Texture2D>("Textures/Blocks/stone"))
    }
}