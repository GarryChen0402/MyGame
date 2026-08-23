using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class BlockModelEditorWindow : EditorWindow
{
    class FaceTemplate
    {
        public string name;
        public Vector3[] verts;
        public FaceTemplate(string name, Vector3[] verts)
        {
            this.name = name;
            this.verts = verts;
        }
    }

    // 绕序已验证：cross(v1-v0, v2-v0) = 面朝外的轴向（与 BaseGameModel.cs 的 FullCubeModel 一致）
    static readonly Dictionary<Vector3, FaceTemplate> StandardFaces = new()
    {
        [Vector3.forward] = new FaceTemplate("front",  new[] { new Vector3(1, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(1, 1, 1) }),
        [Vector3.back]    = new FaceTemplate("back",   new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0) }),
        [Vector3.right]   = new FaceTemplate("left",   new[] { new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0) }),
        [Vector3.left]    = new FaceTemplate("right",  new[] { new Vector3(0, 0, 1), new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 1) }),
        [Vector3.up]      = new FaceTemplate("top",    new[] { new Vector3(1, 1, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0), new Vector3(1, 1, 0) }),
        [Vector3.down]    = new FaceTemplate("bottom", new[] { new Vector3(1, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 1) }),
    };

    static readonly int[] QuadTriangles = { 0, 2, 1, 0, 3, 2 };

    BlockModelData data;
    int selectedFaceIndex = -1;
    Vector2 scroll;
    float previewDistance = 2.5f;

    PreviewRenderUtility preview;
    Mesh previewMesh;
    Material previewMaterial;
    Material previewMaterialSource;
    Vector2 previewRotation;

    [MenuItem("Tools/Block Model Editor")]
    static void Open()
    {
        var window = GetWindow<BlockModelEditorWindow>("Block Model Editor");
        window.minSize = new Vector2(720, 480);
    }

    void OnEnable()
    {
        preview = new PreviewRenderUtility();
        previewMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        previewMaterial.hideFlags = HideFlags.HideAndDontSave;
    }

    void OnDisable()
    {
        if (previewMesh != null) DestroyImmediate(previewMesh);
        if (previewMaterial != null) DestroyImmediate(previewMaterial);
        preview?.Cleanup();
    }

    void OnGUI()
    {
        if (data == null)
            data = new BlockModelData { modelName = "full_cube", faces = new List<BlockFaceData>() };
        if (data.faces == null) data.faces = new List<BlockFaceData>();

        DrawToolbar();
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        DrawFaceList();
        DrawFaceEditor();
        EditorGUILayout.EndHorizontal();

        DrawMaterialBar();

        float previewHeight = Mathf.Max(150f, position.height * 0.4f);
        Rect previewRect = GUILayoutUtility.GetRect(position.width, previewHeight);
        DrawPreview(previewRect);
    }

    void DrawMaterialBar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Preview Material", EditorStyles.miniLabel, GUILayout.Width(110));
        previewMaterialSource = (Material)EditorGUILayout.ObjectField(previewMaterialSource, typeof(Material), false, GUILayout.Width(200));
        if (previewMaterialSource == null)
            GUILayout.Label("(default)", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    void DrawToolbar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        data.modelName = EditorGUILayout.TextField(data.modelName, EditorStyles.toolbarTextField, GUILayout.Width(160));
        if (GUILayout.Button("New", EditorStyles.toolbarButton))
            data = new BlockModelData { modelName = "model", faces = new List<BlockFaceData>() };
        if (GUILayout.Button("Load", EditorStyles.toolbarButton)) Load();
        if (GUILayout.Button("Export", EditorStyles.toolbarButton)) Export();
        GUILayout.FlexibleSpace();
        GUILayout.Label("Faces: " + data.faces.Count, EditorStyles.miniLabel);
        GUILayout.EndHorizontal();
    }

    void DrawFaceList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(170));
        GUILayout.Label("Faces", EditorStyles.boldLabel);

        if (data.faces.Count > 0)
        {
            var names = new string[data.faces.Count];
            for (int i = 0; i < data.faces.Count; i++)
                names[i] = data.faces[i].name ?? "face_" + i;
            selectedFaceIndex = GUILayout.SelectionGrid(selectedFaceIndex, names, 1);
        }
        else
        {
            selectedFaceIndex = -1;
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("+ Add Face")) AddFace();
        if (GUILayout.Button("- Remove Selected")) RemoveFace();

        EditorGUILayout.Space();
        GUILayout.Label("Standard Faces", EditorStyles.boldLabel);
        DrawStandardFaceRow("+X", Vector3.right, "-X", Vector3.left);
        DrawStandardFaceRow("+Y", Vector3.up, "-Y", Vector3.down);
        DrawStandardFaceRow("+Z", Vector3.forward, "-Z", Vector3.back);

        EditorGUILayout.EndVertical();
    }

    void DrawStandardFaceRow(string label1, Vector3 axis1, string label2, Vector3 axis2)
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(label1)) AddStandardFace(axis1);
        if (GUILayout.Button(label2)) AddStandardFace(axis2);
        EditorGUILayout.EndHorizontal();
    }

    void DrawFaceEditor()
    {
        EditorGUILayout.BeginVertical();
        if (selectedFaceIndex < 0 || selectedFaceIndex >= data.faces.Count)
        {
            GUILayout.Label("Select a face to edit", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
            return;
        }

        var face = data.faces[selectedFaceIndex];
        if (face.verts == null) face.verts = new List<Vector3>();
        if (face.uv == null) face.uv = new List<Vector2>();
        if (face.colors == null) face.colors = new List<Color>();
        if (face.triangles == null) face.triangles = new List<int>();

        face.name = EditorGUILayout.TextField("Name", face.name);
        face.canBeOccluded = EditorGUILayout.Toggle("Can Be Occluded", face.canBeOccluded);

        scroll = EditorGUILayout.BeginScrollView(scroll);

        GUILayout.Label("Verts", EditorStyles.boldLabel);
        for (int i = 0; i < face.verts.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            face.verts[i] = EditorGUILayout.Vector3Field("V" + i, face.verts[i]);
            if (GUILayout.Button("-", GUILayout.Width(24))) { face.verts.RemoveAt(i); i--; }
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("+ Vert")) face.verts.Add(Vector3.zero);

        EditorGUILayout.Space();
        GUILayout.Label("UV", EditorStyles.boldLabel);
        while (face.uv.Count < face.verts.Count) face.uv.Add(Vector2.zero);
        for (int i = 0; i < face.verts.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            face.uv[i] = EditorGUILayout.Vector2Field("UV" + i, face.uv[i]);
            if (GUILayout.Button("-", GUILayout.Width(24))) { face.uv.RemoveAt(i); i--; }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        GUILayout.Label("Colors", EditorStyles.boldLabel);
        while (face.colors.Count < face.verts.Count) face.colors.Add(Color.white);
        for (int i = 0; i < face.verts.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            face.colors[i] = EditorGUILayout.ColorField("C" + i, face.colors[i]);
            if (GUILayout.Button("-", GUILayout.Width(24))) { face.colors.RemoveAt(i); i--; }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        GUILayout.Label("Triangles", EditorStyles.boldLabel);
        for (int i = 0; i < face.triangles.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            face.triangles[i] = EditorGUILayout.IntField("T" + i, face.triangles[i]);
            if (GUILayout.Button("-", GUILayout.Width(24))) { face.triangles.RemoveAt(i); i--; }
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("+ Triangle")) face.triangles.Add(0);

        EditorGUILayout.Space();
        if (GUILayout.Button("Flip Winding (Reverse Faces)")) FlipWinding(face);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    void DrawPreview(Rect rect)
    {
        if (rect.Contains(Event.current.mousePosition))
        {
            if (Event.current.type == EventType.MouseDrag)
            {
                previewRotation.x = Mathf.Clamp(previewRotation.x - Event.current.delta.y, -89f, 89f);
                previewRotation.y += Event.current.delta.x;
                Event.current.Use();
                Repaint();
            }
            else if (Event.current.type == EventType.ScrollWheel)
            {
                previewDistance = Mathf.Clamp(previewDistance - Event.current.delta.y * 0.15f, 1f, 10f);
                Event.current.Use();
                Repaint();
            }
        }

        RebuildPreviewMesh();

        preview.BeginPreview(rect, GUIStyle.none);
        preview.camera.clearFlags = CameraClearFlags.SolidColor;
        preview.camera.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);

        Vector3 center = previewMesh.bounds.center;
        float radius = Mathf.Max(previewMesh.bounds.size.magnitude, 0.1f);
        preview.camera.transform.position = center + Quaternion.Euler(previewRotation.x, previewRotation.y, 0) * Vector3.forward * (radius * previewDistance);
        preview.camera.transform.LookAt(center);

        preview.DrawMesh(previewMesh, Matrix4x4.identity, previewMaterialSource != null ? previewMaterialSource : previewMaterial, 0);
        // 直接调 camera.Render() 走 URP 渲染管线，preview.Render() 走内置管线会导致 URP shader 显示为品红
        preview.camera.Render();

        GUI.DrawTexture(rect, preview.EndPreview());
    }

    void RebuildPreviewMesh()
    {
        if (previewMesh != null) DestroyImmediate(previewMesh);
        previewMesh = new Mesh();
        previewMesh.hideFlags = HideFlags.HideAndDontSave;

        var verts = new List<Vector3>();
        var uv = new List<Vector2>();
        var colors = new List<Color>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();

        foreach (var face in data.faces)
        {
            if (face.verts == null || face.triangles == null) continue;
            int vStart = verts.Count;
            Vector3 normal = ComputeFaceNormal(face);
            for (int i = 0; i < face.verts.Count; i++)
            {
                verts.Add(face.verts[i]);
                uv.Add(face.uv != null && i < face.uv.Count ? face.uv[i] : Vector2.zero);
                colors.Add(face.colors != null && i < face.colors.Count ? face.colors[i] : Color.white);
                normals.Add(normal);
            }
            foreach (int t in face.triangles)
            {
                if (t >= 0 && t < face.verts.Count) triangles.Add(t + vStart);
            }
        }

        previewMesh.SetVertices(verts);
        previewMesh.SetUVs(0, uv);
        previewMesh.SetColors(colors);
        previewMesh.SetNormals(normals);
        previewMesh.SetTriangles(triangles, 0);
        previewMesh.RecalculateBounds();
    }

    static Vector3 ComputeFaceNormal(BlockFaceData face)
    {
        if (face.triangles == null || face.triangles.Count < 3 || face.verts == null || face.verts.Count == 0)
            return Vector3.zero;
        int i0 = face.triangles[0], i1 = face.triangles[1], i2 = face.triangles[2];
        if (i0 < 0 || i1 < 0 || i2 < 0 || i0 >= face.verts.Count || i1 >= face.verts.Count || i2 >= face.verts.Count)
            return Vector3.zero;
        return Vector3.Cross(face.verts[i1] - face.verts[i0], face.verts[i2] - face.verts[i0]).normalized;
    }

    void AddFace()
    {
        var face = CreateStandardFace(Vector3.forward);
        face.name = GetUniqueFaceName("face");
        data.faces.Add(face);
        selectedFaceIndex = data.faces.Count - 1;
    }

    void AddStandardFace(Vector3 axis)
    {
        var face = CreateStandardFace(axis);
        if (face == null) return;
        face.name = GetUniqueFaceName(face.name);
        data.faces.Add(face);
        selectedFaceIndex = data.faces.Count - 1;
    }

    static BlockFaceData CreateStandardFace(Vector3 axis)
    {
        if (!StandardFaces.TryGetValue(axis, out var template)) return null;
        return new BlockFaceData
        {
            name = template.name,
            verts = new List<Vector3>(template.verts),
            uv = new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) },
            colors = new List<Color> { Color.white, Color.white, Color.white, Color.white },
            triangles = new List<int>(QuadTriangles)
        };
    }

    string GetUniqueFaceName(string baseName)
    {
        string name = baseName;
        int i = 1;
        while (data.faces.Exists(f => f.name == name))
            name = baseName + "_" + i++;
        return name;
    }

    void RemoveFace()
    {
        if (selectedFaceIndex < 0 || selectedFaceIndex >= data.faces.Count) return;
        data.faces.RemoveAt(selectedFaceIndex);
        selectedFaceIndex = data.faces.Count == 0 ? -1 : Mathf.Clamp(selectedFaceIndex, 0, data.faces.Count - 1);
    }

    static void FlipWinding(BlockFaceData face)
    {
        for (int i = 0; i + 2 < face.triangles.Count; i += 3)
        {
            (face.triangles[i + 1], face.triangles[i + 2]) = (face.triangles[i + 2], face.triangles[i + 1]);
        }
    }

    void Load()
    {
        string path = EditorUtility.OpenFilePanel("Load Block Model", "Assets/Resources/Models", "json");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            data = JsonUtility.FromJson<BlockModelData>(File.ReadAllText(path));
            if (data == null)
            {
                Debug.LogError("Failed to parse JSON: " + path);
                return;
            }
            selectedFaceIndex = -1;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to load: " + e.Message);
        }
    }

    void Export()
    {
        string name = string.IsNullOrEmpty(data.modelName) ? "model" : data.modelName;
        string dir = "Assets/Resources/Models";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, name + ".json");
        File.WriteAllText(path, JsonUtility.ToJson(data, true));
        AssetDatabase.Refresh();
        Debug.Log("Exported: " + path);
    }
}
