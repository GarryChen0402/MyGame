using System.Collections.Generic;
using UnityEngine;

// Crafting module: player-driven grid matching (vanilla workbench style).
// Reads the grid inventory referenced by Config.InputInventory, matches the
// arrangement against crafting recipes of Config.RecipeType and exposes the
// matched result. No tick; the container GUI calls OnGridChanged / TryTakeResult.
public class CraftingModule : BlockEntityModule
{
    public const string DefaultRecipeType = "minecraft:crafting";

    public InventoryModule Grid;      // resolved from Config.InputInventory
    public InventoryModule Output;    // optional; null = result goes to the player
    public RecipeDefinition CurrentRecipe;   // null when the grid matches nothing
    public ItemStack Result;                // matched output preview

    public CraftingModule(ModuleDefinition config) { Config = config; }

    public override void OnPlaced()
    {
        Grid = GetModule<InventoryModule>(Config.InputInventory);
        Output = GetModule<InventoryModule>(Config.OutputInventory);
        if (Grid == null) Debug.LogWarning($"[CraftingModule] missing input inventory '{Config.InputInventory}'");
    }

    // Player put/took grid slots: re-match and expose the new result.
    public void OnGridChanged()
    {
        Recompute();
        MarkDirty();
    }

    public void Recompute()
    {
        CurrentRecipe = MatchRecipe();
        Result = null;
        if (CurrentRecipe != null && CurrentRecipe.Outputs != null && CurrentRecipe.Outputs.Count > 0)
        {
            var o = CurrentRecipe.Outputs[0];
            if (ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(o.itemId, out ushort id))
                Result = new ItemStack { itemId = id, amount = o.amount };
        }
    }

    // Player takes the result: consume the grid, then either insert into the
    // output inventory (when configured) or leave Result for the caller (hand).
    // Returns false when the output inventory has no room (nothing is consumed).
    public bool TryTakeResult()
    {
        if (CurrentRecipe == null || Result == null || Result.IsEmpty()) return false;
        if (Output != null && !CanFitOutput(CurrentRecipe)) return false;
        if (!ConsumeGrid(CurrentRecipe)) return false;

        bool success = true;
        if (Output != null)
            success = Output.TryInsert(Result, InventoryAccess.Module);
        if (success) Recompute();
        MarkDirty();
        return success;
    }

    // ---- matching ----

    private RecipeDefinition MatchRecipe()
    {
        string type = string.IsNullOrEmpty(Config.RecipeType) ? DefaultRecipeType : Config.RecipeType;
        foreach (var recipe in ResourceSystem.Instance.Recipes.Values)
        {
            if (recipe.RecipeTypeFullName != type) continue;
            if (recipe.Shape == null ? MatchesLoose(recipe) : MatchesShape(recipe)) return recipe;
        }
        return null;
    }

    // Loose recipe (no Shape): the grid must contain exactly the Inputs multiset.
    private bool MatchesLoose(RecipeDefinition recipe)
    {
        if (recipe.Inputs == null) return false;
        int filled = 0;
        foreach (var stack in Grid.Inventory.itemStacks)
            if (stack != null && !stack.IsEmpty()) filled++;
        if (filled != recipe.Inputs.Count) return false;

        var remaining = new List<ItemStackAmount>(recipe.Inputs);
        foreach (var stack in Grid.Inventory.itemStacks)
        {
            if (stack == null || stack.IsEmpty()) continue;
            if (!ResourceSystem.Instance.ItemDefinitions.TryGetStringId(stack.itemId, out string name)) return false;
            int idx = remaining.FindIndex(r => r.itemId == name);
            if (idx < 0) return false;
            if (remaining[idx].amount > stack.amount) return false;
            remaining.RemoveAt(idx);
        }
        return true;
    }

    // Shape recipe: the grid's filled cells must fit the recipe's bounding box
    // (translation allowed, rotation not) with every symbol matching its item.
    private bool MatchesShape(RecipeDefinition recipe)
    {
        if (recipe.Shape == null || recipe.ShapeKeys == null) return false;
        int width = recipe.Shape[0].Length;
        int height = recipe.Shape.Length;

        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        var cells = new Dictionary<(int x, int y), ushort>();   // grid coords -> item id
        for (int y = 0; y < Config.GridHeight; y++)
        {
            for (int x = 0; x < Config.GridWidth; x++)
            {
                var stack = Grid.Inventory.GetItemStackAt(y * Config.GridWidth + x);
                if (stack == null || stack.IsEmpty()) continue;
                cells[(x, y)] = stack.itemId;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }
        if (maxX - minX + 1 != width || maxY - minY + 1 != height) return false;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                char symbol = recipe.Shape[y][x];
                if (symbol == ' ')
                {
                    if (cells.ContainsKey((minX + x, minY + y))) return false;
                    continue;
                }
                if (!cells.TryGetValue((minX + x, minY + y), out ushort itemId)) return false;
                if (!recipe.ShapeKeys.TryGetValue(symbol, out string expected)) return false;
                if (!ResourceSystem.Instance.ItemDefinitions.TryGetStringId(itemId, out string actual) || actual != expected) return false;
            }
        }
        return true;
    }

    // ---- consumption ----

    private bool CanFitOutput(RecipeDefinition recipe)
    {
        foreach (var o in recipe.Outputs)
        {
            if (!ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(o.itemId, out ushort id)) return false;
            if (!ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(id, out var def)) return false;
            int room = 0;
            foreach (var stack in Output.Inventory.itemStacks)
                if (stack != null && !stack.IsEmpty() && stack.itemId == id) room += def.MaxStack - stack.amount;
            room += (Output.Inventory.MaxSlotCount - Output.Inventory.itemStacks.Count) * def.MaxStack;
            if (room < o.amount) return false;
        }
        return true;
    }

    private bool ConsumeGrid(RecipeDefinition recipe)
    {
        if (recipe.Shape != null)
        {
            // Shape recipe: consume one item per filled cell of the bounding box.
            int width = recipe.Shape[0].Length;
            int height = recipe.Shape.Length;
            var bounds = FindBounds();
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (recipe.Shape[y][x] != ' ')
                    {
                        int gx = bounds.minX + x, gy = bounds.minY + y;
                        if (!Grid.TryExtract(gy * Config.GridWidth + gx, 1, InventoryAccess.Module)) return false;
                    }
            return true;
        }

        // Loose recipe: consume the Inputs amounts one stack at a time.
        foreach (var req in recipe.Inputs)
        {
            if (!ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(req.itemId, out ushort id)) return false;
            int remaining = req.amount;
            for (int i = 0; i < Grid.Inventory.itemStacks.Count && remaining > 0; i++)
            {
                var stack = Grid.Inventory.itemStacks[i];
                if (stack == null || stack.IsEmpty() || stack.itemId != id) continue;
                int take = Mathf.Min(remaining, stack.amount);
                if (!Grid.TryExtract(i, take, InventoryAccess.Module)) return false;
                remaining -= take;
            }
            if (remaining > 0) return false;
        }
        return true;
    }

    private (int minX, int minY) FindBounds()
    {
        int minX = Config.GridWidth, minY = Config.GridHeight;
        for (int y = 0; y < Config.GridHeight; y++)
            for (int x = 0; x < Config.GridWidth; x++)
            {
                var stack = Grid.Inventory.GetItemStackAt(y * Config.GridWidth + x);
                if (stack == null || stack.IsEmpty()) continue;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
            }
        return (minX, minY);
    }
}
