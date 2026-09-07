using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// UI component rendering a 3D entity model into its own RenderTexture, shown
// through a RawImage - mirroring ItemIconRenderer usage, but for full entity
// models (design doc Docs/玩家界面-模型预览组件与2x2个人合成设计方案.md §4):
// AddComponent on a sized GO, call Setup() once, optionally enable the mouse
// look-tracking (vanilla InventoryScreen behavior: head - and optionally the
// body - turn toward the cursor).
public class EntityModelPreviewUI : MonoBehaviour, EntityModelPreviewRenderSystem.IModelPreviewHost
{
    public enum FollowMode
    {
        Static,        // fixed pose, no cursor tracking
        HeadOnly,      // head turns around the neck pivot; body stays facing the camera
        HeadAndBody    // body turns horizontally first, head follows on top
    }

    public FollowMode Mode = FollowMode.HeadOnly;

    // Part StringIds of the bound EntityModel (defaults fit minecraft:player).
    public string HeadPartName = "head";
    public string BodyPartName = "waist";

    public float HeadYawClamp = 45f;        // head horizontal range, degrees
    public float HeadPitchClamp = 25f;      // head vertical range, degrees
    public float BodyYawClamp = 30f;        // HeadAndBody: body horizontal range
    public float SmoothTime = 0.1f;         // angle smoothing (0 = instant)
    public float RotationSpeed = 0.35f;     // screen pixel offset -> degrees
    public int TextureSize = 256;

    private RawImage image;
    private RenderTexture rt;
    private bool registered;                // Setup() succeeded and stage tree exists
    private EntityVisual visual;
    private bool hasHead, hasBody;

    // Smoothed cursor angles in screen space (degrees, screen-center origin).
    private float yawScreen, pitchScreen, bodyYawScreen;
    private float yawVel, pitchVel, bodyVel;
    private float lastRenderedYaw, lastRenderedPitch, lastRenderedBody;
    private const float RenderAngleThreshold = 0.2f;   // render again only past this
    private bool forceRender;                          // first frame / re-enable

    public EntityVisual Visual => visual;
    public RenderTexture RenderTarget => rt;

    private void Awake()
    {
        image = GetComponent<RawImage>();
        if(image == null)image = gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;   // purely visual, never swallows clicks
    }

    // Binds the component to an EntityModel resource (registry full name) and
    // its face textures (face name -> texture FullName; null = plain atlas
    // rects, same as EntityVisualBuilder). Returns false when the model is
    // missing; the preview then stays blank.
    public bool Setup(string modelFullName, Dictionary<string, string> faceTextureIds)
    {
        EnsureRT();
        if(registered)EntityModelPreviewRenderSystem.Unregister(this);   // re-Setup drops the old tree
        if(!EntityModelPreviewRenderSystem.Register(this, modelFullName, faceTextureIds, out visual))
            return false;
        registered = true;
        hasHead = visual.PartTransforms.ContainsKey(HeadPartName);
        hasBody = visual.PartTransforms.ContainsKey(BodyPartName);
        image.texture = rt;
        forceRender = true;
        return true;
    }

    // Re-renders the preview immediately (e.g. external pose/model changes).
    public void ReRender()
    {
        if(!registered)return;
        forceRender = true;
    }

    // Returns head/body to their base pose (facing the camera).
    public void ResetPose()
    {
        yawScreen = pitchScreen = bodyYawScreen = 0f;
        yawVel = pitchVel = bodyVel = 0f;
        ApplyPose();
        forceRender = true;
    }

    // On disable the stage tree and RT stay alive; Update stops (inactive GO)
    // and the next Enable triggers a force render through OnEnable below.
    private void OnEnable()
    {
        if(registered)forceRender = true;
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

    private void EnsureRT()
    {
        if(rt != null)return;
        rt = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.ARGB32);
        rt.Create();
    }

