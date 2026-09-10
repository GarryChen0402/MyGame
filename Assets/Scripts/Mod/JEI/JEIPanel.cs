using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// JEI overlay root (design §4.1: an overlay layer, never a SinglePanel - it
// must coexist with the open container panel for give feedback and, later,
// recipe fill / drag-delete). P1: a right-edge strip with the virtualized item
// grid, live search and cheat-mode give. Visibility follows the input stack
// (§6.8): shown only while a world panel is open, under a Ctrl+O master switch.
public class JEIPanel : MonoBehaviour, IScrollHandler
{
    private const float StripWidth = 116f;   // 2 columns x 50 pitch + padding
    private const float CellSize = 46f;
    private const float CellPitch = 50f;
    private const float TopPadding = 8f;
    private const float SearchHeight = 34f;
    private const int Columns = 2;

    private readonly JEIDataService data = new();
    private readonly System.Collections.Generic.List<JEICell> cells = new();
    private JEIIconCache iconCache;
    private GameObject content;
    private GameObject grid;
    private UITextWidget noResultText;
    private int visibleRows;
    private int scrollRow;
    private bool followEnabled = true;

    private void Awake()
    {
        // Host GO is the engine-created stretch root (CreateOverlayRoot): keep
        // it active at all times - the visibility switch lives on "Content",
        // a deactivated host would stop this Update and could never come back.
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
        iconCache.enabled = false;
    }

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
        // Polled even while hidden: in game state (no panel) the switch can
        // still be flipped, matching "the toggle is independent of panels".
        if(KeyBindingManager.Instance.WasPressed("jei:toggle"))followEnabled = !followEnabled;

        bool visible = followEnabled && IsWorldPanelOpen();
        if(content.activeSelf == visible)return;
        content.SetActive(visible);
        iconCache.enabled = visible;   // stop burning renders while hidden
        if(!visible)
        {
            // Panel closed under the pointer: no exit event will arrive, so
            // drop our own stale hover feed (the tooltip also guards this).
            var ui = UIManager.Instance;
            if(ui != null && ui.CurrentHoverInfo is JEICell)ui.CurrentHoverInfo = null;
            return;
        }
        scrollRow = 0;
        Rebind();
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
            cells[i].SetItem(index < items.Count ? items[index] : (ushort)0);
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
