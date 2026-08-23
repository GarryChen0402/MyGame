using UnityEngine;
using System.Collections.Generic;

public class ModelTester : MonoBehaviour
{
    public TextAsset modelTarget;
    private void Awake()
    {
        Mesh mesh = new();
        List<Vector3> verts =new();
        List<Vector2> uv = new();
        List<Vector3> normals = new();
        List<Color> colors = new();
        List<int> triangles = new();

        // TextAsset json = Resources.Load<TextAsset>("Models/full_cube");
        CustomModel basemodel = BlockModelParser.Parser(modelTarget.text);
        // BaseGameModel.FullCubeModel.ExtendModelMesh(verts, uv, colors, normals, triangles);
        basemodel.ExtendModelMesh(Vector3.zero, verts, uv, colors, normals, triangles);
        basemodel.ExtendModelMesh(Vector3.up, verts, uv, colors, normals, triangles);
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uv);
        mesh.SetColors(colors);
        mesh.SetNormals(normals.ToArray());
        mesh.SetTriangles(triangles, 0);
        // mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        MeshFilter filter = GetComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        // MeshRenderer renderer = GetComponent<MeshRenderer>();
        // renderer.material = new Material(Shader.Find(""))

    }
}
