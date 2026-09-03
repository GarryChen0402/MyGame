using System.Collections.Generic;
using UnityEngine;

// Hidden rendering stage for UI-embedded entity models (design doc
// Docs/玩家界面-模型预览组件与2x2个人合成设计方案.md §4.2). One shared
// orthographic camera on a dedicated layer renders every registered preview
// model into that UI's own RenderTexture - serially, one at a time. Unity
// cameras pick up every active renderer on their culling layer regardless of
// hierarchy, so per-instance scenes would paint each other's models; hiding
// the other instances while one renders keeps the stage on a single layer
// with an unbounded instance count.
public static class EntityModelPreviewRenderSystem
{
    private const string PreviewLayerName = "ModelPreview";
    private const int FallbackLayer = 30;   // highest free slot (31 is ItemIcon)

    // Stage camera framing for the default 2m player model: feet sit near the
    // lower frame edge with headroom above. Tunable per model family later.
    public static float OrthoSize = 1.25f;
    public static float CameraHeight = 1.15f;
    public static float CameraDistance = 5f;

    private static bool initialized;
    private static int stageLayer;
    private static int stageLayerMask;
    private static Camera stageCamera;
    private static Transform stageRoot;
    private static Material stageMaterial;
    private static readonly List<EntityModelPreviewUI> registered = new();

    // Builds the model tree under the stage and registers it for rendering.
    // Returns false (with a warning) when the model resource is missing.
    public static bool Register(EntityModelPreviewUI ui, string modelFullName,
                                Dictionary<string, string> faceTextureIds, out EntityVisual visual)
    {
        visual = null;
        EnsureInit();
        if(!ResourceSystem.Instance.EntityModels.TryGetResourceWithFullName(modelFullName, out var model))
        {
            Debug.LogWarning($"[EntityModelPreview] model '{modelFullName}' is not registered (EntityModels)");
            return false;
        }
        // The stage camera sits on -Z looking toward +Z (main-camera convention);
        // the model's front face points +Z, so the tree is turned 180° to face
        // the camera without mirroring.
        visual = EntityVisualBuilder.Build(model, faceTextureIds, stageMaterial, stageRoot);
        visual.Root.localRotation = Quaternion.Euler(0f, 180f, 0f);
        SetLayerRecursive(visual.Root, stageLayer);
        registered.Add(ui);
        return true;
    }

    public static void Unregister(EntityModelPreviewUI ui)
    {
        if(ui == null)return;
        if(!registered.Remove(ui))return;
        if(ui.Visual != null && ui.Visual.Root != null)
            Object.Destroy(ui.Visual.Root.gameObject);
    }

    // Renders the given preview into its RenderTexture right now: every other
    // registered model is hidden for the draw call, then restored.
    public static void Render(EntityModelPreviewUI ui)
    {
        EnsureInit();
        if(ui == null || ui.Visual == null || ui.RenderTarget == null)return;
        foreach(var other in registered)
            if(other != ui && other.Visual != null)
                SetRenderersEnabled(other.Visual, false);
        stageCamera.targetTexture = ui.RenderTarget;
        stageCamera.Render();
        stageCamera.targetTexture = null;
        foreach(var other in registered)
            if(other != ui && other.Visual != null)
                SetRenderersEnabled(other.Visual, true);
    }

    private static void SetRenderersEnabled(EntityVisual visual, bool enabled)
    {
        foreach(var kv in visual.PartTransforms)
        {
            var renderer = kv.Value.GetComponent<MeshRenderer>();
            if(renderer != null)renderer.enabled = enabled;
        }
    }

    // Builder-created trees default to layer 0; every GO must sit on the stage
    // layer or the stage camera (cullingMask = stage layer only) never draws them.
    private static void SetLayerRecursive(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for(int i = 0; i < root.childCount; i++)
            SetLayerRecursive(root.GetChild(i), layer);
    }

    private static void EnsureInit()
    {
        if(initialized)return;
        initialized = true;

        stageLayer = LayerMask.NameToLayer(PreviewLayerName);
        if(stageLayer < 0)
        {
            stageLayer = FallbackLayer;
            Debug.LogWarning($"[EntityModelPreview] Layer '{PreviewLayerName}' not found in Tags & Layers, " +
                             $"using layer {stageLayer} (add the layer to move off the fallback).");
        }
        stageLayerMask = 1 << stageLayer;

        // The stage lives in the world scene: hide it from every camera that
        // renders the world; only the stage camera renders this layer.
        foreach(var cam in Camera.allCameras)cam.cullingMask &= ~stageLayerMask;

        var root = new GameObject("[EntityModelPreviewStage]");
        Object.DontDestroyOnLoad(root);
        root.layer = stageLayer;
        stageRoot = root.transform;

        var camGo = new GameObject("PreviewCamera");
        camGo.transform.SetParent(stageRoot, false);
        camGo.transform.position = new Vector3(0f, CameraHeight, -CameraDistance);
        camGo.transform.LookAt(new Vector3(0f, CameraHeight, 0f));

        stageCamera = camGo.AddComponent<Camera>();
        stageCamera.orthographic = true;
        stageCamera.orthographicSize = OrthoSize;
        stageCamera.clearFlags = CameraClearFlags.SolidColor;
        stageCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);   // transparent: UI shows through
        stageCamera.cullingMask = stageLayerMask;
        stageCamera.allowHDR = false;
        stageCamera.allowMSAA = false;
        // Disabled: the camera only ever renders on demand into a preview's
        // RenderTexture. Left enabled it would also auto-render to the screen
        // every frame and - created after the world camera - paint over the
        // whole view with the stage contents.
        stageCamera.enabled = false;

        // Unlit: the stage has no lights, shading comes from the baked mesh
        // colors (same setup as the item icon scene).
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
        stageMaterial = new Material(shader);
        stageMaterial.SetTexture("_BaseMap", ResourceSystem.Instance.BlockAtlas);
        stageMaterial.SetFloat("_Surface", 1f);
        stageMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        stageMaterial.SetOverrideTag("RenderType", "Transparent");
        stageMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        stageMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        stageMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        stageMaterial.SetInt("_ZWrite", 0);
    }
}
