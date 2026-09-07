using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

// Crack overlay view (plan M2, CrackOverlayRenderer): one child GO per block
// being mined. Geometry copies every face of the target block model and pushes
// the vertices Epsilon out along their per-vertex normals (the arrays
// ExtendModelMesh appends line up vertex-by-vertex), so depth testing hides
// buried faces while the decal hugs non-full models such as stairs.
public class CrackBlockRenderer : MonoBehaviour
{
    public const int StageCount = 10;
    private const float Epsilon = 0.002f;

    // Block-type session targets -> current crack stage (view-side projection
    // of Duration/CompleteTime; maintained by the session events below).
    public readonly Dictionary<Vector3Int, int> BlockBreakStages = new();
    public bool AnyChanged { get; private set; } = true;

    // Mined target state id per coordinate, captured at session Start from the
    // event DTO (rule R-C2-3/R-C2-6): overlay geometry builds from this cache
    // instead of a render-side block query. Session lifetime only - removed
    // together with the stage entry.
    private readonly Dictionary<Vector3Int, int> blockStateIds = new();
    private readonly Dictionary<Vector3Int, CrackOverlay> overlays = new();
    private static Material crackMaterial;
    private static Rect[] stageRects;   // atlas rect per stage, cached on first use
    private static readonly Rect IdentityRect = new(0, 0, 1, 1);

    // One child GO + mesh per breaking block.
    private class CrackOverlay
    {
        public GameObject go;
        public Mesh mesh;
        public Vector2[] baseUv;        // model-space uv before the stage-rect mapping
        public int lastStage = -1;
    }

    // Mining sessions only: Block target + the attack binding. Right-click use
    // sessions (placing etc.) share the Block target type and must not crack.
    private static bool IsMiningSession(SessionEventData data)
        => data.targetType == InteractionSessionTargetType.Block
            && data.bindingFullName == InteractionManager.AttackBindingName;

    private void Awake()
    {
        EventBus.Instance.Subscribe<InteractionSessionContextStartEvent>(OnInteractionSessionStart);
        EventBus.Instance.Subscribe<InteractionSessionContextTickEvent>(OnInteractionSessionTick);
        EventBus.Instance.Subscribe<InteractionSessionContextInteruptedEvent>(OnInteractionSessionInterupted);
        EventBus.Instance.Subscribe<InteractionSessionContextCompletedEvent>(OnInteractionSessionCompleteded);
    }
    private void Update()
    {
        if(AnyChanged)RebuildMesh();
    }

    private void OnDestroy()
    {
        foreach(var overlay in overlays.Values)
            if(overlay.mesh != null)Destroy(overlay.mesh);
        overlays.Clear();
        EventBus.Instance.Unsubscribe<InteractionSessionContextStartEvent>(OnInteractionSessionStart);
        EventBus.Instance.Unsubscribe<InteractionSessionContextTickEvent>(OnInteractionSessionTick);
        EventBus.Instance.Unsubscribe<InteractionSessionContextInteruptedEvent>(OnInteractionSessionInterupted);
        EventBus.Instance.Unsubscribe<InteractionSessionContextCompletedEvent>(OnInteractionSessionCompleteded);
    }

    private static int GetBlockBreakStage(float duration, float fullTime)
    {
        // A tick may land after Duration already crossed CompleteTime (the
        // completed event removes the entry later in the same frame): clamp.
        return Mathf.Min(StageCount - 1, Mathf.FloorToInt(duration / fullTime * StageCount));
    }

    private void OnInteractionSessionStart(InteractionSessionContextStartEvent evt)
    {
        if(!IsMiningSession(evt.Data))return;
        BlockBreakStages[evt.Data.blockDimCoord] = 0;
        blockStateIds[evt.Data.blockDimCoord] = evt.Data.blockStateId;
        AnyChanged = true;
    }

    private void OnInteractionSessionTick(InteractionSessionContextTickEvent evt)
    {
        if(!IsMiningSession(evt.Data))return;
        var blockCoord = evt.Data.blockDimCoord;
        int newstage = GetBlockBreakStage(evt.Data.durantion, evt.Data.completeTime);
        if(BlockBreakStages.TryGetValue(blockCoord, out int stage) && stage == newstage)return;
        BlockBreakStages[blockCoord] = newstage;
        AnyChanged = true;
    }

    private void OnInteractionSessionInterupted(InteractionSessionContextInteruptedEvent evt)
    {
        if(!IsMiningSession(evt.Data))return;
        BlockBreakStages.Remove(evt.Data.blockDimCoord);
        blockStateIds.Remove(evt.Data.blockDimCoord);
        AnyChanged = true;
    }

    private void OnInteractionSessionCompleteded(InteractionSessionContextCompletedEvent evt)
    {
        if(!IsMiningSession(evt.Data))return;
        BlockBreakStages.Remove(evt.Data.blockDimCoord);
        blockStateIds.Remove(evt.Data.blockDimCoord);
        AnyChanged = true;
    }

