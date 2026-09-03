using System;
using UnityEngine;

// Instant 3x3 crafting work container (workbench): purely player-driven, no
// periodic work. While the grid matches a recipe, its output sits live in the
// result slot as a preview; taking the result consumes one full recipe set
// from the grid and re-matches immediately. Recipes with a Shape match by
// translation only (no rotation) over the shape's non-empty bounding box;
// Shape == null matches loose by multiset totals.
public class CraftingWorkContainer : WorkContainer
{
    [Serializable]
    public class Config
    {
        public string Grid, Result;
        public int GridWidth = 3, GridHeight = 3;
    }

    private readonly Config config;
    private readonly string recipeTypeId;   // recipe category full name (WorkContainerConfig.RecipeType)

    public InventoryDataContainer Grid, Result;
    public RecipeDefinition CurrentMatch;   // source of the live preview; runtime only, never persisted
    private int matchRowOffset;             // shape row -> grid row shift of CurrentMatch
    private int matchColOffset;             // shape col -> grid col shift of CurrentMatch

    public CraftingWorkContainer(WorkContainerConfig config)
    {
        this.config = JsonUtility.FromJson<Config>(config.Parameters);
        recipeTypeId = config.RecipeType;
    }

    public override void OnBind(BlockEntity blockEntity)
    {
        base.OnBind(blockEntity);
        if(config == null)return;
        Grid = GetData<InventoryDataContainer>(config.Grid);
        Result = GetData<InventoryDataContainer>(config.Result);
        if(Grid == null || Result == null)
            Debug.LogWarning("[CraftingWorkContainer] missing grid/result container(s), disabled");
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
        MarkDirty();
    }

    // Clears the virtual preview without touching the grid (UI close / save
    // restore): the result slot only ever holds a preview of a live match.
    public void ClearPreview()
    {
        CurrentMatch = null;
        ClearResult();
    }

