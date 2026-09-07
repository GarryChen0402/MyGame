using System.Collections.Generic;
using UnityEngine;

public struct ModelFaceData // 自定义模型中， 单个面的信息， 包含这个面的相关 顶点数据， uv采样点（相对于单张Texture2D）
{
    public List<Vector3> verts;
    public List<Vector2> uv;
    public List<Color> colors;

    public List<Vector3> normals;
    public List<int> triangles;

    // Whether this face can be hidden by a neighbor block.
    // False for faces not flush with the cell boundary (e.g. stairs' step risers, torches).
    public bool canBeOccluded;

}