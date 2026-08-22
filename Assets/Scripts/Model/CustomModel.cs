using System.Collections.Generic;
using UnityEngine;

public struct CustomModel
{
    public string ModelName;
    public Dictionary<string, ModelFaceData> MeshData;

    public void ExtendModelMesh(List<Vector3> verts, List<Vector2> uv, List<Color> colors, List<Vector3> normals, List<int> triangles)
    {
        foreach(var data in MeshData.Values)
        {
            int vStart = verts.Count;
            foreach(var v in data.verts) verts.Add(v);
            foreach(var u in data.uv) uv.Add(u);
            foreach(var c in data.colors) colors.Add(c);
            foreach(var n in data.normals) normals.Add(n);
            foreach(var t in data.triangles) triangles.Add(t + vStart);
        }
    }

}