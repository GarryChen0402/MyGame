using System.Collections.Generic;
using UnityEngine;

public static class ItemIconRenderSystem
{
    private const string IconLayerName = "ItemIcon";

    private static bool initialized;
    private static Camera iconCamera;
    private static Transform modelRoot;
    private static readonly Dictionary<ushort, Mesh> blockMeshCache = new();   // keyed by itemId
    private static readonly Dictionary<ushort, Mesh> itemMeshCache = new();    // keyed by itemId
    private static Material iconMaterial;

    // Renders the model of the given item into target: block items render the
    // block model, other items a silhouette extruded from their texture. No-op
    // when any registry lookup fails (the UI keeps its previous frame).
    public static void RenderItemIcon(ushort itemId, RenderTexture target)
    {
        if(target == null)return;
        if(!ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(itemId, out var def))return;

        EnsureInit();

        Mesh mesh = def.IsBlockItem
            ? GetOrCreateBlockMesh(itemId, def)
            : GetOrCreateItemMesh(itemId, def);
        if(mesh == null || mesh.vertexCount == 0)return;

        // Blocks get an isometric three-face view; items are shown head-on
        // (the item model's front face points +z, so the camera sits on +z).
        iconCamera.transform.position = def.IsBlockItem
            ? new Vector3(1f, 1f, 1f).normalized * 5f
            : new Vector3(0f, 0f, 5f);
        iconCamera.transform.LookAt(Vector3.zero);

        // The head-on camera's right axis points -x (Unity LookAt convention),
        // which flips the model horizontally. Rotating the item 180° around y
        // cancels that flip without touching the UVs; a proper rotation keeps
        // the front face's winding intact, so it still faces the camera.
        modelRoot.localRotation = def.IsBlockItem
            ? Quaternion.identity
            : Quaternion.Euler(0f, 180f, 0f);

        modelRoot.GetComponent<MeshFilter>().sharedMesh = mesh;
        iconCamera.targetTexture = target;
        iconCamera.Render();
    }

    private static Mesh GetOrCreateBlockMesh(ushort itemId, ItemDefinition def)
    {
        if(blockMeshCache.TryGetValue(itemId, out var mesh))return mesh;
        if(!ResourceSystem.Instance.BlockDefinitions.TryGetNumberId(def.BlockFullName, out ushort blockId))return null;
        // Icons show the block's default state (vanilla behavior).
        BlockState state = ResourceSystem.Instance.GetState(ResourceSystem.Instance.GetDefaultState(blockId));
        if(state == null)return null;
        if(!ResourceSystem.Instance.CustomModels.TryGetResourceWithFullName(state.ModelId, out var model))return null;

        mesh = BuildBlockMesh(state, model);
        blockMeshCache[itemId] = mesh;
        return mesh;
    }

    private static Mesh GetOrCreateItemMesh(ushort itemId, ItemDefinition def)
    {
        if(itemMeshCache.TryGetValue(itemId, out var mesh))return mesh;
        mesh = BuildItemMesh(def);
        if(mesh != null)itemMeshCache[itemId] = mesh;
        return mesh;
    }

    // Six unoccluded faces, baked face shading (mesh colors), centered at origin.
    private static Mesh BuildBlockMesh(BlockState state, CustomModel model)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colors = new List<Color>();
        var normals = new List<Vector3>();
        var tris = new List<int>();
        var faceRects = new Dictionary<string, Rect>();
        foreach(var kv in state.Block.TextureIds)
            if(ResourceSystem.Instance.Textures.TryGetResourceWithFullName(kv.Value, out var tex))
                faceRects[kv.Key] = tex.AtlasUVRect;

        // Icons always show the block facing the camera (+z, south): drop the
        // default state's facing rotation (shape flips on X/Z stay) so stairs
        // etc. show their front face instead of their back.
        model.ExtendModelMesh(new Vector3(-0.5f, -0.5f, -0.5f), verts, uvs, colors, normals, tris, null, faceRects,
            state.RotationX, 0, state.RotationZ);

