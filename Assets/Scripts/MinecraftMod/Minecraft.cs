
using UnityEngine;

public class Minecraft : IMod
{
    public static readonly string ModId = "Minecraft".ToLower();
    public void RegisterAllResources()
    {

        CustomModel cube = BlockModelParser.Parser(Resources.Load<TextAsset>("Models/full_cube").text);
        cube.modId = ModId;
        cube.name = "full_block";
        ResourceSystem.Instance.CustomModels.Register(cube);
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
            }
        };

        ResourceSystem.Instance.RegisterTexture(ModId, "stone", Resources.Load<Texture2D>("Textures/Blocks/stone"));
        ResourceSystem.Instance.BlockDefinitions.Register(air);
        ResourceSystem.Instance.BlockDefinitions.Register(stoneDefinition);
        // ResourceSystem.Instance.Textures.Register("stone",  Resources.Load<Texture2D>("Textures/Blocks/stone"))
    }
}