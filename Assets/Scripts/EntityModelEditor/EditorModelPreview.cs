using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Interactive 3D preview of an EntityModel - or a single subtree of it - for
// the model editor (design doc §5.1). Own RenderTexture shown through a
// raycastable RawImage; drag orbits the model (yaw unlimited, pitch clamped
// around the horizontal), click picks the face under the cursor. Picking
// raycasts through the shared stage camera; every cube GO carries a temporary
// MeshCollider while the preview is alive. Rebuild() re-registers the tree
// with the same orbit angles, so UV edits appear without the view jumping.
// FitContent auto-scales/centers the rendered bounds into the stage viewport,
// and a few post-rebuild re-renders cover the warm-up frame that otherwise
// shows blank.
public class EditorModelPreview : MonoBehaviour, EntityModelPreviewRenderSystem.IModelPreviewHost,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public int TextureSize = 256;
    public float DragSensitivity = 0.3f;    // degrees of orbit per pixel dragged
    public float PitchLimit = 80f;          // vertical orbit clamp, degrees
    public bool FitContent = true;          // auto-frame the model into the stage view
    public float FitMargin = 0.8f;          // fraction of the view height the model may fill

    // Picked-face notifications: (cube StringId, face key). A click that hits
    // nothing fires with (null, null) so hosts can clear their selection.
    public readonly UnityEvent<string, string> OnFacePicked = new();

    // Direction order in which EntityModelParser bakes faces; the baked cube
    // mesh triangles (2 per face) and thus the collider indices follow it,
    // with missing faces simply shifted out. Public: hosts use it to pick the
    // default-selected face of a cube.
    public static readonly string[] FaceOrder =
        { "top", "bottom", "front", "back", "left", "right" };

    private const float ClickSlop = 4f;   // drags under this distance count as clicks

    private RawImage image;
    private RenderTexture rt;
    private EntityVisual visual;
    private bool registered;
    private EntityModel model;
    private Dictionary<string, string> faceTextureIds;
    private string subtreeRootId;

    // User orbit on top of the base 180° pose that faces the model to the
    // camera (see ApplyOrbit).
    private float yaw, pitch;
    private bool dragging;
    private Vector2 lastPointerPos;
    private bool pointerMoved;
    // The first Camera.Render into a fresh RenderTexture can come up blank
    // (stage camera / pipeline warm-up), so Rebuild and re-enable schedule a
    // few extra renders over the following frames instead of trusting the one
    // synchronous draw.
    private int pendingRenders;

    // Selection highlight: the picked face's 4 baked vertices are tinted in
    // the preview mesh only. The original per-vertex colors are backed up here
    // and restored on deselect, so the model data never sees the tint.
    private static readonly Color HighlightColor = new(1f, 0.95f, 0.6f);
    private const float HighlightMix = 0.55f;
    private readonly Dictionary<MeshFilter, Color[]> originalMeshColors = new();
    private MeshFilter highlightedFilter;

    public EntityVisual Visual => visual;
    public RenderTexture RenderTarget => rt;

    private void Awake()
    {
        image = gameObject.AddComponent<RawImage>();
        image.raycastTarget = true;   // on purpose: the preview receives the drag/click events
        rt = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.ARGB32);
        rt.Create();
    }

    // Binds the component to a model instance (null subtreeRootId = the whole
    // model, otherwise only the part subtree). Drops any previous tree.
    public bool Setup(EntityModel model, Dictionary<string, string> faceTextureIds, string subtreeRootId = null)
    {
        this.model = model;
        this.faceTextureIds = faceTextureIds;
        this.subtreeRootId = subtreeRootId;
        return Rebuild();
    }

    // Rebuilds the preview tree from the current model - call after a UV edit
    // was committed into the session model. The orbit view is kept.
    public bool Rebuild()
    {
        if(model == null)return false;
        ClearHighlight();   // the old tree is destroyed right below; drop backups with it
        if(registered)EntityModelPreviewRenderSystem.Unregister(this);
        registered = subtreeRootId == null
            ? EntityModelPreviewRenderSystem.RegisterFromModel(this, model, faceTextureIds, out visual)
            : EntityModelPreviewRenderSystem.RegisterFromSubtree(this, model, subtreeRootId, faceTextureIds, out visual);
        if(registered)
        {
            image.texture = rt;
            ApplyOrbit();
            AddPickColliders();
            EntityModelPreviewRenderSystem.Render(this);
            pendingRenders = Mathf.Max(pendingRenders, 2);   // safety net for the blank-first-frame case
        }
        return registered;
    }

    private void OnEnable()
    {
        if(registered)pendingRenders = Mathf.Max(pendingRenders, 2);
    }

    private void Update()
    {
        if(!registered || pendingRenders <= 0)return;
        pendingRenders--;
        EntityModelPreviewRenderSystem.Render(this);
    }

    private void OnDestroy()
    {
        EntityModelPreviewRenderSystem.Unregister(this);
        if(rt != null)
        {
            rt.Release();
            Destroy(rt);
            rt = null;
        }
        registered = false;
    }

    // ---- drag to orbit ----

    public void OnPointerDown(PointerEventData eventData)
    {
        dragging = true;
        pointerMoved = false;
        lastPointerPos = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if(!dragging)return;
        Vector2 d = eventData.position - lastPointerPos;
        lastPointerPos = eventData.position;
        if(d.magnitude > ClickSlop)pointerMoved = true;

        // Grab-the-model feel: dragging right turns the model's right side
        // toward the camera. If the orbit feels mirrored in play, flip the two
        // signs below (the 180° base pose mirrors horizontal drag direction).
        yaw -= d.x * DragSensitivity;
        pitch -= d.y * DragSensitivity;
        pitch = Mathf.Clamp(pitch, -PitchLimit, PitchLimit);
        ApplyOrbit();
        EntityModelPreviewRenderSystem.Render(this);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if(!dragging)return;
        dragging = false;
        if(!pointerMoved)PickFace(eventData.position);
    }

    // Applies the orbit on top of the base pose. Quaternion.Euler(pitch, 180 +
    // yaw, 0) rotates around the model's own axes, so the pitch limit never
    // flips the model past its poles regardless of yaw.
    private void ApplyOrbit()
    {
        if(visual == null)return;
        visual.Root.localRotation = Quaternion.Euler(pitch, 180f + yaw, 0f);
        if(!FitContent)return;
        // Auto-frame: reset the tree to unit scale at the origin so the renderer
        // bounds describe the model geometry alone, then scale+center it into
        // the stage viewport (square RT => view height == view width). Bounds
        // are re-measured on every orbit since rotation changes them.
        visual.Root.localScale = Vector3.one;
        visual.Root.localPosition = Vector3.zero;
        Bounds b = new Bounds();
        bool any = false;
        foreach(var kv in visual.PartTransforms)
        {
            var r = kv.Value.GetComponent<MeshRenderer>();
            if(r == null)continue;
            if(!any)
            {
                b = r.bounds;
                any = true;
            }
            else b.Encapsulate(r.bounds);
        }
        if(!any)return;
        float view = 2f * EntityModelPreviewRenderSystem.OrthoSize;
        float s = view * FitMargin / Mathf.Max(b.size.x, b.size.y);
        visual.Root.localScale = new Vector3(s, s, s);
        // The camera looks at (0, CameraHeight, 0), so that point must end up
        // at the scaled bounds center. The stage root sits at the world origin,
        // which keeps local and world space interchangeable here.
        visual.Root.localPosition = new Vector3(0f, EntityModelPreviewRenderSystem.CameraHeight, 0f)
            - b.center * s;
    }

    // ---- selection highlight ----

    // Tints the 4 baked vertices of the given cube face. The highlight lives
    // only on the preview meshes; SelectFace(null, null) clears it. Returns
    // false when the face cannot be shown (missing cube/face/part).
    public bool SelectFace(string cubeId, string faceKey)
    {
        ClearHighlight();
        if(cubeId == null || faceKey == null || model == null)return false;
        if(!model.Cubes.TryGetValue(cubeId, out var cube) || !cube.Faces.ContainsKey(faceKey))return false;
        if(!visual.PartTransforms.TryGetValue(cubeId, out var part))return false;
        var filter = part.GetComponent<MeshFilter>();
        if(filter == null || filter.sharedMesh == null)return false;
        int idx = BakedFaceIndex(cube, faceKey);
        if(idx < 0)return false;
        var mesh = filter.sharedMesh;
        Color[] colors = mesh.colors;   // returns a fresh copy; SetColors writes it back
        originalMeshColors[filter] = colors;
        highlightedFilter = filter;
        int first = idx * 4;   // one face = 4 consecutive vertices in bake order
        for(int i = first; i < first + 4 && i < colors.Length; i++)
            colors[i] = Color.Lerp(colors[i], HighlightColor, HighlightMix);
        mesh.SetColors(colors);
        return true;
    }

    // Restores the tinted vertices to their original colors.
    public void ClearHighlight()
    {
        if(highlightedFilter != null &&
           originalMeshColors.TryGetValue(highlightedFilter, out Color[] original))
        {
            var mesh = highlightedFilter.sharedMesh;
            if(mesh != null)mesh.SetColors(original);
        }
        highlightedFilter = null;
        originalMeshColors.Clear();
    }

    // Position of faceKey among the cube's baked faces (FaceOrder intersected
    // with the present faces) - the exact ordering the mesh vertex groups, the
    // triangles and the pick collider indices follow.
    private int BakedFaceIndex(CustomCube cube, string faceKey)
    {
        int baked = 0;
        foreach(string key in FaceOrder)
        {
            if(!cube.Faces.ContainsKey(key))continue;
            if(key == faceKey)return baked;
            baked++;
        }
        return -1;
    }

    // ---- click to pick a face ----

    private void PickFace(Vector2 screenPos)
    {
        var camera = EntityModelPreviewRenderSystem.StageCamera;
        if(camera == null || visual == null)
        {
            OnFacePicked.Invoke(null, null);
            return;
        }
        var rect = (RectTransform)transform;
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPos, null, out Vector2 local))
        {
            OnFacePicked.Invoke(null, null);
            return;
        }
        Vector2 size = rect.rect.size;
        if(size.x <= 0f || size.y <= 0f)
        {
            OnFacePicked.Invoke(null, null);
            return;
        }
        // The RawImage stretches the (square) RT across the rect, so hosts
        // should keep the preview square or the pick ray drifts off the shown
        // pixels.
        Vector2 uv = new((local.x + size.x * 0.5f) / size.x, (local.y + size.y * 0.5f) / size.y);
        Ray ray = camera.ViewportPointToRay(uv);

        // All preview trees share the stage near the origin (others are hidden
        // only while rendering), so accept the nearest hit of our own tree.
        var hits = Physics.RaycastAll(ray, 100f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach(var hit in hits)
        {
            if(!BelongsToVisual(hit.transform))continue;
            if(ResolveFace(hit, out string cubeId, out string faceKey))
            {
                OnFacePicked.Invoke(cubeId, faceKey);
                return;
            }
        }
        OnFacePicked.Invoke(null, null);
    }

    private bool BelongsToVisual(Transform t)
    {
        while(t != null)
        {
            if(t == visual.Root)return true;
            t = t.parent;
        }
        return false;
    }

    // Full name on purpose: the project defines a voxel RaycastHit struct
    // (Physics/Raycaster.cs) in the global namespace, which shadows the
    // UnityEngine one in files without a namespace.
    private bool ResolveFace(UnityEngine.RaycastHit hit, out string cubeId, out string faceKey)
    {
        cubeId = hit.transform.name;
        faceKey = null;
        if(model == null || !model.Cubes.TryGetValue(cubeId, out var cube))return false;

        // Every face bakes exactly 2 triangles in FaceOrder; the mesh (and the
        // collider) therefore hold one face per present direction, in order.
        int faceIndex = hit.triangleIndex / 2;
        int baked = 0;
        foreach(string key in FaceOrder)
        {
            if(!cube.Faces.ContainsKey(key))continue;
            if(baked == faceIndex)
            {
                faceKey = key;
                return true;
            }
            baked++;
        }
        return false;
    }

    private void AddPickColliders()
    {
        foreach(var kv in visual.PartTransforms)
        {
            var filter = kv.Value.GetComponent<MeshFilter>();
            if(filter == null)continue;   // hierarchy-only group nodes have no mesh
            var collider = kv.Value.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
        }
    }
}