        // MC-style baked face shading: the icon scene has no lights, so shade
        // each vertex by its rotated face direction (top brightest, sides mid,
        // bottom darkest) to make the block's faces read clearly.
        for(int i = 0; i < normals.Count; i++)
        {
            float shade = Mathf.Abs(normals[i].y) > 0.9f
                ? (normals[i].y > 0f ? 1f : 0.55f)
                : 0.78f;
            Color c = colors[i];
            c.r *= shade; c.g *= shade; c.b *= shade;
            colors[i] = c;
        }

        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // Vanilla ItemModelGenerator logic: every non-transparent pixel of the
    // texture becomes a 1-texel-thick slab (front/back faces, plus side faces
    // where a neighbor pixel is transparent), forming the item's silhouette.
    private static Mesh BuildItemMesh(ItemDefinition def)
    {
        if(def.LayerTextures == null || def.LayerTextures.Length == 0)return null;
        if(!ResourceSystem.Instance.Textures.TryGetResourceWithFullName(def.LayerTextures[0], out var tex))return null;
        Texture2D source = tex.Atlas;
        if(!source.isReadable)
        {
            Debug.LogWarning($"[ItemIcon] Texture '{def.LayerTextures[0]}' is not readable; enable Read/Write in its import settings to build the item model.");
            return null;
        }
        Rect rect = tex.AtlasUVRect;
        int w = source.width, h = source.height;
        Color32[] pixels = source.GetPixels32();   // rows bottom-up: y=0 is the bottom row

        bool IsSolid(int x, int y)
            => x >= 0 && x < w && y >= 0 && y < h && pixels[y * w + x].a > 0;

        const float halfDepth = 0.03125f;   // 1 texel = 1/16 block, half depth per face
        float stepX = 1f / w, stepY = 1f / h;

        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colors = new List<Color>();
        var normals = new List<Vector3>();
        var tris = new List<int>();

        void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                     Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD, Vector3 normal)
        {
            int vStart = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            uvs.Add(uvA); uvs.Add(uvB); uvs.Add(uvC); uvs.Add(uvD);
            for(int i = 0; i < 4; i++)colors.Add(Color.white);
            for(int i = 0; i < 4; i++)normals.Add(normal);
            tris.Add(vStart); tris.Add(vStart + 1); tris.Add(vStart + 2);
            tris.Add(vStart); tris.Add(vStart + 2); tris.Add(vStart + 3);
        }

        for(int y = 0; y < h; y++)
        {
            for(int x = 0; x < w; x++)
            {
                if(!IsSolid(x, y))continue;
                float x0 = -0.5f + x * stepX, x1 = x0 + stepX;
                float y0 = -0.5f + y * stepY, y1 = y0 + stepY;
                float u0 = rect.x + rect.width * (x / (float)w);
                float u1 = rect.x + rect.width * ((x + 1) / (float)w);
                float v0 = rect.y + rect.height * (y / (float)h);
                float v1 = rect.y + rect.height * ((y + 1) / (float)h);
                Vector2 uv00 = new(u0, v0), uv10 = new(u1, v0), uv11 = new(u1, v1), uv01 = new(u0, v1);

                // front (normal +z) and back (normal -z)
                AddQuad(new Vector3(x0, y0, halfDepth), new Vector3(x1, y0, halfDepth),
                        new Vector3(x1, y1, halfDepth), new Vector3(x0, y1, halfDepth),
                        uv00, uv10, uv11, uv01, Vector3.forward);
                AddQuad(new Vector3(x0, y0, -halfDepth), new Vector3(x0, y1, -halfDepth),
                        new Vector3(x1, y1, -halfDepth), new Vector3(x1, y0, -halfDepth),
                        uv00, uv01, uv11, uv10, Vector3.back);

                if(!IsSolid(x - 1, y))   // left (normal -x)
                    AddQuad(new Vector3(x0, y0, -halfDepth), new Vector3(x0, y0, halfDepth),
                            new Vector3(x0, y1, halfDepth), new Vector3(x0, y1, -halfDepth),
                            uv00, uv01, uv01, uv00, Vector3.left);
                if(!IsSolid(x + 1, y))   // right (normal +x)
                    AddQuad(new Vector3(x1, y0, halfDepth), new Vector3(x1, y0, -halfDepth),
                            new Vector3(x1, y1, -halfDepth), new Vector3(x1, y1, halfDepth),
                            uv10, uv11, uv11, uv10, Vector3.right);
                if(!IsSolid(x, y + 1))   // top (normal +y)
                    AddQuad(new Vector3(x0, y1, -halfDepth), new Vector3(x0, y1, halfDepth),
                            new Vector3(x1, y1, halfDepth), new Vector3(x1, y1, -halfDepth),
                            uv01, uv01, uv11, uv11, Vector3.up);
                if(!IsSolid(x, y - 1))   // bottom (normal -y)
                    AddQuad(new Vector3(x0, y0, halfDepth), new Vector3(x0, y0, -halfDepth),
                            new Vector3(x1, y0, -halfDepth), new Vector3(x1, y0, halfDepth),
                            uv00, uv00, uv10, uv10, Vector3.down);
            }
        }

        if(verts.Count == 0)return null;
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
        iconCamera.allowMSAA = false;   // paired with RenderTexture.antiAliasing on the icon RT

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
        // Transparent surface: item textures (e.g. diamond sword) carry alpha
        // pixels that must blend against the UI instead of rendering black.
        iconMaterial.SetFloat("_Surface", 1f);
        iconMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        iconMaterial.SetOverrideTag("RenderType", "Transparent");
        iconMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        iconMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        iconMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        iconMaterial.SetInt("_ZWrite", 0);
        modelGo.GetComponent<MeshRenderer>().sharedMaterial = iconMaterial;
    }
}
