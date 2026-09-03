using System;
using System.Collections.Generic;
using UnityEngine;

// Host contract for the crafting grid/result slot accessors (design doc
// Docs/玩家界面-模型预览组件与2x2个人合成设计方案.md §5.1.2): both a block
// entity's work container and the player forward these callbacks to their own
// solver, so the UI code binds through one interface.
public interface ICraftingGridHost
{
    void OnGridChanged();
    void OnResultTaken();
}

// Grid crafting state machine, decoupled from any host (BlockEntity / Player).
// While the grid matches a recipe, its output sits live in the result slot as
// a preview; taking the result consumes one full recipe set from the grid and
// re-matches immediately. Shaped recipes match by translation only (no
// rotation) over the shape's non-empty bounding box; shapeless recipes match
// slot-to-placeholder (see MatchesLoose) - dispatched by Kind.
public class CraftingSolver
{
    public readonly InventoryDataContainer Grid;
    public readonly InventoryDataContainer Result;
    public readonly int GridWidth;
    public readonly int GridHeight;
    public readonly List<string> SupportedRecipeTypes;

    // Host dirty hook (player passes null: the player save is written whole on
    // a fixed cadence, so per-change marking is unnecessary).
    public Action MarkDirty;

    public RecipeContent CurrentMatch { get; private set; }   // live preview source; runtime only, never persisted
    private int matchRowOffset;          // shape row -> grid row shift of CurrentMatch
    private int matchColOffset;          // shape col -> grid col shift of CurrentMatch
    private readonly List<int> matchSlots = new();   // grid indices paired to shapeless placeholders; synced with CurrentMatch

    public CraftingSolver(InventoryDataContainer grid, InventoryDataContainer result,
        int gridWidth, int gridHeight, List<string> supportedRecipeTypes, Action markDirty)
    {
        Grid = grid;
        Result = result;
        GridWidth = gridWidth;
        GridHeight = gridHeight;
        SupportedRecipeTypes = supportedRecipeTypes;
        MarkDirty = markDirty;
    }

    // ---- player-driven events ----

    // Fired by the grid slot accessors after every successful click/quick-move
    // settlement, inside the same synchronous click flow: re-match and rebuild
    // or clear the preview, so the result slot never shows a phantom output
    // once the grid stopped matching.
    public void OnGridChanged() => RefreshPreview();

    // Fired when the result slot is emptied (pickup / shift-move): consume the
    // previewed recipe set, then re-match the remaining grid. Repeated clicks
    // cannot over-issue - the first take already consumed the materials.
    public void OnResultTaken()
    {
        if(CurrentMatch == null)return;
        Consume(CurrentMatch);
        CurrentMatch = null;
        ClearResult();
        RefreshPreview();   // grid may still match -> preview the next set
        MarkDirty?.Invoke();
    }

    // Clears the virtual preview without touching the grid (UI close / save
    // restore): the result slot only ever holds a preview of a live match.
    public void ClearPreview()
    {
        CurrentMatch = null;
        ClearResult();
    }

    // ---- preview matching ----

    // Re-scans the grid and writes the first matching recipe's output into the
    // result slot. The slot is mutated directly - the preview is
    // container-internal state, not a player insertion, so the result
    // container's Module-only InsertPolicy must not block it.
    public void RefreshPreview()
    {
        if(Grid == null || Result == null)return;
        CurrentMatch = FindRecipe();
        if(CurrentMatch == null)
        {
            ClearResult();
            return;
        }
        var outEntry = CurrentMatch.Outputs != null && CurrentMatch.Outputs.Count > 0
            ? CurrentMatch.Outputs[0] : null;
        if(outEntry == null || !ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(outEntry.itemId, out ushort outId))
        {
            Debug.LogWarning($"[CraftingSolver] recipe '{CurrentMatch.FullName}' output is missing or unregistered");
            CurrentMatch = null;
            ClearResult();
            return;
        }
        var slot = Result.Inv.itemStacks[0];
        if(slot.itemId == outId && slot.amount == outEntry.amount)return;   // unchanged
        slot.itemId = outId;
        slot.amount = outEntry.amount;
        MarkDirty?.Invoke();
    }

    // First recipe of the supported recipe types that fits, scanned bucket by
    // bucket in the declared type order (a cross-type ambiguity resolves to
    // the earlier declared type). The demo recipes are shape-disjoint, so
    // iteration order cannot pick a wrong one.
    private RecipeContent FindRecipe()
    {
        if(Grid == null || SupportedRecipeTypes == null)return null;
        foreach(var recipeType in SupportedRecipeTypes)
        {
            foreach(var recipe in ResourceSystem.Instance.GetRecipesByRecipeType(recipeType))
            {
                bool fits = recipe.Kind switch
                {
                    RecipeKind.Shaped => MatchesShape(recipe),
                    RecipeKind.Shapeless => MatchesLoose(recipe),
                    // Processing recipes never run here; a mismatched direct
                    // registration is already rejected at Freeze, skip defensively.
                    _ => false
                };
                if(fits)return recipe;
            }
        }
        return null;
    }

