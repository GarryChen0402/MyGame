using UnityEngine;

// Loot table validation partial (parallel to ResourceSystem.Recipes.cs): bad
// references log errors but do not abort startup (unlike recipe validation) -
// a broken table simply rolls nothing at runtime, mod content must not hang
// the boot.
public partial class ResourceSystem
{
    private void ValidateLootReferences()
    {
        foreach(var table in LootTables.Values)
        {
            if(table.Groups == null || table.Groups.Count == 0)
            { Debug.LogError($"[Loot] {table.FullName} has no groups (min 1 required)"); continue; }
            foreach(var group in table.Groups)
                foreach(var entry in group.Entries)
                {
                    if(entry.ItemInfo == null)
                    { Debug.LogError($"[Loot] {table.FullName} entry without ItemInfo"); continue; }
                    if(!ItemDefinitions.ContainsValue(entry.ItemInfo.ItemFullName))
                        Debug.LogError($"[Loot] {table.FullName} references unknown item {entry.ItemInfo.ItemFullName}");
                    if(entry.ItemInfo.Amount < 1)
                        Debug.LogError($"[Loot] {table.FullName} amount < 1");
                    if(entry.Weight < 0f)
                        Debug.LogError($"[Loot] {table.FullName} negative weight");
                }
        }
    }
}
