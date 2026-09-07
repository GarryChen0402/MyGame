using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Dropdown selector widget in the same shape as the other widgets: self-built
// component + static factory, anchored to the parent's center, size decided by
// the caller. The field shows the current option (or a grey placeholder) and
// toggles a popup list on click. The popup (full-canvas invisible backdrop +
// option rows) is parented under the owning Canvas so it floats above every
// panel; it is rebuilt from the current options on each expand and destroyed
// on collapse. Expanding one dropdown collapses any other.
//
// Layout caveat: the popup is positioned from the field's world rect, so the
// field must already be laid out (sizeDelta set) when it expands, and the
// canvas chain is assumed to be unscaled (no CanvasScaler) - pages that scale
// themselves (rt.localScale != 1) would need a scale compensation here.
public class UIDropdownWidget : MonoBehaviour
{
    private readonly List<string> options = new();
    private Button fieldButton;
    private Image fieldBg;
    private TextMeshProUGUI label;
    private string placeholder = "Select...";
    private int selectedIndex = -1;

    public int ItemHeight = 26;
    public readonly UnityEvent<int> OnSelectionChanged = new();

    // Popup state: at most one dropdown is expanded at a time; the popup GO
    // live under the Canvas and must be cleaned up explicitly (a tab switch
    // destroys this page but not Canvas children).
    private bool expanded;
    private GameObject backdropGo;
    private GameObject listGo;
    private static UIDropdownWidget openDropdown;

    private void Awake()
    {
        fieldBg = gameObject.AddComponent<Image>();
        fieldBg.color = new Color(1f, 1f, 1f, 0.8f);
        fieldButton = gameObject.AddComponent<Button>();
        fieldButton.targetGraphic = fieldBg;
        fieldButton.onClick.AddListener(() => { if(expanded)Collapse(); else Expand(); });

        // Left-aligned label with room for the caret hint.
        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(transform, false);
        var labelRt = (RectTransform)labelGo.transform;
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(8, 0);
        labelRt.offsetMax = new Vector2(-8, 0);
        label = labelGo.AddComponent<TextMeshProUGUI>();
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.Left;
        label.fontSize = 18;

        // GO comes with a RectTransform from the factory (Awake-time transform
        // replacement is deferred by Unity, so never AddComponent one here).
        RectTransform rt = (RectTransform)transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);

