using UnityEngine;

public static class BaseGameModel
{
    public static string ModId = "minecraft";

    public static readonly CustomModel FullCubeModel = new()
    {
        modId = "base_game",
        name = "full_cube",
        MeshData = new()
        {
            ["front"] = new ModelFaceData() //Face Toward to +Z-axis 
            {
                verts = new() { new Vector3(1, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(1, 1, 1) },
                uv = new() { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) },
                colors = new() { Color.white, Color.white, Color.white, Color.white },
                normals = new() { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward},
                triangles = new() { 0, 2, 1, 0, 3, 2 }
            },
            ["back"] = new ModelFaceData()//Face Toward to -Z-axis 
            {
                verts = new() { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0) },
                uv = new() { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) },
                colors = new() { Color.white, Color.white, Color.white, Color.white },
                normals = new() { Vector3.back, Vector3.back, Vector3.back, Vector3.back},
                triangles = new() { 0, 2, 1, 0, 3, 2 }
            },
            ["left"] = new ModelFaceData()//Face Toward to +X-axis 
            {
                verts = new() { new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0) },
                uv = new() { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) },
                colors = new() { Color.white, Color.white, Color.white, Color.white },
                normals = new() { Vector3.right, Vector3.right, Vector3.right, Vector3.right},
                triangles = new() { 0, 2, 1, 0, 3, 2 }
            },
            ["right"] = new ModelFaceData()//Face Toward to -X-axis 
            {
                verts = new() { new Vector3(0, 0, 1), new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 1) },
                uv = new() { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) },
                colors = new() { Color.white, Color.white, Color.white, Color.white },
                normals = new() { Vector3.left, Vector3.left, Vector3.left, Vector3.left},
                triangles = new() { 0, 2, 1, 0, 3, 2 }
            },
            ["top"] = new ModelFaceData()//Face Toward to +Y-axis 
            {
                verts = new() { new Vector3(1, 1, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0), new Vector3(1, 1, 0) },
                uv = new() { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) },
                colors = new() { Color.white, Color.white, Color.white, Color.white },
                normals = new() { Vector3.up, Vector3.up, Vector3.up, Vector3.up},
                triangles = new() { 0, 2, 1, 0, 3, 2 }
            },
            ["bottom"] = new ModelFaceData()//Face Toward to -Y-axis
            {
                verts = new() { new Vector3(1, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 1) },
                uv = new() { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) },
                colors = new() { Color.white, Color.white, Color.white, Color.white },
                normals = new() { Vector3.down, Vector3.down, Vector3.down, Vector3.down},
                triangles = new() { 0, 2, 1, 0, 3, 2 }
            },
        }
    };
}