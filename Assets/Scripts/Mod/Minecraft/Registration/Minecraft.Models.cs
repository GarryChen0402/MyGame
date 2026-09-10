using UnityEngine;

public partial class Minecraft
{
    // ---- A: custom models ----
    private void BuildCustomModels()
    {
        // CustomModel Content
        cube = BlockModelParser.Parser(Resources.Load<TextAsset>("Models/full_cube").text);
        cube.modId = ModId;
        cube.name = "full_block";
        ResourceSystem.Instance.CustomModels.Register(cube);
        stairModel = BlockModelParser.Parser(Resources.Load<TextAsset>("Models/stair").text);
        stairModel.modId = ModId;
        stairModel.name = "stair";
        ResourceSystem.Instance.CustomModels.Register(stairModel);
        allIds = stairModel.GetAllFaceId();
        crossModel = BlockModelParser.Parser(Resources.Load<TextAsset>("Models/cross").text);
        crossModel.modId = ModId;
        crossModel.name = "cross";
        ResourceSystem.Instance.CustomModels.Register(crossModel);
    }
}
