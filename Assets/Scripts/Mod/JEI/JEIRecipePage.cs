using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Recipe screen of the JEI mod (design §6.4; standalone form per D10, 2026-09-11):
// a standard SinglePanel registered as "jei:recipe" by JEIMod and opened from
// the R/U keys - open closes the previously open UI first (close-then-open
// lives in JEIPanel.OnRecipeKey) and closing returns to the game state, never
// to the old panel. It pushes the ui input handler, so the strip's follow
// condition stays true and the item list remains usable beside it.
// One recipe per page, three layouts by RecipeKind: shaped draws the Shape
// grid (spaces become empty slots), shapeless tiles Inputs in 3 columns,
// processing is a single input row with an arrow, cook seconds and the output.
// Cells are display-only (click-through navigation is P3) and pooled - never
// destroyed. Own IScrollHandler: EventSystem routes a wheel to the first
// ancestor handler, so scrolling here pages the view and never reaches the
// strip's list scrolling.
public class JEIRecipePage : UIBehavior, IScrollHandler
{
    private const float PanelW = 560f;
    private const float PanelH = 430f;
    private const float CellSize = 80f;
    private const float CellPitch = 84f;
    private const float RowY0 = 62f;
    private const float ArrowW = 40f;
    private const float Gap = 6f;
    private const int MaxGrid = 3;      // the game's crafting grid is 3x3
    private const int InputSlots = 9;
    private const int OutputSlots = 3;

    private readonly List<JEICell> cells = new();
    private JEIIconCache iconCache;
    private UIButtonWidget backButton;
    private UIButtonWidget prevButton;
    private UIButtonWidget nextButton;
    private UITextWidget titleText;
    private UITextWidget pageText;
    private UITextWidget arrowText;
    private UITextWidget timeText;
    private UITextWidget emptyText;

    private IReadOnlyList<RecipeContent> recipes;
    private int index;
    private bool warnedClamp;

    public void Init(JEIIconCache cache)
    {
        iconCache = cache;

        var rt = (RectTransform)transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);   // standalone centered screen
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(PanelW, PanelH);
        rt.anchoredPosition = Vector2.zero;

        // Backdrop. Unlike the tooltip's, this one KEEPS its raycast target:
        // the page must catch wheel events for paging, and shielding the panel
        // behind it from clicks is the point of an overlay.
        UIWidgetBackground.CreateNewBackground().transform.SetParent(transform, false);

        backButton = MakeButton("Back", 10f, 5f, 52f);
        backButton.OnClick.AddListener(CloseSelf);
        titleText = MakeText(70f, 5f, 180f, 22f, 16f, TextAlignmentOptions.Left);
        prevButton = MakeButton("<", 10f, 30f, 24f, 20f);
        prevButton.OnClick.AddListener(() => ChangePage(-1));
        nextButton = MakeButton(">", PanelW - 34f, 30f, 24f, 20f);
        nextButton.OnClick.AddListener(() => ChangePage(1));
        pageText = MakeText(40f, 30f, PanelW - 80f, 20f, 14f, TextAlignmentOptions.Center);
        // "->" instead of a real arrow: the UI font lacks arrow glyphs and TMP
        // would warn every frame drawing the replacement box (precedent:
        // UIDropdownWidget).
        arrowText = MakeText(0f, 0f, ArrowW, 20f, 16f, TextAlignmentOptions.Center);
        arrowText.SetText("->");
        timeText = MakeText(0f, 0f, 50f, 18f, 14f, TextAlignmentOptions.Center);
        emptyText = MakeText(50f, 150f, PanelW - 100f, 24f, 16f, TextAlignmentOptions.Center);
        emptyText.SetText("No Recipes");
        emptyText.SetColor(new Color(0.3f, 0.3f, 0.3f, 1f));

        for(int i = 0; i < InputSlots + OutputSlots; i++)
        {
            var go = new GameObject($"Recipe Cell {i}", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var cellRt = (RectTransform)go.transform;
            cellRt.anchorMin = cellRt.anchorMax = new Vector2(0f, 1f);
            cellRt.pivot = new Vector2(0f, 1f);
            var cell = go.AddComponent<JEICell>();
            cell.Init(iconCache, CellSize, clickable: false);
            cells.Add(cell);
        }

        HideInputsFrom(0);
        HideOutputsFrom(0);
        arrowText.gameObject.SetActive(false);
        timeText.gameObject.SetActive(false);
        emptyText.gameObject.SetActive(false);
        SetPageRowActive(false);
        gameObject.SetActive(false);
    }

    // UIManager entry (OpenUI data): the caller resolves the list from the
    // hovered item (JEIPanel.OnRecipeKey); every open resets to the first page.
    public override void SetData(object data)
    {
        recipes = data as IReadOnlyList<RecipeContent>;
        index = 0;
        LayoutPage();
    }