    private void Update()
    {
        if(!registered || Mode == FollowMode.Static)return;
        if(!gameObject.activeInHierarchy)return;

        var rect = (RectTransform)transform;
        var canvas = GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        // Rect-space offset from the preview center. Overlay UI converts any
        // in-window mouse position successfully, so a cursor far outside the
        // preview simply saturates the clamps (vanilla: the head does not snap
        // back until the cursor is near the model center again).
        Vector2 local = rect.rect.center;
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, Input.mousePosition, cam, out Vector2 lp))
            local = lp;
        Vector2 d = local - rect.rect.center;

        float rawYaw = d.x * RotationSpeed;
        // Screen y grows upward while a positive Euler x pitch looks down, so a
        // cursor below the center (d.y < 0) must yield a positive pitch.
        float rawPitch = -d.y * RotationSpeed;
        float desiredPitch = Mathf.Clamp(rawPitch, -HeadPitchClamp, HeadPitchClamp);

        // HeadAndBody splits the horizontal angle: the body takes the inner
        // range (slower, via a longer smoothing time below), the head covers
        // the remainder, mirroring vanilla's body-then-head look.
        float desiredHeadYaw, desiredBodyYaw = 0f;
        if(Mode == FollowMode.HeadAndBody)
        {
            float maxTotal = HeadYawClamp + BodyYawClamp;
            float total = Mathf.Clamp(rawYaw, -maxTotal, maxTotal);
            desiredBodyYaw = Mathf.Clamp(total, -BodyYawClamp, BodyYawClamp);
            desiredHeadYaw = total - desiredBodyYaw;
        }
        else desiredHeadYaw = Mathf.Clamp(rawYaw, -HeadYawClamp, HeadYawClamp);

        yawScreen = Mathf.SmoothDampAngle(yawScreen, desiredHeadYaw, ref yawVel, SmoothTime);
        pitchScreen = Mathf.SmoothDampAngle(pitchScreen, desiredPitch, ref pitchVel, SmoothTime);
        bodyYawScreen = Mathf.SmoothDampAngle(bodyYawScreen, desiredBodyYaw, ref bodyVel,
            SmoothTime * 3f);   // the body deliberately lags the head

        ApplyPose();

        // Render only when the pose moved enough (mouse at rest = no draws).
        if(forceRender ||
           Mathf.Abs(yawScreen - lastRenderedYaw) > RenderAngleThreshold ||
           Mathf.Abs(pitchScreen - lastRenderedPitch) > RenderAngleThreshold ||
           Mathf.Abs(bodyYawScreen - lastRenderedBody) > RenderAngleThreshold)
        {
            forceRender = false;
            lastRenderedYaw = yawScreen;
            lastRenderedPitch = pitchScreen;
            lastRenderedBody = bodyYawScreen;
            EntityModelPreviewRenderSystem.Render(this);
        }
    }

    // Writes the smoothed angles onto the model parts. The stage tree is
    // turned 180° to face the camera, which swaps the model's left/right with
    // the screen's, so horizontal angles apply negated. Vertical angles rotate
    // around the part's own horizontal axis: positive pitch looks down and
    // arrives pre-negated from Update (screen y grows upward). If an in-editor
    // look still shows horizontal inversion, flip the yaw sign only (design
    // doc §4.3 calibration point).
    private void ApplyPose()
    {
        if(!registered)return;
        if(hasHead)
            visual.PartTransforms[HeadPartName].localRotation = Quaternion.Euler(pitchScreen, -yawScreen, 0f);
        if(hasBody && Mode == FollowMode.HeadAndBody)
            visual.PartTransforms[BodyPartName].localRotation = Quaternion.Euler(0f, -bodyYawScreen, 0f);
    }

    // Factory mirroring ProgressBarUI.AddProgressBar: builds a sized GO with
    // the component and Setup()s it in one call.
    public static GameObject AddEntityModelPreview(string name, Vector3 position, Vector2 size,
        string modelFullName, Dictionary<string, string> faceTextureIds)
    {
        var go = new GameObject(name);
        go.transform.localPosition = position;
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        var preview = go.AddComponent<EntityModelPreviewUI>();
        if(!preview.Setup(modelFullName, faceTextureIds))
            Debug.LogWarning($"[EntityModelPreview] preview '{name}' failed to set up (see warning above)");
        return go;
    }
}
