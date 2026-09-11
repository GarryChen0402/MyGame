using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One cell of the JEI item grid: cached icon + hover tint + amount badge +
// click-to-give. Deliberately not a SlotUI - it has no mirror /
// SlotAddr, and it settles through the give command directly. Feeds the general
// tooltip via IHoverItemSource, exactly like SlotUI does. The recipe page
// reuses the same cell with clickable:false (display-only, design decision 1).
public class JEICell : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
    IHoverItemSource
{
    // Item id 0 is a real item (minecraft:stone registers first), so empty
    // cells need a sentinel outside the reachable id range.
    public const ushort NoItem = ushort.MaxValue;

    // 8px per side: the 64x64 icon render target displays 1:1 in an 80px cell.
    private const float IconInset = 8f;
    private static readonly Color HoverColor = new(1f, 1f, 1f, 0.3f);

    private CanvasGroup group;
    private RawImage icon;
    private TextMeshProUGUI amountText;
    private GameObject hoverOverlay;
    private JEIIconCache iconCache;
    private ushort itemId = NoItem;   // NoItem = empty cell (past the end of the filtered list)
    private int amount = 1;
    private bool clickable = true;
    private bool shown = true;

    public void Init(JEIIconCache cache, float size, bool clickable = true)
    {
        iconCache = cache;
        this.clickable = clickable;
        ((RectTransform)transform).sizeDelta = new Vector2(size, size);

        // Pool visibility + hit switch. A CanvasGroup instead of SetActive:
        // deactivating a cell under the pointer skips its OnPointerExit, and
        // re-activation routing is unreliable. alpha 0 + blocksRaycasts false
        // hides every child graphic (icon, badge, hover tint) while
        // the GO stays active.
        group = gameObject.AddComponent<CanvasGroup>();

        // Invisible hit surface (clear color, no sprite): hover / click / wheel
        // land on the cell body (wheel events bubble up to the panel's
        // IScrollHandler); the cell itself draws no backdrop.
        var hit = gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;

        var iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(transform, false);
        var iconRt = (RectTransform)iconGo.transform;
        iconRt.anchorMin = Vector2.zero;
        iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = new Vector2(IconInset, IconInset);
        iconRt.offsetMax = new Vector2(-IconInset, -IconInset);
        icon = iconGo.AddComponent<RawImage>();
        icon.raycastTarget = false;
        icon.enabled = false;

        hoverOverlay = new GameObject("Hover", typeof(RectTransform));
        hoverOverlay.transform.SetParent(transform, false);
        var hoverRt = (RectTransform)hoverOverlay.transform;
        hoverRt.anchorMin = Vector2.zero;
        hoverRt.anchorMax = Vector2.one;
        hoverRt.offsetMin = Vector2.zero;
        hoverRt.offsetMax = Vector2.zero;
        var hoverImage = hoverOverlay.AddComponent<Image>();
        hoverImage.color = HoverColor;
        hoverImage.raycastTarget = false;
        hoverOverlay.SetActive(false);

        // Stack badge, created last so it renders above icon and hover tint.
        var amountGo = new GameObject("Amount", typeof(RectTransform));
        amountGo.transform.SetParent(transform, false);
        amountText = amountGo.AddComponent<TextMeshProUGUI>();
        amountText.alignment = TextAlignmentOptions.BottomRight;
        amountText.fontSize = 16f;
        amountText.fontStyle = FontStyles.Bold;
        amountText.color = Color.black;
        amountText.raycastTarget = false;
        // TMP's OnEnable overwrites sizeDelta, so set the rect after AddComponent.
        var amountRt = (RectTransform)amountGo.transform;
        amountRt.anchorMin = amountRt.anchorMax = new Vector2(1f, 0f);
        amountRt.pivot = new Vector2(1f, 0f);
        amountRt.sizeDelta = new Vector2(size - 6f, 18f);
        amountRt.anchoredPosition = new Vector2(-3f, 3f);
    }

    // Virtual grid rebind: same id + amount short-circuits (the icon RT stays
    // bound). amount > 1 shows the badge (recipe view inputs/outputs).
    public void SetItem(ushort id, int amount = 1)
    {
        if(id == NoItem)amount = 1;   // normalize empty: repeated SetItem(NoItem) still short-circuits
        if(itemId == id && this.amount == amount)return;
        itemId = id;
        this.amount = amount;
        icon.enabled = shown && id != NoItem;
        if(id == NoItem)icon.texture = null;
        amountText.text = amount > 1 ? amount.ToString() : "";
    }

    // Icons trickle in from the cache's frame budget; poll until the RT lands.
    // Hidden pool cells must not enqueue renders: bail before touching the cache.
    private void Update()
    {
        if(!shown || itemId == NoItem)return;
        var rt = iconCache.Get(itemId);
        if(rt != null && icon.texture != rt)icon.texture = rt;
    }

    // Pool show/hide (recipe page). See Init for why the GO stays active.
    public void SetShown(bool value)
    {
        if(shown == value)return;
        shown = value;
        group.alpha = value ? 1f : 0f;
        group.blocksRaycasts = value;
        icon.enabled = value && itemId != NoItem;
        if(!value)ForceUnhover();
    }

    // Rebind/hide can run while the pointer still rests on this cell, in which
    // case no exit event will arrive: drop the stale hover feed ourselves.
    public void ForceUnhover()
    {
        var ui = UIManager.Instance;
        if(ui != null && ui.CurrentHoverInfo == this)ui.CurrentHoverInfo = null;
        if(hoverOverlay != null)hoverOverlay.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Display-only cells (recipe page): click-through navigation is P3, so
        // until then a click here must do nothing at all.
        if(!clickable || itemId == NoItem)return;
        if(!ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(itemId, out var def))return;
        // Cheat-mode semantics (decision D2): left = one stack, right = one item.
        int amount = eventData.button == PointerEventData.InputButton.Right ? 1 : def.MaxStack;
        ContainerCommandProcessor.Instance.GiveItem(def.FullName, amount);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        var ui = UIManager.Instance;
        if(ui != null && itemId != NoItem)ui.CurrentHoverInfo = this;
        if(itemId != NoItem)hoverOverlay.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        var ui = UIManager.Instance;
        if(ui != null && ui.CurrentHoverInfo == this)ui.CurrentHoverInfo = null;
        hoverOverlay.SetActive(false);
    }

    public bool TryGetHoverItemId(out ushort id)
    {
        id = itemId;
        return itemId != NoItem;
    }
}
