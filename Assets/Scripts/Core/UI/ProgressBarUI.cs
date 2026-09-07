using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

// Generic progress indicator for mod GUIs, mirroring SlotUI usage: AddComponent
// on a sized GO, call Setup() once, then drive Progress each frame. Two child
// Images (Back / Front) render the background under the fillAmount cutout, so
// no custom shader / clip logic is needed.
public class ProgressBarUI : MonoBehaviour
{
    public enum Direction
    {
        LeftToRight,
        RightToLeft,
        BottomToTop,
        TopToBottom,
        RadialClockwise,
        RadialCounterClockwise
    }

    private Image bgImage;
    private Image fillImage;
    private float progress;
    public float Progress
    {
        get => progress;
        set {
            progress = Mathf.Clamp01(value);
            ApplyFill();
        }
    }

    private void ApplyFill()
    {
        if(fillImage == null)return;   // Setup() not called yet
        fillImage.type = Image.Type.Filled;
        fillImage.fillAmount = progress;
    }

    private void Awake()
    {
        gameObject.GetOrAddComponent<RectTransform>();

        // Back added first sits at the bottom of the render order; Front is
        // added after it and carries the fill on top.
        var backGo = new GameObject("Back");
        backGo.transform.SetParent(transform, false);
        bgImage = AddChildImage(backGo);
        bgImage.enabled = false;   // hidden until Setup() provides a back sprite

        var frontGo = new GameObject("Front");
        frontGo.transform.SetParent(transform, false);
        fillImage = AddChildImage(frontGo);
    }

    // Full-stretch child image: tracks this GO's rect, never blocks clicks.
    private static Image AddChildImage(GameObject go)
    {
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var image = go.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;   // never stretch the texture, keep its ratio
        return image;
    }

    public void Setup(Sprite front, Direction direction, Sprite back = null)
    {
        if(back != null)
        {
            bgImage.enabled = true;
            bgImage.sprite = back;
        }
        if(front == null)return;

        fillImage.sprite = front;
        fillImage.type = Image.Type.Filled;

        // Map the semantic direction onto Image's (fillMethod, fillOrigin) pair.
        switch(direction)
        {
            case Direction.LeftToRight:
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                break;
            case Direction.RightToLeft:
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)Image.OriginHorizontal.Right;
                break;
            case Direction.BottomToTop:
                fillImage.fillMethod = Image.FillMethod.Vertical;
                fillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
                break;
            case Direction.TopToBottom:
                fillImage.fillMethod = Image.FillMethod.Vertical;
                fillImage.fillOrigin = (int)Image.OriginVertical.Top;
                break;
            case Direction.RadialClockwise:
                fillImage.fillMethod = Image.FillMethod.Radial360;
                fillImage.fillOrigin = (int)Image.Origin360.Top;
                fillImage.fillClockwise = true;
                break;
            case Direction.RadialCounterClockwise:
                fillImage.fillMethod = Image.FillMethod.Radial360;
                fillImage.fillOrigin = (int)Image.Origin360.Top;
                fillImage.fillClockwise = false;
                break;
        }
        ApplyFill();
    }

    public static GameObject AddProgressBar(string name, Vector3 position, Direction direction, Sprite front, Sprite back)
    {
        var go = new GameObject(name);
        // go.transform.SetParent(transform, false);
        go.transform.localPosition = position;
        go.AddComponent<RectTransform>();
        var bar = go.AddComponent<ProgressBarUI>();
        bar.Setup(front, direction, back);
        return go;
    }
}
