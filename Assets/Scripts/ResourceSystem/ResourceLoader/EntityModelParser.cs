using System.Collections.Generic;
using UnityEngine;

// Parses EntityModel json (JsonUtility) into an EntityModel resource.
// Json sizes are in pixels (MC convention, 1m = 16px) and get divided by 16.
// Face vertex/triangle data is generated procedurally from Size; the json only
// carries the face UV rect (normalized within its texture) and color.
public static class EntityModelParser
{
    private const float PixelsPerBlock = 16f;

    // Corner points of each face in unit-cube space (matches BaseGameModel winding).
    private static readonly Dictionary<string, Vector3[]> FaceCorners = new()
    {
        ["front"]  = new[] { new Vector3(1, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(1, 1, 1) },
        ["back"]   = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0) },
        ["left"]   = new[] { new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0) },
        ["right"]  = new[] { new Vector3(0, 0, 1), new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 1) },
        ["top"]    = new[] { new Vector3(1, 1, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0), new Vector3(1, 1, 0) },
        ["bottom"] = new[] { new Vector3(1, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 1) }
    };

    private static readonly Dictionary<string, Vector3> FaceNormals = new()
    {
        ["front"] = Vector3.forward,  ["back"] = Vector3.back,
        ["left"] = Vector3.right,     ["right"] = Vector3.left,
        ["top"] = Vector3.up,         ["bottom"] = Vector3.down
    };

    public static EntityModel Parse(string json)
    {
        var data = JsonUtility.FromJson<EntityModelData>(json);
        var model = new EntityModel
        {
            modId = data.modId,
            name = data.name,
            Roots = new List<string>(data.roots ?? new string[0]),
            Cubes = new Dictionary<string, CustomCube>(),
            Hierarchy = new Dictionary<string, List<string>>()
        };
        foreach(var cube in data.cubes ?? new CubeEntry[0])
            model.Cubes[cube.stringId] = CubeToModel(cube);
        foreach(var h in data.hierarchy ?? new HierarchyEntry[0])
            model.Hierarchy[h.parent] = new List<string>(h.children ?? new string[0]);
        return model;
    }

    private static CustomCube CubeToModel(CubeEntry entry)
    {
        Vector3 size = entry.size / PixelsPerBlock;
        var cube = new CustomCube
        {
            StringId = entry.stringId,
            Size = size,
            Pivot = entry.pivot / PixelsPerBlock,
            Position = entry.position / PixelsPerBlock,
            Faces = new Dictionary<string, ModelFaceData>()
        };
        AddFace(cube, "top", entry.top, size);
        AddFace(cube, "bottom", entry.bottom, size);
        AddFace(cube, "front", entry.front, size);
        AddFace(cube, "back", entry.back, size);
        AddFace(cube, "left", entry.left, size);
        AddFace(cube, "right", entry.right, size);
        return cube;
    }

    private static void AddFace(CustomCube cube, string name, FaceEntry entry, Vector3 size)
    {
        if(entry == null)return;
        var corners = FaceCorners[name];
        var verts = new List<Vector3>(4);
        var uv = new List<Vector2>(4);
        var colors = new List<Color>(4);
        // JsonUtility leaves an unspecified Color at (0,0,0,0); treat that as white.
        Color color = entry.color.a == 0f ? Color.white : entry.color;
        for(int i = 0; i < 4; i++)
        {
            Vector3 c = corners[i];
            verts.Add(new Vector3(c.x * size.x, c.y * size.y, c.z * size.z));
            // Corner (x, y) maps into the uvRect: x -> width, y -> height.
            uv.Add(new Vector2(entry.uvRect.x + c.x * entry.uvRect.z,
                               entry.uvRect.y + c.y * entry.uvRect.w));
            colors.Add(color);
        }
        Vector3 normal = FaceNormals[name];
        cube.Faces[name] = new ModelFaceData
        {
            verts = verts,
            uv = uv,
            colors = colors,
            normals = new List<Vector3> { normal, normal, normal, normal },
            triangles = new List<int> { 0, 2, 1, 0, 3, 2 },
            canBeOccluded = entry.canBeOccluded
        };
    }
}

[System.Serializable]
public class EntityModelData
{
    public string modId;
    public string name;
    public string[] roots;
    public CubeEntry[] cubes;
    public HierarchyEntry[] hierarchy;
}

[System.Serializable]
public class CubeEntry
{
    public string stringId;
    public Vector3 size;
    public Vector3 pivot;
    public Vector3 position;
    public FaceEntry top;
    public FaceEntry bottom;
    public FaceEntry front;
    public FaceEntry back;
    public FaceEntry left;
    public FaceEntry right;
}

[System.Serializable]
public class FaceEntry
{
    public Vector4 uvRect;   // u, v, w, h: normalized rect inside the texture
    public Color color;
    public bool canBeOccluded;
}

[System.Serializable]
public class HierarchyEntry
{
    public string parent;
    public string[] children;
}
