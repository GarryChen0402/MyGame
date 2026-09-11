using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// JEI panel (design §4.1: a coexistence layer, never a SinglePanel - it must
// stay on screen next to the open container panel for give feedback and,
// later, recipe fill / drag-delete). P1: a right-edge strip with the
// virtualized item grid, live search and cheat-mode give. Visibility follows
// the input stack (§6.8): shown only while a world panel is open, under a
// Ctrl+O master switch. P7: R/U and the toggle arrive via the UIManager action
// ring - this class only exposes ToggleFollow / OnRecipeKey as the registered
// callbacks. P8: a standard UIBehaviour riding the UI pipeline as UIKind
// Overlay (host GO parented under the UIManager Overlay root). D10: the recipe
// view is its own SinglePanel ("jei:recipe") - OnRecipeKey resolves the hovered
// item and drives it through close-then-open; the strip itself stays alongside
// (the recipe screen pushes the ui handler, keeping the follow condition true).
public class JEIPanel : UIBehavior, IScrollHandler
{
    private const float StripWidth = 520f;   // 6 columns x 84 pitch + padding
    private const float CellSize = 80f;
    private const float CellPitch = 84f;
    private const float TopPadding = 8f;
    private const float SearchHeight = 34f;
    private const int Columns = 6;

    private readonly JEIDataService data = new();
    private readonly System.Collections.Generic.List<JEICell> cells = new();
    private JEIIconCache iconCache;
    private GameObject content;
    private GameObject grid;
    private UITextWidget noResultText;
    private int visibleRows;
    private int scrollRow;
    private bool followEnabled = true;

