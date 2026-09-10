using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One cell of the JEI item grid: slot backdrop + cached icon + hover tint +
// click-to-give. Deliberately not a SlotUI - it has no mirror / SlotAddr, and
// it settles through the give command directly. Feeds the general tooltip via
// IHoverItemSource, exactly like SlotUI does.
public class JEICell : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
    IHoverItemSource
{
    private const float IconInset = 5f;
    private static readonly Color HoverColor = new(1f, 1f, 1f, 0.3f);
    private static Sprite slotSprite;

    private RawImage icon;
    private GameObject hoverOverlay;
    private JEIIconCache iconCache;
    private ushort itemId;   // 0 = empty cell (past the end of the filtered list)

    private static Sprite SlotSprite()
    {
        if(slotSprite == null)slotSprite = Resources.Load<Sprite>("Textures/UI/slot");
        return slotSprite;
    }

    public void Init(JEIIconCache cache, float size)
    {
        iconCache = cache;
        ((RectTransform)transform).sizeDelta = new Vector2(size, size);

        var bg = gameObject.AddComponent<Image>();
        bg.sprite = SlotSprite();
        // Raycast target on the cell body: hover / click / wheel all land here
        // (wheel events bubble up to the panel's IScrollHandler).
        bg.raycastTarget = true;

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
    }

    // Virtual grid rebind: same id short-circuits (the icon RT stays bound).
    public void SetItem(ushort id)
    {
        if(itemId == id)return;
        itemId = id;
        icon.enabled = id != 0;
        if(id == 0)icon.texture = null;
    }

    // Icons trickle in from the cache's frame budget; poll until the RT lands.
    private void Update()
    {
        if(itemId == 0)return;
        var rt = iconCache.Get(itemId);
        if(rt != null && icon.texture != rt)icon.texture = rt;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if(itemId == 0)return;
        if(!ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(itemId, out var def))return;
        // Cheat-mode semantics (decision D2): left = one stack, right = one item.
        int amount = eventData.button == PointerEventData.InputButton.Right ? 1 : def.MaxStack;
        ContainerCommandProcessor.Instance.GiveItem(def.FullName, amount);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        var ui = UIManager.Instance;
        if(ui != null && itemId != 0)ui.CurrentHoverInfo = this;
        if(itemId != 0)hoverOverlay.SetActive(true);
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
        return itemId != 0;
    }
}