    // Snapshots BlockBreakStages into a temp dictionary, then reconciles the
    // overlay GOs against it: destroy entries that vanished, apply the current
    // stage to live ones, build new ones.
    public void RebuildMesh()
    {
        var tempDict = new Dictionary<Vector3Int, int>(BlockBreakStages);

        foreach(var coord in new List<Vector3Int>(overlays.Keys))
            if(!tempDict.ContainsKey(coord))DestroyOverlay(coord);

        foreach(var kvp in tempDict)
        {
            if(overlays.TryGetValue(kvp.Key, out var overlay))
                ApplyStage(overlay, kvp.Value);
            else
                CreateOverlay(kvp.Key, kvp.Value);
        }
        AnyChanged = false;
    }

    private void DestroyOverlay(Vector3Int coord)
    {
        if(!overlays.Remove(coord, out var overlay))return;
        if(overlay.mesh != null)Destroy(overlay.mesh);
        if(overlay.go != null)Destroy(overlay.go);
    }

    // Builds the crack shell of the block currently occupying coord: every face
    // of its model (no occlusion mask), uv kept in model space, vertices lifted
    // Epsilon along their normals. The parent GO sits at the world origin, so
    // the child localPosition == dimension coordinate matches the block space
    // ExtendModelMesh expects.
    private void CreateOverlay(Vector3Int coord, int stage)
    {
        // Rule R-C2-6: the mined target's state id comes from the cache the
        // Start handler captured from the event DTO - no render-side block
        // query, no dimension access.
        if(!blockStateIds.TryGetValue(coord, out int cachedId) || cachedId == 0)return;
        ushort stateId = (ushort)cachedId;
        BlockState state = ResourceSystem.Instance.GetState(stateId);
        if(state == null || !ResourceSystem.Instance.CustomModels.TryGetResourceWithFullName(state.ModelId, out var model))return;

        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colors = new List<Color>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();
        var faceRects = new Dictionary<string, Rect>();
        foreach(string face in model.GetAllFaceId())faceRects[face] = IdentityRect;

        model.ExtendModelMesh(Vector3.zero, verts, uvs, colors, normals, triangles,
            null, faceRects, state.RotationX, state.RotationY, state.RotationZ);

        // Every appended vertex has a matching normal (the parser emits one per
        // vertex; ExtendModelMesh rotates it): shift along it for the decal gap.
        for(int i = 0; i < verts.Count; i++)
            verts[i] += normals[i] * Epsilon;

        var go = new GameObject($"CrackOverlay {coord}");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = coord;
        var filter = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = GetMaterial();

        var mesh = new Mesh { name = $"CrackOverlay Mesh {coord}" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        filter.sharedMesh = mesh;

        var overlay = new CrackOverlay { go = go, mesh = mesh, baseUv = uvs.ToArray() };
        overlays[coord] = overlay;
        ApplyStage(overlay, stage);
    }

    // Stage switch only rewrites the uv array (crack texture rect of the new
    // stage); geometry never rebuilds during a session.
    private void ApplyStage(CrackOverlay overlay, int stage)
    {
        stage = Mathf.Clamp(stage, 0, StageCount - 1);
        if(stage == overlay.lastStage)return;
        overlay.lastStage = stage;
        Rect r = GetStageRect(stage);
        var uv = new Vector2[overlay.baseUv.Length];
        for(int i = 0; i < uv.Length; i++)
            uv[i] = new Vector2(overlay.baseUv[i].x * r.width + r.x, overlay.baseUv[i].y * r.height + r.y);
        overlay.mesh.uv = uv;
    }

    private static Rect GetStageRect(int stage)
    {
        if(stageRects == null)
        {
            stageRects = new Rect[StageCount];
            var textures = ResourceSystem.Instance.Textures;
            for(int i = 0; i < StageCount; i++)
                if(textures.TryGetResourceWithFullName($"{Minecraft.ModId}:crack_{i}", out var tex))
                    stageRects[i] = tex.AtlasUVRect;
        }
        Rect r = stageRects[stage];
        if(r.width <= 0f || r.height <= 0f)
        {
            Debug.LogWarning($"[CrackBlockRenderer] {Minecraft.ModId}:crack_{stage} is not packed into the atlas");
            r = IdentityRect;
        }
        return r;
    }

    // Transparent unlit so crack sprites blend over the block; ZWrite off with
    // LEqual depth lets occluded decal faces be culled by the opaque chunk mesh.
    // _Surface=1 is required: URP forces the fragment alpha to 1 in the opaque
    // surface path (UnlitForwardPass.hlsl OutputAlpha), which would draw the
    // white sprite background at full opacity.
    private static Material GetMaterial()
    {
        if(crackMaterial != null)return crackMaterial;
        crackMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        crackMaterial.SetFloat("_Surface", 1f);
        crackMaterial.SetFloat("_Blend", 0f);   // alpha blend mode
        crackMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        crackMaterial.SetOverrideTag("RenderType", "Transparent");
        crackMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        crackMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        crackMaterial.SetInt("_SrcBlendAlpha", (int)BlendMode.SrcAlpha);
        crackMaterial.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
        crackMaterial.SetInt("_ZWrite", 0);
        crackMaterial.renderQueue = (int)RenderQueue.Transparent;
        crackMaterial.SetTexture("_BaseMap", ResourceSystem.Instance.BlockAtlas);
        return crackMaterial;
    }
}