    // Back leaves the recipe screen: the close command tears it down and the
    // game state resumes - the previously open panel is not restored (D10).
    private void CloseSelf()
    {
        UIManager.Instance?.CloseUI();
    }

    public void OnScroll(PointerEventData eventData)
    {
        ChangePage(eventData.scrollDelta.y > 0f ? -1 : 1);
    }

    private void ChangePage(int delta)
    {
        if(recipes == null || recipes.Count == 0)return;
        int next = Mathf.Clamp(index + delta, 0, recipes.Count - 1);
        if(next == index)return;
        index = next;
        LayoutPage();
    }

    private void LayoutPage()
    {
        // Rebind runs under a possibly stationary pointer: drop every hover
        // feed before the cells change what they show.
        foreach(var cell in cells)cell.ForceUnhover();

        bool has = recipes != null && recipes.Count > 0;
        emptyText.gameObject.SetActive(!has);
        SetPageRowActive(has);
        arrowText.gameObject.SetActive(has);
        timeText.gameObject.SetActive(false);
        if(!has)
        {
            HideInputsFrom(0);
            HideOutputsFrom(0);
            titleText.SetText("No Recipes");
            return;
        }

        var recipe = recipes[index];
        titleText.SetText(TitleFor(recipe.Kind));
        pageText.SetText($"{index + 1} / {recipes.Count}");
        prevButton.SetInteractable(index > 0);
        nextButton.SetInteractable(index < recipes.Count - 1);

        int usedInputs = recipe.Kind switch
        {
            RecipeKind.Shaped => LayoutShaped(recipe),
            RecipeKind.Shapeless => LayoutLoose(recipe),
            _ => LayoutProcessing(recipe)
        };
        HideInputsFrom(usedInputs);
    }

    // Shape grid, spaces = empty slots; returns the input cells used.
    private int LayoutShaped(RecipeContent recipe)
    {
        if(recipe.Shape.Length > MaxGrid)WarnClampOnce();
        int rows = Mathf.Min(MaxGrid, recipe.Shape.Length);
        int cols = 0;
        for(int r = 0; r < rows; r++)cols = Mathf.Max(cols, recipe.Shape[r].Length);
        if(cols > MaxGrid)WarnClampOnce();
        cols = Mathf.Min(MaxGrid, cols);
        float gridW = GridWidth(cols);
        float startX = BlockStartX(gridW);
        for(int r = 0; r < rows; r++)
        {
            string row = recipe.Shape[r];
            for(int c = 0; c < cols; c++)
            {
                ushort id = JEICell.NoItem;
                if(c < row.Length && row[c] != ' ' && recipe.ShapeKeys != null
                   && recipe.ShapeKeys.TryGetValue(row[c], out string itemName))
                    TryResolve(itemName, out id);
                Place(r * cols + c, startX + c * CellPitch, RowY0 + r * CellPitch, id, 1);
            }
        }
        float rowCenterY = RowCenterY(rows);
        PlaceArrow(startX, gridW, rowCenterY);
        PlaceOutputs(recipe, startX, gridW, rowCenterY);
        return rows * cols;
    }