    // Host GO is the factory-built stretch root (P8): keep it active at all
    // times - the visibility switch lives on "Content", a deactivated host
    // would stop this Update and could never come back.
    private void Awake()
    {
        iconCache = gameObject.AddComponent<JEIIconCache>();

        content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(transform, false);
        var contentRt = (RectTransform)content.transform;
        contentRt.anchorMin = new Vector2(1f, 0f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(1f, 0.5f);
        contentRt.sizeDelta = new Vector2(StripWidth, 0f);
        contentRt.anchoredPosition = Vector2.zero;

        // Semi-transparent backdrop (decision D5): readable over the game yet
        // not hiding what is behind it. Raycast target: the wheel events of
        // every cell bubble up through it to this panel's IScrollHandler.
        var backdrop = new GameObject("Backdrop", typeof(RectTransform));
        backdrop.transform.SetParent(content.transform, false);
        var backdropRt = (RectTransform)backdrop.transform;
        backdropRt.anchorMin = Vector2.zero;
        backdropRt.anchorMax = Vector2.one;
        backdropRt.offsetMin = Vector2.zero;
        backdropRt.offsetMax = Vector2.zero;
        var backdropImage = backdrop.AddComponent<Image>();
        backdropImage.color = new Color(0.08f, 0.09f, 0.11f, 0.5f);

        grid = new GameObject("Grid", typeof(RectTransform));
        grid.transform.SetParent(content.transform, false);
        var gridRt = (RectTransform)grid.transform;
        gridRt.anchorMin = gridRt.anchorMax = new Vector2(0f, 1f);
        gridRt.pivot = new Vector2(0f, 1f);
        gridRt.anchoredPosition = Vector2.zero;

        BuildCells();
        BuildSearchBox();

        noResultText = UITextWidget.CreateNewText("No items", content).GetComponent<UITextWidget>();
        noResultText.gameObject.name = "No Items";
        var noResultRt = (RectTransform)noResultText.transform;
        noResultRt.sizeDelta = new Vector2(StripWidth, 24f);
        noResultRt.anchoredPosition = new Vector2(-StripWidth * 0.5f, -(TopPadding + 60f));
        noResultText.SetFontSize(16f);
        noResultText.SetColor(new Color(0.9f, 0.9f, 0.9f, 0.9f));
        noResultText.gameObject.SetActive(false);

        content.SetActive(false);
    }

    // The recipe screen (D10) shares this cache: one RT per item serves both
    // the strip and the recipe panel. The cache is never disabled as a whole
    // anymore - requests only ever come from active cells, which stop ticking
    // the moment their own root hides.
    public JEIIconCache IconCache => iconCache;

    // Virtual grid: only the visible rows (plus one buffer row) exist as cells;
    // scrolling re-binds them to shifted item ids, so the cell count is
    // independent of the item count (R6: fine into the thousands).
    private void BuildCells()
    {
        visibleRows = Mathf.Max(1, Mathf.FloorToInt((Screen.height - SearchHeight - TopPadding) / CellPitch));
        int cellCount = (visibleRows + 1) * Columns;
        for(int i = 0; i < cellCount; i++)
        {
            var go = new GameObject($"Cell {i}", typeof(RectTransform));
            go.transform.SetParent(grid.transform, false);
            var cellRt = (RectTransform)go.transform;
            cellRt.anchorMin = cellRt.anchorMax = new Vector2(0f, 1f);
            cellRt.pivot = new Vector2(0f, 1f);
            int column = i % Columns;
            int row = i / Columns;
            cellRt.anchoredPosition = new Vector2(TopPadding + column * CellPitch, -(TopPadding + row * CellPitch));
            var cell = go.AddComponent<JEICell>();
            cell.Init(iconCache, CellSize);
            cells.Add(cell);
        }
    }

    private void BuildSearchBox()
    {
        var searchGo = UITextInputWidget.CreateNewTextInput("", "Search", content);
        searchGo.name = "Search";
        var searchRt = (RectTransform)searchGo.transform;
        searchRt.anchorMin = new Vector2(0f, 0f);
        searchRt.anchorMax = new Vector2(1f, 0f);
        searchRt.pivot = new Vector2(0.5f, 0f);
        searchRt.sizeDelta = new Vector2(-12f, SearchHeight - 10f);
        searchRt.anchoredPosition = new Vector2(0f, 6f);
        // Live filter: the widget's OnChange fires per keystroke. Note the
        // keybinding layer silences all actions while the field is focused
        // (KeyBindingManager), so typing 'e' cannot close the panel.
        searchGo.GetComponent<UITextInputWidget>().OnValueChanged.AddListener(OnSearchChanged);
    }

    private void Update()
    {
        bool visible = followEnabled && IsWorldPanelOpen();
        if(content.activeSelf != visible)
        {
            content.SetActive(visible);
            if(!visible)
            {
                // Strip cells deactivate under the pointer without an exit
                // event, so drop that now-stale hover feed (the tooltip also
                // guards this). Recipe cells live on the recipe panel's own
                // root and keep hovering - only clear a cell this flip hid.
                var ui = UIManager.Instance;
                if(ui != null && ui.CurrentHoverInfo is JEICell cell && !cell.gameObject.activeInHierarchy)
                    ui.CurrentHoverInfo = null;
                return;
            }
            scrollRow = 0;
            Rebind();
        }
    }

    // Ctrl+O "follow panels" master switch (P7: no longer self-polled - the
    // UIManager action ring routes the jei:toggle ui_action here; flippable in
    // any context, the visibility recompute happens in Update).
    public void ToggleFollow()
    {
        followEnabled = !followEnabled;
    }

    // R/U semantics (design §6.4, D10), routed from the UIManager action ring
    // (P7): a hovered item opens its recipe screen ("jei:recipe", an
    // independent SinglePanel). Opening is close-then-open - whatever UI is
    // open (a world panel or the previous recipe screen) closes first and is
    // never restored on exit. No hover target is a no-op. The hover lookup
    // stays on the general CurrentHoverInfo feed - panel slots, strip cells
    // and recipe cells alike, so chained navigation works.
    public void OnRecipeKey(bool recipesNotUses)
    {
        // The ring polls regardless of JEI visibility (its slot gate only
        // concerns the hovered slot); keep the old scope: a hidden JEI ignores
        // R/U. Covers "follow off" and "no panel open" alike. With the recipe
        // screen open its handler keeps this condition true.
        if(!followEnabled || !IsWorldPanelOpen())return;
        var ui = UIManager.Instance;
        if(ui == null || ui.DragActive)return;   // hover feed is stale mid-drag
        IHoverItemSource source = HoverSource.Validate(ui.CurrentHoverInfo);
        if(source == null || !source.TryGetHoverItemId(out ushort itemId))return;
        var recipes = recipesNotUses ? data.GetRecipesMaking(itemId) : data.GetRecipesUsing(itemId);
        ui.CloseUI();   // D10: the recipe screen replaces whatever was open
        ui.OpenUI("jei:recipe", recipes);
    }

    // Follow condition (decision D1): a world panel is open. The menu pushes
    // its own handler and is excluded by construction; game state runs the
    // player handler. Compared by name - FullName allocates per access.
    private static bool IsWorldPanelOpen()
    {
        var handlers = InputHandlerManager.Instance;
        return handlers != null
            && handlers.CurrentInputHandler != null
            && handlers.CurrentInputHandler.name == "ui_input_handler";
    }

    private void OnSearchChanged(string query)
    {
        data.ApplyFilter(query);
        scrollRow = 0;
        Rebind();
    }

    private void Rebind()
    {
        data.EnsureBuilt();
        var items = data.Filtered;
        int first = scrollRow * Columns;
        for(int i = 0; i < cells.Count; i++)
        {
            int index = first + i;
            cells[i].SetItem(index < items.Count ? items[index] : JEICell.NoItem);
        }
        noResultText.gameObject.SetActive(items.Count == 0);
    }

    // Wheel events bubble up from the hovered cell to here (the panel root of
    // the strip). Row-stepped scrolling; no Input.GetAxis, so the game-state
    // wheel (hotbar cycling) is untouched by design (R4).
    public void OnScroll(PointerEventData eventData)
    {
        if(!content.activeSelf)return;
        ScrollBy(eventData.scrollDelta.y > 0f ? -1 : 1);
    }

    private void ScrollBy(int rows)
    {
        int totalRows = Mathf.CeilToInt(data.Filtered.Count / (float)Columns);
        int maxRow = Mathf.Max(0, totalRows - visibleRows);
        int next = Mathf.Clamp(scrollRow + rows, 0, maxRow);
        if(next == scrollRow)return;   // already at an end: no rebind churn
        scrollRow = next;
        Rebind();
    }
}
