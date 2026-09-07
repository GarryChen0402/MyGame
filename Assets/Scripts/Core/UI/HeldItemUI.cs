using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Cursor-held stack renderer (方案 §5.3): SlotUI-style icon + amount label
// that follows the mouse while the player's cursor stack is non-empty. Tooltip
// kind: it lives on the topmost TooltipRoot so it overlays every panel, but
// having no Graphic itself it never swallows clicks.
public class HeldItemUI : UIBehavior
{
    private RectTransform rt;
    private ItemIconRenderer Icon;
    private TextMeshProUGUI Text;
    private int cachedVersion = -1;
    private ushort cachedItemId;
    private int cachedAmount;

    private void Awake()
    {
        rt = gameObject.AddComponent<RectTransform>();
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;   // zero-size anchor point parked on the cursor

        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(transform, false);
        var iconRt = iconGo.AddComponent<RectTransform>();
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta = new Vector2(64, 64);   // icon center = cursor position
        var rawImage = iconGo.AddComponent<RawImage>();
        rawImage.raycastTarget = false;   // never swallow slot clicks below
        Icon = iconGo.AddComponent<ItemIconRenderer>();

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(iconGo.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = textRt.anchorMax = new Vector2(0.5f, 0.5f);
        textRt.anchoredPosition = new Vector2(-2, -25);
        Text = textGo.AddComponent<TextMeshProUGUI>();
        Text.raycastTarget = false;
        Text.alignment = TextAlignmentOptions.BottomRight;
        Text.fontSize = 20;
        Text.color = Color.black;
        Text.fontStyle = FontStyles.Bold;

        // TMP's OnEnable overwrites sizeDelta, so set it after AddComponent.
        textRt.sizeDelta = new Vector2(100, textRt.sizeDelta.y);
    }

    // Only the icon child is toggled, not this GO: SetActive would stop
    // Update, so an empty HeldItemUI could never wake up to notice a pickup.
    private void Update()
    {
        // Phase C: reads the held mirror (player cursor stack value copy).
        // Mirror sync runs before this Update every render frame, so the
        // version gate below renders exactly on the frame the change lands.
        var held = MirrorSync.Instance == null ? null : MirrorSync.Instance.PlayerHeldMirror;
        bool empty = held == null || held.Content.IsEmpty;
        bool iconActive = Icon.gameObject.activeSelf;
        if(iconActive != !empty)Icon.gameObject.SetActive(!empty);
        if(empty)return;

        // Render only when the mirrored content changed (version, then values
        // as a safety net): every pickup / drop / merge / swap bumps the
        // version, so a first held render can never be skipped.
        if(cachedVersion != held.Version
           || cachedItemId != held.Content.itemId
           || cachedAmount != held.Content.amount)
        {
            cachedVersion = held.Version;
            cachedItemId = held.Content.itemId;
            cachedAmount = held.Content.amount;
            Icon.SetItem(held.Content.itemId);
            Text.text = held.Content.amount > 1 ? held.Content.amount.ToString() : "";
        }

        var parentRt = (RectTransform)transform.parent;
        var canvas = GetComponentInParent<Canvas>();
        Vector2 local;
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(
               parentRt, Input.mousePosition, canvas == null ? null : canvas.worldCamera, out local))
            rt.localPosition = local;
    }

    public static UIDefinition heldItemUIDefinition = new()
    {
        modId = "minecraft",
        name = "held_item",
        Kind = UIKind.Tooltip,
        OpenWithPlayerInventory = false,
        Factory = () =>
        {
            var go = new GameObject("Held Item");
            go.AddComponent<HeldItemUI>();
            return go;
        }
    };
}