    // Shapeless: inputs tiled in 3-column rows, left to right.
    private int LayoutLoose(RecipeContent recipe)
    {
        int count = recipe.Inputs == null ? 0 : recipe.Inputs.Count;
        if(count > InputSlots)WarnClampOnce();
        count = Mathf.Min(InputSlots, count);
        int cols = Mathf.Min(MaxGrid, Mathf.Max(1, count));
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)cols));
        float gridW = GridWidth(cols);
        float startX = BlockStartX(gridW);
        for(int i = 0; i < count; i++)
        {
            ushort id = JEICell.NoItem;
            TryResolve(recipe.Inputs[i].itemId, out id);
            Place(i, startX + (i % cols) * CellPitch, RowY0 + (i / cols) * CellPitch, id, recipe.Inputs[i].amount);
        }
        float rowCenterY = RowCenterY(rows);
        PlaceArrow(startX, gridW, rowCenterY);
        PlaceOutputs(recipe, startX, gridW, rowCenterY);
        return count;
    }

    // Processing: one input row, arrow + cook seconds, output.
    private int LayoutProcessing(RecipeContent recipe)
    {
        int count = recipe.Inputs == null ? 0 : recipe.Inputs.Count;
        if(count > MaxGrid)WarnClampOnce();
        count = Mathf.Min(MaxGrid, count);
        float gridW = GridWidth(Mathf.Max(1, count));
        float startX = BlockStartX(gridW);
        for(int i = 0; i < count; i++)
        {
            ushort id = JEICell.NoItem;
            TryResolve(recipe.Inputs[i].itemId, out id);
            Place(i, startX + i * CellPitch, RowY0, id, recipe.Inputs[i].amount);
        }
        float rowCenterY = RowCenterY(1);
        PlaceArrow(startX, gridW, rowCenterY);
        float arrowX = startX + gridW + Gap;
        timeText.SetText((recipe.ProcessingTickTime / 20f).ToString("0.#", CultureInfo.InvariantCulture) + "s");
        SetRect(timeText.gameObject, arrowX + ArrowW * 0.5f - 25f, rowCenterY + 8f, 50f, 18f);
        timeText.gameObject.SetActive(true);
        PlaceOutputs(recipe, startX, gridW, rowCenterY);
        return count;
    }

    // Output column on the right of the block, vertically centered on it.
    private void PlaceOutputs(RecipeContent recipe, float startX, float gridW, float rowCenterY)
    {
        int count = recipe.Outputs == null ? 0 : Mathf.Min(OutputSlots, recipe.Outputs.Count);
        float outputX = startX + gridW + Gap + ArrowW + Gap;
        float top = rowCenterY - CellSize * 0.5f;
        for(int i = 0; i < count; i++)
        {
            ushort id = JEICell.NoItem;
            TryResolve(recipe.Outputs[i].itemId, out id);
            Place(InputSlots + i, outputX, top + i * CellPitch, id, recipe.Outputs[i].amount);
        }
        for(int i = count; i < OutputSlots; i++)cells[InputSlots + i].SetShown(false);
    }

    private void PlaceArrow(float startX, float gridW, float rowCenterY)
    {
        SetRect(arrowText.gameObject, startX + gridW + Gap, rowCenterY - 10f, ArrowW, 20f);
    }

    private void Place(int slot, float x, float yTop, ushort id, int amount)
    {
        SetRect(cells[slot].gameObject, x, yTop, CellSize, CellSize);
        cells[slot].SetShown(true);
        cells[slot].SetItem(id, amount);
    }

    private void HideInputsFrom(int used)
    {
        for(int i = used; i < InputSlots; i++)cells[i].SetShown(false);
    }

    private void HideOutputsFrom(int used)
    {
        for(int i = used; i < OutputSlots; i++)cells[InputSlots + i].SetShown(false);
    }

    private void SetPageRowActive(bool value)
    {
        prevButton.gameObject.SetActive(value);
        pageText.gameObject.SetActive(value);
        nextButton.gameObject.SetActive(value);
    }

    // Player-facing words for the recipe form. Processing maps to "Smelting"
    // because the furnace is the only processing station today; a future
    // non-furnace station should derive its title from RecipeType instead.
    private static string TitleFor(RecipeKind kind)
        => kind == RecipeKind.Processing ? "Smelting" : "Crafting";

    private static bool TryResolve(string fullName, out ushort id)
    {
        id = JEICell.NoItem;
        return !string.IsNullOrEmpty(fullName) && ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(fullName, out id);
    }

    private void WarnClampOnce()
    {
        if(warnedClamp)return;
        warnedClamp = true;
        Debug.LogWarning("[JEI] recipe exceeds the 3x3 recipe page: display clamped");
    }

    // Whole block (grid + arrow + output) horizontally centered in the panel.
    private static float GridWidth(int cols) => cols * CellPitch - (CellPitch - CellSize);
    private static float BlockStartX(float gridW) => (PanelW - (gridW + Gap + ArrowW + Gap + CellSize)) * 0.5f;
    private static float RowCenterY(int rows) => RowY0 + ((rows - 1) * CellPitch + CellSize) * 0.5f;

    // Page children lay out from the panel's top-left corner.
    private static void SetRect(GameObject go, float x, float yTop, float width, float height)
    {
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(x, -yTop);
    }

    private UIButtonWidget MakeButton(string label, float x, float y, float width, float height = 22f)
    {
        var go = UIButtonWidget.CreateNewButton(label, null, gameObject);
        // UIButtonWidget.Awake forces center anchors: re-anchor after creation.
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        var widget = go.GetComponent<UIButtonWidget>();
        widget.SetFontSize(14f);
        SetRect(go, x, y, width, height);
        return widget;
    }

    private UITextWidget MakeText(float x, float y, float width, float height, float fontSize,
        TextAlignmentOptions alignment)
    {
        var go = UITextWidget.CreateNewText("", gameObject);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        var widget = go.GetComponent<UITextWidget>();
        widget.SetFontSize(fontSize);
        widget.SetAlignment(alignment);
        SetRect(go, x, y, width, height);
        return widget;
    }
}