    // Vanilla-style translation matching. The shape's non-empty bounding box
    // (ragged row tails count as empty) is tried at every offset that fits:
    // symbol cells must hold their ShapeKeys item, all other grid cells must
    // be empty. No rotation, no mirrors, no extra items.
    private bool MatchesShape(RecipeContent recipe)
    {
        int rows = recipe.Shape.Length;
        int minR = rows, maxR = -1, minC = int.MaxValue, maxC = -1;
        for(int r = 0; r < rows; r++)
        {
            string row = recipe.Shape[r];
            for(int c = 0; c < row.Length; c++)
            {
                if(row[c] == ' ')continue;
                if(r < minR)minR = r;
                if(r > maxR)maxR = r;
                if(c < minC)minC = c;
                if(c > maxC)maxC = c;
            }
        }
        if(maxR < 0)return false;   // empty shape can never match

        int boxH = maxR - minR + 1, boxW = maxC - minC + 1;
        for(int dy = 0; dy + boxH <= GridHeight; dy++)
        {
            for(int dx = 0; dx + boxW <= GridWidth; dx++)
            {
                if(!ShapeFitsAt(recipe, dy - minR, dx - minC))continue;
                matchRowOffset = dy - minR;   // shape (r, c) sits at grid (r + offset, c + offset)
                matchColOffset = dx - minC;
                return true;
            }
        }
        return false;
    }

    // True when every grid cell matches the shape translated by (rowShift,
    // colShift): symbol cells need their item, everything else must be empty.
    private bool ShapeFitsAt(RecipeContent recipe, int rowShift, int colShift)
    {
        for(int r = 0; r < GridHeight; r++)
        {
            for(int c = 0; c < GridWidth; c++)
            {
                var slot = Grid.Inv.GetItemStackAt(r * GridWidth + c);
                char symbol = SymbolAt(recipe, r - rowShift, c - colShift);
                if(symbol == ' ')
                {
                    if(slot != null && !slot.IsEmpty())return false;
                    continue;
                }
                if(!recipe.ShapeKeys.TryGetValue(symbol, out string fullName))return false;
                if(!ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(fullName, out ushort itemId))return false;
                if(slot == null || slot.IsEmpty() || slot.itemId != itemId)return false;
            }
        }
        return true;
    }

    // Shape symbol at shape coordinates (r, c); ' ' outside the array, past a
    // ragged row end, or left of column 0 - i.e. an empty-cell requirement.
    private static char SymbolAt(RecipeContent recipe, int r, int c)
    {
        if(r < 0 || r >= recipe.Shape.Length || c < 0)return ' ';
        string row = recipe.Shape[r];
        if(c >= row.Length)return ' ';
        return row[c];
    }

    // Shapeless slot-placeholder match (vanilla ShapelessRecipe semantics):
    // inputs expand into placeholders (an entry of amount N = N same-item
    // placeholders). The number of occupied slots must equal the total
    // placeholder count and every occupied slot must pair with a placeholder
    // of its item - one slot too many, one too few or a foreign item all
    // fail. A slot holding a stack of several items still claims a single
    // placeholder; the surplus stays in the slot and is left over on consume.
    private bool MatchesLoose(RecipeContent recipe)
    {
        matchSlots.Clear();
        if(recipe.Inputs == null || recipe.Inputs.Count == 0)return false;
        int needTotal = 0;
        var placeholders = new Dictionary<ushort, int>();   // item id -> remaining placeholders
        foreach(var need in recipe.Inputs)
        {
            if(!ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(need.itemId, out ushort id))return false;
            needTotal += need.amount;
            placeholders.TryGetValue(id, out int have);
            placeholders[id] = have + need.amount;
        }
        if(needTotal <= 0)return false;

        int occupied = 0;
        for(int i = 0; i < Grid.Inv.itemStacks.Count; i++)
        {
            var slot = Grid.Inv.GetItemStackAt(i);
            if(slot == null || slot.IsEmpty())continue;
            occupied++;
            if(occupied > needTotal)return false;   // more occupied slots than placeholders can never fit
            if(!placeholders.TryGetValue(slot.itemId, out int left) || left <= 0)return false;
            placeholders[slot.itemId] = left - 1;
            matchSlots.Add(i);
        }
        return occupied == needTotal;
    }

    // ---- consumption (on result take) ----

    private void Consume(RecipeContent recipe)
    {
        if(recipe.Kind == RecipeKind.Shaped)ConsumeShaped(recipe);
        else ConsumeLoose(recipe);   // shapeless (the only other kind that can preview here)
    }

    // Shaped: consume the exact symbol cells of the matched shape at its
    // offset - one per cell, spaces untouched.
    private void ConsumeShaped(RecipeContent recipe)
    {
        for(int r = 0; r < recipe.Shape.Length; r++)
        {
            string row = recipe.Shape[r];
            for(int c = 0; c < row.Length; c++)
            {
                if(row[c] == ' ' || !recipe.ShapeKeys.ContainsKey(row[c]))continue;
                int gr = r + matchRowOffset, gc = c + matchColOffset;
                Grid.Inv.TryConsumeItemAt(gr * GridWidth + gc, 1);
            }
        }
    }

    // Loose: consume one item from every slot that was paired with a
    // placeholder. matchSlots is re-synced by the last RefreshPreview and the
    // grid cannot change between preview and take without another refresh, so
    // the pairing is still exact here.
    private void ConsumeLoose(RecipeContent recipe)
    {
        foreach(int slot in matchSlots)Grid.Inv.TryConsumeItemAt(slot, 1);
    }

    private void ClearResult()
    {
        if(Result == null)return;
        var slot = Result.Inv.itemStacks[0];
        if(slot.IsEmpty())return;
        slot.Clear();
        MarkDirty?.Invoke();
    }
}
