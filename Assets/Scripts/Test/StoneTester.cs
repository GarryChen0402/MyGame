using System.Collections.Generic;
using UnityEngine;

public class StoneTester : MonoBehaviour
{
    private void Awake()
    {
        transform.position = Vector3.zero;

        // Resource registration and atlas packing are handled by GameBootstrap.
        string stoneId = "minecraft:stone";
        // 解析 stone → 模型 → 贴图
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

        // 每个面对应一个图集区域
        var faceRects = new Dictionary<string, Rect>();
        foreach (var face in model.MeshData)
        {
            faceRects[face.Key] = tex.AtlasUVRect;
        }

        // 生成 mesh
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
