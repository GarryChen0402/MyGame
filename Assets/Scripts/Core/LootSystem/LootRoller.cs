using System.Collections.Generic;
using UnityEngine;

// Pure roll logic, stateless: rolls one table into the result list. Groups
// evaluate independently and append (each group's products add up); empty
// tables/groups add nothing (rule R1).
public static class LootRoller
{
    public static void Roll(LootTableDefinition table, LootContext ctx, List<ItemStack> result)
    {
        foreach(var group in table.Groups)
        {
            if(group.Condition != null && !group.Condition.Pass(ctx))continue;   // R1 gate
            foreach(var entry in group.Entries)
            {
                int hits = (int)entry.Weight;                       // R4: integer part - certain hits
                float frac = entry.Weight - hits;
                if(frac > 0f && Random.value < frac)hits++;         // R4/R6: fractional part, per-roll random
                if(hits <= 0)continue;
                if(!ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(
                        entry.ItemInfo.ItemFullName, out ushort itemId))continue;   // validated at Freeze
                for(int h = 0; h < hits; h++)
                    result.Add(new ItemStack { itemId = itemId, amount = entry.ItemInfo.Amount });   // R5: amount per hit
            }
        }
    }
}
