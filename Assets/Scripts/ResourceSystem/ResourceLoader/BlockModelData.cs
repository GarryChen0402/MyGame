using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BlockModelData
{
    public string modelName;
    public List<BlockFaceData> faces;
}

[Serializable]
public class BlockFaceData
{
    public string name;
    public List<Vector3> verts;
    public List<Vector2> uv;
    public List<Color> colors;
    public List<int> triangles;
}