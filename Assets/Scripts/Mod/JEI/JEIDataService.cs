using System;
using System.Collections.Generic;

// Read-only view of the frozen item registry for the JEI panel: the item-id
// snapshot (registration order) plus the P1 search filter. Built lazily on
// first use - the panel only exists after bootstrap, so the registry is
// frozen and the snapshot never needs invalidation.
public class JEIDataService
{
    private readonly List<ushort> allIds = new();
    private readonly List<ushort> filtered = new();
    private bool built;

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
}
