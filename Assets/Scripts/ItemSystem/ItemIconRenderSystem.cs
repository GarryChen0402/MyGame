using System.Collections.Generic;
using UnityEngine;

public static class ItemIconRenderSystem
{
    private const string IconLayerName = "ItemIcon";

    private static bool initialized;
    private static Camera iconCamera;
    private static Transform modelRoot;
    private static readonly Dictionary<ushort, Mesh> meshCache = new();
    private static Material iconMaterial;

    // Renders the block model of the given item into target. No-op for non-block
    // items or when any registry lookup fails (the UI keeps its previous frame).
    public static void RenderItemIcon(ushort itemId, RenderTexture target)
    {
        if(target == null)return;
        if(!ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(itemId, out var def) || !def.IsBlockItem)return;
        if(!ResourceSystem.Instance.BlockDefinitions.TryGetResourceWithFullName(def.BlockFullName, out var blockDef))return;
        if(!ResourceSystem.Instance.CustomModels.TryGetResourceWithFullName(blockDef.ModelId, out var model))return;
        if(!ResourceSystem.Instance.BlockDefinitions.TryGetNumberId(def.BlockFullName, out var blockId))return;

        EnsureInit();

        if(!meshCache.TryGetValue(blockId, out var mesh))
        {
            mesh = BuildBlockMesh(blockDef, model);
            meshCache[blockId] = mesh;
        }
        if(mesh == null || mesh.vertexCount == 0)return;

        modelRoot.GetComponent<MeshFilter>().sharedMesh = mesh;
        iconCamera.targetTexture = target;
        iconCamera.Render();
    }

    // Six unoccluded faces, baked face shading (mesh colors), centered at origin.
    private static Mesh BuildBlockMesh(BlockDefinition blockDef, CustomModel model)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colors = new List<Color>();
        var normals = new List<Vector3>();
        var tris = new List<int>();
        var faceRects = new Dictionary<string, Rect>();
        foreach(var kv in blockDef.TextureIds)
            if(ResourceSystem.Instance.Textures.TryGetResourceWithFullName(kv.Value, out var tex))
                faceRects[kv.Key] = tex.AtlasUVRect;

        model.ExtendModelMesh(new Vector3(-0.5f, -0.5f, -0.5f), verts, uvs, colors, normals, tris, null, faceRects);

        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void EnsureInit()
    {
        if(initialized)return;
        initialized = true;

        int layer = LayerMask.NameToLayer(IconLayerName);
        if(layer < 0)
        {
            layer = 31;   // fallback: use the last layer when "ItemIcon" isn't configured
            Debug.LogWarning($"[ItemIcon] Layer '{IconLayerName}' not found in Tags & Layers, using layer {layer}.");
        }
        int layerMask = 1 << layer;

        // The icon scene lives in the world scene, so hide it from every camera
        // that renders the world; only the icon camera renders this layer.
        foreach(var cam in Camera.allCameras) cam.cullingMask &= ~layerMask;

        var root = new GameObject("[ItemIconScene]");
        Object.DontDestroyOnLoad(root);   // static class: no MonoBehaviour inheritance, qualify the call
        root.layer = layer;

        var camGo = new GameObject("IconCamera");
        camGo.transform.SetParent(root.transform);
        // Explicit isometric viewpoint from above (top + two sides visible);
        // avoids Euler-angle sign ambiguity between MC and Unity conventions.
        camGo.transform.position = new Vector3(1f, 1f, 1f).normalized * 5f;
        camGo.transform.LookAt(Vector3.zero);   // ensure the block at origin is in view

        iconCamera = camGo.AddComponent<Camera>();
        iconCamera.orthographic = true;
        iconCamera.orthographicSize = 1f;
        iconCamera.clearFlags = CameraClearFlags.SolidColor;
        iconCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);   // transparent: UI shows through
        iconCamera.cullingMask = layerMask;

        iconCamera.allowHDR = false;
        iconCamera.allowMSAA = true;   // paired with RenderTexture.antiAliasing on the icon RT

        var modelGo = new GameObject("ModelRoot");
        modelGo.transform.SetParent(root.transform);
        modelGo.layer = layer;
        modelGo.AddComponent<MeshFilter>();
        modelGo.AddComponent<MeshRenderer>();
        modelRoot = modelGo.transform;

        // Unlit: the icon scene has no lights, shading comes from the baked mesh colors.
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
        iconMaterial = new Material(shader);
        iconMaterial.SetTexture("_BaseMap", ResourceSystem.Instance.BlockAtlas);
        modelGo.GetComponent<MeshRenderer>().sharedMaterial = iconMaterial;
    }
}