        RefreshField();
    }

    public int SelectedIndex => selectedIndex;

    // Label of the selected option, or null while nothing is selected.
    public string SelectedLabel => selectedIndex >= 0 && selectedIndex < options.Count ? options[selectedIndex] : null;

    public void SetPlaceholder(string text)
    {
        placeholder = text;
        RefreshField();
    }

    public void SetOptions(IReadOnlyList<string> labels)
    {
        if(expanded)Collapse();
        options.Clear();
        options.AddRange(labels);
        if(selectedIndex >= options.Count)selectedIndex = -1;
        RefreshField();
    }

    // Programmatic selection also notifies listeners.
    public void SetSelectedIndex(int index)
    {
        selectedIndex = index;
        RefreshField();
        OnSelectionChanged.Invoke(selectedIndex);
    }

    public void Collapse()
    {
        if(!expanded)return;
        expanded = false;
        if(openDropdown == this)openDropdown = null;
        if(backdropGo != null){ Destroy(backdropGo); backdropGo = null; }
        if(listGo != null){ Destroy(listGo); listGo = null; }
    }

    private void OnDestroy() => Collapse();

    private void RefreshField()
    {
        if(label == null)return;
        bool hasSelection = selectedIndex >= 0 && selectedIndex < options.Count;
        // No arrow glyph: LiberationSans SDF (and fallbacks) lacks U+25BE, and
        // TMP then warns every frame while rendering the replacement box.
        label.text = hasSelection ? options[selectedIndex] : placeholder;
        label.color = hasSelection ? Color.black : new Color(0.45f, 0.45f, 0.45f, 1f);
    }

    private void Expand()
    {
        if(expanded || options.Count == 0)return;
        var canvas = GetComponentInParent<Canvas>();
        if(canvas == null)return;   // not inside a Canvas: nothing sensible to pop up
        if(openDropdown != null && openDropdown != this)openDropdown.Collapse();
        openDropdown = this;
        expanded = true;

        var canvasRt = (RectTransform)canvas.transform;

        // Invisible full-canvas backdrop absorbs every click outside the list
        // (clicking anywhere else collapses the popup).
        backdropGo = new GameObject("Dropdown Backdrop", typeof(RectTransform));
        backdropGo.transform.SetParent(canvas.transform, false);
        var backdropRt = (RectTransform)backdropGo.transform;
        backdropRt.anchorMin = Vector2.zero;
        backdropRt.anchorMax = Vector2.one;
        backdropRt.offsetMin = Vector2.zero;
        backdropRt.offsetMax = Vector2.zero;
        var backdropImage = backdropGo.AddComponent<Image>();
        backdropImage.color = new Color(0f, 0f, 0f, 0f);
        var backdropButton = backdropGo.AddComponent<Button>();
        backdropButton.targetGraphic = backdropImage;
        backdropButton.transition = Selectable.Transition.None;
        backdropButton.onClick.AddListener(Collapse);

        // Option list: positioned below the field, one row per option.
        var rect = (RectTransform)transform;
        float width = rect.rect.width;
        int count = options.Count;
        float height = count * ItemHeight;

        listGo = new GameObject("Dropdown List", typeof(RectTransform));
        listGo.transform.SetParent(canvas.transform, false);
        var listRt = (RectTransform)listGo.transform;
        listRt.anchorMin = listRt.anchorMax = new Vector2(0.5f, 0.5f);
        listRt.pivot = new Vector2(0f, 1f);   // top-left anchored: list grows downward
        listRt.sizeDelta = new Vector2(width, height);
        Vector3 worldBottomCenter = rect.TransformPoint(new Vector3(0f, -rect.rect.height * 0.5f, 0f));
        Vector3 local = canvasRt.InverseTransformPoint(worldBottomCenter);
        listRt.anchoredPosition = new Vector2(local.x - width * 0.5f, local.y);

        // Opaque list backdrop over the row gaps, below the option rows.
        var listImage = listGo.AddComponent<Image>();
        listImage.color = new Color(0.13f, 0.13f, 0.13f, 0.95f);

        for(int i = 0; i < count; i++)
        {
            var itemGo = UIButtonWidget.CreateNewButton(options[i], null, listGo);
            var itemRt = (RectTransform)itemGo.transform;
            itemRt.anchorMin = new Vector2(0f, 1f);
            itemRt.anchorMax = new Vector2(0f, 1f);
            itemRt.pivot = new Vector2(0.5f, 0.5f);
            itemRt.sizeDelta = new Vector2(width, ItemHeight);
            itemRt.anchoredPosition = new Vector2(width * 0.5f, -ItemHeight * (i + 0.5f));
            var itemButton = itemGo.GetComponent<UIButtonWidget>();
            if(i == selectedIndex)itemButton.SetColor(new Color(0.65f, 0.8f, 1f, 1f));
            int captured = i;
            itemButton.OnClick.AddListener(() => SelectAt(captured));
        }
    }

    private void SelectAt(int index)
    {
        selectedIndex = index;
        Collapse();
        RefreshField();
        OnSelectionChanged.Invoke(selectedIndex);
    }

    public static GameObject CreateNewDropdown(string placeholder = "Select...", GameObject parent = null)
    {
        GameObject dropGo = new GameObject("Dropdown", typeof(RectTransform));
        var widget = dropGo.AddComponent<UIDropdownWidget>();
        widget.SetPlaceholder(placeholder);
        if(parent != null)dropGo.transform.SetParent(parent.transform, false);
        return dropGo;
    }
}
