using System.Collections.Generic;
using UnityEngine;

public class StoneTester : MonoBehaviour
{
    private void Awake()
    {
        transform.position = Vector3.zero;

        // 1. 注册 mod 资源（贴图 + 方块定义）
        new Minecraft().RegisterAllResources();
        string stoneId = "minecraft:stone";
        // 2. 模型注册（模型加载流程暂放这里，之后应移到 mod 内）
        // CustomModel cube = BlockModelParser.Parser(Resources.Load<TextAsset>("Models/full_cube").text);
        // cube.modId = Minecraft.ModId;
        // cube.name = "full_block";
        // ResourceSystem.Instance.CustomModels.Register(cube);

        // 3. 打包图集，回填每个贴图的 AtlasUVRect
        ResourceSystem.Instance.BuildAtlas();

        // 4. 解析 stone → 模型 → 贴图
        if (!ResourceSystem.Instance.BlockDefinitions.TryGetResourceWithFullName($"{stoneId}", out var stone))
        {
            Debug.LogError("stone 方块定义未注册");
            return;
        }
        if (!ResourceSystem.Instance.CustomModels.TryGetResourceWithFullName($"{stone.ModelId}", out var model))
        {
            Debug.LogError($"模型 {Minecraft.ModId}:{stone.ModelId} 未注册");
            return;
        }
        if (!ResourceSystem.Instance.Textures.TryGetResourceWithFullName($"{Minecraft.ModId}:stone", out var tex))
        {
            Debug.LogError("stone 贴图未注册");
            return;
        }

        // 5. 每个面对应一个图集区域
        var faceRects = new Dictionary<string, Rect>();
        foreach (var face in model.MeshData)
        {
            faceRects[face.Key] = tex.AtlasUVRect;
        }

        // 6. 生成 mesh
        var verts = new List<Vector3>();
        var uv = new List<Vector2>();
        var colors = new List<Color>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();

        model.ExtendModelMesh(Vector3.zero, verts, uv, colors, normals, triangles, null, faceRects);

        var mesh = new Mesh
        {
            name = "stone_mesh"
        };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uv);
        mesh.SetColors(colors);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;
        GetComponent<MeshRenderer>().sharedMaterial = ResourceSystem.Instance.BlockMaterial;
    }
}
