using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    // Drag visual feedback (Docs/物品拖拽分配交互实现方案.md §7 阶段3):
    // Accept = slot will receive the distribution (green outline), Reject =
    // hovered but cannot receive (red outline).
    public enum DragHighlight { None, Accept, Reject }

    private ItemIconRenderer Icon;
    private TextMeshProUGUI Text;
    private Outline highlight;
    private GameObject hoverOverlay;                    // translucent white hover highlight
    private static Sprite whiteSprite;                  // generated 1x1 solid sprite
    private static readonly Color AcceptColor = new(0.35f, 1f, 0.4f);
    private static readonly Color RejectColor = new(1f, 0.3f, 0.25f);
    private static readonly Color HoverColor = new(1f, 1f, 1f, 0.3f);

    private static Sprite GetWhiteSprite()
    {
        if(whiteSprite != null)return whiteSprite;
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        return whiteSprite;
    }

    private void Awake()
    {
        var bg_image = gameObject.AddComponent<Image>();
        bg_image.sprite = Resources.Load<Sprite>("Textures/UI/slot");
        // Explicit rect size: the slot used to inherit the RectTransform
        // default (100x100), silently coupling all layout math to an unnamed
        // number (P0 of Docs/UI布局系统-…).
        ((RectTransform)transform).sizeDelta = new Vector2(UIStyle.SlotSize, UIStyle.SlotSize);
        // var edgeGo = new GameObject("edge");
        // edgeGo.transform.SetParent(gameObject.transform);
        // edgeGo.AddComponent<Image>().sprite = Resources.Load<Sprite>("Textures/UI/slot_ui_edge");

        highlight = bg_image.gameObject.AddComponent<Outline>();
        highlight.effectDistance = new Vector2(3, 3);
        highlight.enabled = false;

        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(gameObject.transform, false);
        iconGo.AddComponent<RectTransform>();
        iconGo.AddComponent<RawImage>();
        Icon = iconGo.AddComponent<ItemIconRenderer>();

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(gameObject.transform, false);
        textGo.transform.localPosition = new Vector3(-2, -25, 0);
        textGo.AddComponent<RectTransform>();

        Text = textGo.AddComponent<TextMeshProUGUI>();
        Text.alignment = TextAlignmentOptions.BottomRight;
        Text.fontSize = UIStyle.CountFontSize;
        Text.color = Color.black;
        Text.fontStyle = FontStyles.Bold;

        // TMP's OnEnable overwrites sizeDelta, so set it after AddComponent.
        var textRect = (RectTransform)Text.transform;
        textRect.sizeDelta = new Vector2(UIStyle.SlotSize, textRect.sizeDelta.y);   // width = one slot

        // Hover highlight: created last so it renders above the icon/text,
        // like vanilla's white overlay on the hovered slot. No raycast
        // target: purely visual, must never swallow clicks.
        hoverOverlay = new GameObject("Hover");
        hoverOverlay.transform.SetParent(transform, false);
        var hoverRt = hoverOverlay.AddComponent<RectTransform>();
        hoverRt.anchorMin = Vector2.zero;
        hoverRt.anchorMax = Vector2.one;
        hoverRt.offsetMin = Vector2.zero;
        hoverRt.offsetMax = Vector2.zero;
        var hoverImage = hoverOverlay.AddComponent<Image>();
        hoverImage.sprite = GetWhiteSprite();
        hoverImage.color = HoverColor;
        hoverImage.raycastTarget = false;
        hoverOverlay.SetActive(false);
    }

    // ---- read-source binding (Phase C: display reads a slot snapshot;
    // S1: ContainerMirror for resident panels, PanelData for BE sessions) ----

    // Display-only slot (hotbar): shows the source value, never interactive.
    // A null source renders empty - bindings may precede the resident player
    // registration during startup, and panels with no data (widget test) show
    // blank slots that stay unclickable.
    public void BindDisplay(ISlotReadSource source, int slotIndex)
    {
        this.source = source;
        this.slotIndex = slotIndex;
        addr = null;
        shown = false;   // first Refresh after a bind always renders
        Refresh();
    }

    // Interactive slot: display plus the address the click/drag commands
    // settle through (see ContainerCommandProcessor; a null address slot is
    // display-only and never clickable).
    public void BindInteractive(ISlotReadSource source, int slotIndex, SlotAddr addr)
    {
        this.source = source;
        this.slotIndex = slotIndex;
        this.addr = addr;
        shown = false;
        Refresh();
    }

    public SlotAddr? Addr => addr;

    public void Refresh()
    {
        // Value read from the source; out-of-range / unbound renders empty.
        ushort id = 0;
        int amount = 0;
        if(source != null && slotIndex >= 0 && slotIndex < source.Capacity)
        {
            var slot = source.GetSlot(slotIndex);
            id = slot.itemId;
            amount = slot.amount;
        }
        bool empty = amount == 0;
        // Skip when the rendered state already matches: Refresh may be polled
        // every frame (furnace work tick) and Icon.SetItem re-renders a 3D
        // model into a RenderTexture, which must not run on unchanged slots.
        if(shown && shownItemId == id && shownAmount == (empty ? 0 : amount)) return;
        shown = true;
        shownItemId = id;
        shownAmount = empty ? 0 : amount;
        if(empty)
        {
            Icon.gameObject.SetActive(false);
            Text.text = "";
            return;
        }

        Icon.gameObject.SetActive(true);
        Icon.SetItem(id);
        Text.text = amount > 1 ? amount.ToString() : "";
    }

    private ISlotReadSource source;
    private int slotIndex;
    private SlotAddr? addr;

    // Managed by the UIManager drag session; the outline is drawn on the slot
    // background image so it never covers the icon.
    public void SetDragHighlight(DragHighlight state)
    {
        if(highlight == null)return;
        if(state == DragHighlight.None)
        {
            highlight.enabled = false;
            return;
        }
        highlight.enabled = true;
        highlight.effectColor = state == DragHighlight.Accept ? AcceptColor : RejectColor;
    }

    // Hover tracking (feeds UIManager.CurrentHoverSlotUI; tooltip later).
    // Unity stops routing enter/exit during a drag, so the drag session
    // updates the hover slot from its own per-frame raycast instead.
    public void OnPointerEnter(PointerEventData eventData)
    {
        UIManager.Instance.CurrentHoverSlotUI = this;
        hoverOverlay?.SetActive(true);
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        var ui = UIManager.Instance;
        if(ui != null && ui.CurrentHoverSlotUI == this)ui.CurrentHoverSlotUI = null;
        hoverOverlay?.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
        => UIManager.Instance.HandleSlotClicked(this, eventData);

    public void OnEndDrag(PointerEventData eventData)
        => UIManager.Instance.HandleDragEnd(this, eventData);
    public void OnBeginDrag(PointerEventData eventData)
        => UIManager.Instance.HandleDragBegin(this, eventData);
    // Unity only routes BeginDrag/EndDrag to the object it resolved as the
    // drag handler at press time, and that resolution looks for IDragHandler
    // (StandaloneInputModule: pointerDrag = GetEventHandler<IDragHandler>).
    // Hover tracking during the drag is polled by UIManager instead.
    public void OnDrag(PointerEventData eventData) { }

    private bool shown;              // false until the first Refresh after bind
    private ushort shownItemId;      // last rendered state (empty => amount 0)
    private int shownAmount;

}