using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public static class BlockModelParser
{
    public static CustomModel Parser(string json)
    {
        var data = JsonUtility.FromJson<BlockModelData>(json);
        var model = new CustomModel
        {
            ModelName = data.modelName,
            MeshData = new Dictionary<string, ModelFaceData>()
        };
        foreach(var f in data.faces)
            model.MeshData[f.name] = FaceToData(f);
        return model;
    }

    private static ModelFaceData FaceToData(BlockFaceData f)
    {
        var face = new ModelFaceData
        {
            verts = f.verts,
            uv =  f.uv,
            colors = f.colors,
            normals = new List<Vector3>(),
            triangles = f.triangles
        };

        Vector3 normal = Vector3.zero;
        if(f.verts.Count >= 3 && f.triangles.Count >= 3)
        {
            var v0 = f.verts[f.triangles[0]];
            var v1 = f.verts[f.triangles[1]];
            var v2 = f.verts[f.triangles[2]];
            normal = Vector3.Cross(v1 -v0, v2 - v0);
        }
        for(int i = 0; i < f.verts.Count; i++)
        {
            face.normals.Add(normal);
        }
        return face;
    }
}