    // Instant crafting: no periodic work; Tick stays empty to keep the
    // container pipeline uniform (BlockEntityManager ticks at 20 Hz).
    public override void Tick() { }

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
            Debug.LogWarning($"[CraftingWorkContainer] recipe '{CurrentMatch.FullName}' output is missing or unregistered");
            CurrentMatch = null;
            ClearResult();
            return;
        }
        var slot = Result.Inv.itemStacks[0];
        if(slot.itemId == outId && slot.amount == outEntry.amount)return;   // unchanged
        slot.itemId = outId;
        slot.amount = outEntry.amount;
        MarkDirty();
    }

    // First recipe of this container's category that fits. The demo recipes
    // are shape-disjoint, so registry iteration order cannot pick a wrong one.
    private RecipeDefinition FindRecipe()
    {
        if(Grid == null)return null;
        foreach(var recipe in ResourceSystem.Instance.Recipes.Values)
        {
            if(recipe.RecipeTypeFullName != recipeTypeId)continue;
            bool fits = recipe.Shape != null && recipe.Shape.Length > 0
                ? MatchesShape(recipe) : MatchesLoose(recipe);
            if(fits)return recipe;
        }
        return null;
    }

    // Vanilla-style translation matching. The shape's non-empty bounding box
    // (ragged row tails count as empty) is tried at every offset that fits:
    // symbol cells must hold their ShapeKeys item, all other grid cells must
    // be empty. No rotation, no mirrors, no extra items.
    private bool MatchesShape(RecipeDefinition recipe)
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
        for(int dy = 0; dy + boxH <= config.GridHeight; dy++)
        {
            for(int dx = 0; dx + boxW <= config.GridWidth; dx++)
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
    private bool ShapeFitsAt(RecipeDefinition recipe, int rowShift, int colShift)
    {
        for(int r = 0; r < config.GridHeight; r++)
        {
            for(int c = 0; c < config.GridWidth; c++)
            {
                var slot = Grid.Inv.GetItemStackAt(r * config.GridWidth + c);
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
    private static char SymbolAt(RecipeDefinition recipe, int r, int c)
    {
        if(r < 0 || r >= recipe.Shape.Length || c < 0)return ' ';
        string row = recipe.Shape[r];
        if(c >= row.Length)return ' ';
        return row[c];
    }

    // Loose multiset match (no Shape): every Input entry must be available and
    // the total grid count must equal the recipe need - one exact set, no
    // leftover or foreign items, position irrelevant.
    private bool MatchesLoose(RecipeDefinition recipe)
    {
        if(recipe.Inputs == null || recipe.Inputs.Count == 0)return false;
        int needTotal = 0;
        foreach(var need in recipe.Inputs)
        {
            needTotal += need.amount;
            if(!TryGetAvailable(need.itemId, out int have) || have < need.amount)return false;
        }
        if(needTotal <= 0)return false;
        return CountGridTotal() == needTotal;
    }

    // ---- consumption (on result take) ----

    private void Consume(RecipeDefinition recipe)
    {
        if(recipe.Shape != null && recipe.Shape.Length > 0)ConsumeShaped(recipe);
        else ConsumeLoose(recipe);
    }

    // Shaped: consume the exact symbol cells of the matched shape at its
    // offset - one per cell, spaces untouched.
    private void ConsumeShaped(RecipeDefinition recipe)
    {
        for(int r = 0; r < recipe.Shape.Length; r++)
        {
            string row = recipe.Shape[r];
            for(int c = 0; c < row.Length; c++)
            {
                if(row[c] == ' ' || !recipe.ShapeKeys.ContainsKey(row[c]))continue;
                int gr = r + matchRowOffset, gc = c + matchColOffset;
                Grid.Inv.TryConsumeItemAt(gr * config.GridWidth + gc, 1);
            }
        }
    }

    // Loose: cross-slot consume per input entry (same pattern as the
    // processing container's ConsumeInput).
    private void ConsumeLoose(RecipeDefinition recipe)
    {
        foreach(var need in recipe.Inputs)ConsumeNeed(need);
    }

    private void ConsumeNeed(ItemStackAmount need)
    {
        if(!ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(need.itemId, out ushort id))return;
        int left = need.amount;
        for(int i = 0; i < Grid.Inv.itemStacks.Count && left > 0; i++)
        {
            var slot = Grid.Inv.GetItemStackAt(i);
            if(slot == null || slot.IsEmpty() || slot.itemId != id)continue;
            int take = Mathf.Min(left, slot.amount);
            Grid.Inv.TryConsumeItemAt(i, take);
            left -= take;
        }
    }

    private bool TryGetAvailable(string fullName, out int total)
    {
        total = 0;
        if(!ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(fullName, out ushort id))return false;
        foreach(var slot in Grid.Inv.itemStacks)
            if(slot != null && slot.itemId == id)total += slot.amount;
        return true;
    }

    private int CountGridTotal()
    {
        int total = 0;
        foreach(var slot in Grid.Inv.itemStacks)
            if(slot != null && !slot.IsEmpty())total += slot.amount;
        return total;
    }

    private void ClearResult()
    {
        if(Result == null)return;
        var slot = Result.Inv.itemStacks[0];
        if(slot.IsEmpty())return;
        slot.Clear();
        MarkDirty();
    }

    // ---- persistence ----

    // State fully derives from the grid data container, nothing to save.
    public override string Serialize() => "{}";

    // Data containers restore before work containers (BlockEntityManager), so
    // the grid is already loaded: drop any result that got saved while a
    // preview sat in the slot - it never represents consumed materials.
    public override void Deserialize(string json)
    {
        CurrentMatch = null;
        ClearResult();
    }
}

// Grid slot accessor: after every successful click/quick-move settlement on
// the grid, re-run the crafting match inside the same synchronous click flow.
public class CraftingGridSlotAccess : ContainerSlotAccess
{
    private readonly CraftingWorkContainer owner;

    public CraftingGridSlotAccess(InventoryDataContainer container, int index, CraftingWorkContainer owner)
        : base(container, index)
    {
        this.owner = owner;
    }

    public override void MarkChanged()
    {
        base.MarkChanged();
        owner.OnGridChanged();
    }
}

// Result slot accessor: emptying the slot consumes the previewed recipe set.
// Policies stay on the base (the result container is Module-insert only), so
// players can only ever take from it. Half-take semantics for multi-item
// outputs are left for later - demo recipe outputs all amount to 1.
public class CraftingResultSlotAccess : ContainerSlotAccess
{
    private readonly CraftingWorkContainer owner;

    public CraftingResultSlotAccess(InventoryDataContainer container, int index, CraftingWorkContainer owner)
        : base(container, index)
    {
        this.owner = owner;
    }

    public override void MarkChanged()
    {
        base.MarkChanged();
        owner.OnResultTaken();
    }
}
