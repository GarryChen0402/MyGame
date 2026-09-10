using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Hover-info feed of the general tooltip layer: whatever the pointer sits on
// (SlotUI today, JEI cells next) reports its current item here. Pull model -
// the tooltip reads UIManager.CurrentHoverInfo once per frame, no event wiring.
public interface IHoverItemSource
{
    bool TryGetHoverItemId(out ushort itemId);
}

// Single arbiter of "is this hover source still real": a panel closed / hidden
// under the pointer deactivates its slots without an exit event, and teardown
// destroys them outright - both count as no hover. Shared by the tooltip and
// the JEI R/U path.
public static class HoverSource
{
    public static IHoverItemSource Validate(IHoverItemSource source)
    {
        if(source is Component component && (component == null || !component.gameObject.activeInHierarchy))return null;
        return source;
    }
}

// General hover tooltip (D6 of Docs/JEI式物品浏览器-功能调研与设计草案.md):
// two lines - derived item name + modId - over a small backdrop, shown after a
// short dwell and following the mouse. Lives on TooltipRoot; every graphic is
// raycast-transparent so it can never swallow clicks. Hidden while a drag
// session is active (Unity stops routing enter/exit then, so the hover feed
// is stale by design).
public class TooltipUI : UIBehavior
{
    private const float ShowDelay = 0.3f;
    private const float Width = 260f;
    private const float Height = 68f;
    private const float CursorOffset = 18f;
    private const float EdgeMargin = 8f;

    private RectTransform rt;
    private GameObject content;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI modText;

    private IHoverItemSource lastSource;
    private float hoverStartTime;
    private ushort currentItemId;

    private void Awake()
    {
        // TooltipRoot's children are built without a RectTransform; adding one
        // defers the transform replacement (same stance as HeldItemUI).
        // Sized (not a zero-size point like HeldItemUI): FollowMouse places the
        // pivot corner at the cursor, so the box grows away from it instead of
        // centering on it.
        rt = gameObject.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(Width, Height);

        // Visible body toggled on/off; this GO stays active so Update keeps
        // polling for a new hover (an inactive GO would stop its own Update).
        content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(transform, false);
        var contentRt = (RectTransform)content.transform;
        contentRt.anchorMin = Vector2.zero;
        contentRt.anchorMax = Vector2.one;
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;

        var bg = UIWidgetBackground.CreateNewBackground();
        bg.transform.SetParent(content.transform, false);
        // Raycast-transparent: a hit-catching graphic here would sit under the
        // pointer and loop the hover feed (exit -> hide -> enter -> show).
        bg.GetComponent<Image>().raycastTarget = false;

        nameText = MakeLine("Name", 22f, Color.black, -10f);
        modText = MakeLine("Mod", 15f, new Color(0.25f, 0.25f, 0.25f, 1f), -42f);
        content.SetActive(false);
    }

    private TextMeshProUGUI MakeLine(string label, float fontSize, Color color, float y)
    {
        var go = UITextWidget.CreateNewText("", content);
        go.name = label;
        var textRt = (RectTransform)go.transform;
        textRt.anchorMin = textRt.anchorMax = new Vector2(0f, 1f);
        textRt.pivot = new Vector2(0f, 1f);
        textRt.sizeDelta = new Vector2(Width - 24f, fontSize + 8f);
        textRt.anchoredPosition = new Vector2(12f, y);
        var widget = go.GetComponent<UITextWidget>();
        widget.SetFontSize(fontSize);
        widget.SetColor(color);
        widget.SetAlignment(TextAlignmentOptions.Left);
        return go.GetComponent<TextMeshProUGUI>();
    }

    private void Update()
    {
        var ui = UIManager.Instance;
        IHoverItemSource source = HoverSource.Validate(ui == null ? null : ui.CurrentHoverInfo);
        // Declared outside the short-circuit: valid==true implies the out
        // parameter ran, but the compiler cannot correlate the two.
        ushort itemId = 0;
        bool valid = source != null && !ui.DragActive && source.TryGetHoverItemId(out itemId);
        if(!valid)
        {
            lastSource = null;
            SetVisible(false);
            return;
        }

        // Dwell timer restarts whenever the pointer moves to another source;
        // moving inside the same slot keeps it running.
        if(source != lastSource)
        {
            lastSource = source;
            hoverStartTime = Time.unscaledTime;
        }
        if(Time.unscaledTime - hoverStartTime < ShowDelay)
        {
            SetVisible(false);
            return;
        }

        if(currentItemId != itemId)
        {
            currentItemId = itemId;
            ResourceSystem.Instance.ItemDefinitions.TryGetStringId(itemId, out string fullName);
            // Name derivation is per-hover (one allocation per change), not per frame.
            nameText.text = ItemNames.DisplayName(fullName);
            modText.text = ItemNames.ModId(fullName);
        }
        SetVisible(true);
        FollowMouse();
    }

    private void SetVisible(bool visible)
    {
        if(content.activeSelf != visible)content.SetActive(visible);
    }

    private void FollowMouse()
    {
        if(rt == null)return;
        var parentRt = (RectTransform)transform.parent;
        var canvas = GetComponentInParent<Canvas>();
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(
               parentRt, Input.mousePosition, canvas == null ? null : canvas.worldCamera, out var local))
            return;

        // Grows right-up from the cursor; flips to the left near the screen's
        // right edge (the JEI strip lives there) so the text never clips.
        float halfWidth = parentRt.rect.width * 0.5f;
        Vector2 pos = local + new Vector2(CursorOffset, CursorOffset * 0.66f);
        if(pos.x + Width > halfWidth - EdgeMargin)
        {
            rt.pivot = new Vector2(1f, 0f);
            pos.x = local.x - CursorOffset;
        }
        else
        {
            rt.pivot = new Vector2(0f, 0f);
        }
        rt.localPosition = pos;
    }

    public static UIDefinition tooltipUIDefinition = new()
    {
        modId = "minecraft",
        name = "tooltip",
        Kind = UIKind.Tooltip,
        OpenWithPlayerInventory = false,
        Factory = () =>
        {
            var go = new GameObject("Tooltip");
            go.AddComponent<TooltipUI>();
            return go;
        }
    };
}
