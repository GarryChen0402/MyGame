using System;
using System.Collections.Generic;

// Read-only view of the frozen registries for the JEI panel: the item-id
// snapshot (registration order), the P1 search filter, and the P2 recipe
// reverse index. Built lazily on first use - the panel only exists after
// bootstrap, so the registries are frozen and nothing needs invalidation.
public class JEIDataService
{
    private static readonly List<RecipeContent> NoRecipes = new();

    private readonly List<ushort> allIds = new();
    private readonly List<ushort> filtered = new();
    private bool built;

    private Dictionary<ushort, List<RecipeContent>> byOutput;
    private Dictionary<ushort, List<RecipeContent>> byInput;

    public IReadOnlyList<ushort> Filtered => filtered;

    public void EnsureBuilt()
    {
        if(built)return;
        built = true;
        var items = ResourceSystem.Instance.ItemDefinitions;
        // Explicit id walk: registration order is stable (a new item appends),
        // Values enumeration order is not contractual.
        for(int id = 0; id < items.Count; id++)
            if(items.TryGetResourceWithNumberId((ushort)id, out _))allIds.Add((ushort)id);
        filtered.AddRange(allIds);
    }

    // P1 grammar: case-insensitive substring over the full name, space-split
    // tokens AND-combined ("oak log" matches "minecraft:oak_log").
    public void ApplyFilter(string query)
    {
        EnsureBuilt();
        filtered.Clear();
        if(string.IsNullOrWhiteSpace(query))
        {
            filtered.AddRange(allIds);
            return;
        }

        string[] tokens = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var items = ResourceSystem.Instance.ItemDefinitions;
        foreach(ushort id in allIds)
        {
            if(!items.TryGetStringId(id, out string fullName))continue;
            string lower = fullName.ToLowerInvariant();
            bool all = true;
            foreach(string token in tokens)
            {
                if(lower.Contains(token))continue;
                all = false;
                break;
            }
            if(all)filtered.Add(id);
        }
    }

    // ---- P2 recipe reverse index (design §6.4) ----

    // Recipes that produce this item (the R view).
    public IReadOnlyList<RecipeContent> GetRecipesMaking(ushort itemId)
    {
        EnsureRecipeIndex();
        return byOutput.TryGetValue(itemId, out var list) ? list : NoRecipes;
    }

    // Recipes that consume this item (the U view).
    public IReadOnlyList<RecipeContent> GetRecipesUsing(ushort itemId)
    {
        EnsureRecipeIndex();
        return byInput.TryGetValue(itemId, out var list) ? list : NoRecipes;
    }

    // Lazy once: recipe registration is frozen after bootstrap. Id walk keeps
    // registration order, same stance as the item snapshot above.
    private void EnsureRecipeIndex()
    {
        if(byOutput != null)return;
        byOutput = new Dictionary<ushort, List<RecipeContent>>();
        byInput = new Dictionary<ushort, List<RecipeContent>>();
        var recipes = ResourceSystem.Instance.Recipes;
        var items = ResourceSystem.Instance.ItemDefinitions;
        var inputs = new HashSet<ushort>();   // per-recipe dedupe (reused)
        for(int id = 0; id < recipes.Count; id++)
        {
            if(!recipes.TryGetResourceWithNumberId((ushort)id, out var recipe))continue;
            inputs.Clear();
            if(recipe.Inputs != null)
                foreach(var entry in recipe.Inputs)
                    if(entry != null && items.TryGetNumberId(entry.itemId, out ushort inId))inputs.Add(inId);
            if(recipe.ShapeKeys != null)
                foreach(var pair in recipe.ShapeKeys)
                    if(items.TryGetNumberId(pair.Value, out ushort inId))inputs.Add(inId);
            foreach(ushort inId in inputs)AddIndexEntry(byInput, inId, recipe);
            if(recipe.Outputs == null)continue;
            foreach(var entry in recipe.Outputs)
                if(entry != null && items.TryGetNumberId(entry.itemId, out ushort outId))AddIndexEntry(byOutput, outId, recipe);
        }
    }

    private static void AddIndexEntry(Dictionary<ushort, List<RecipeContent>> map, ushort itemId, RecipeContent recipe)
    {
        if(!map.TryGetValue(itemId, out var list))map[itemId] = list = new List<RecipeContent>();
        list.Add(recipe);
    }
}
