using System.Collections.Generic;
using UnityEngine;

// Resolves what a broken block drops, read before the break removes it
// (design doc 掉落物ItemEntity实现方案.md §6). v1: the block drops itself x1
// (RegisterBlock auto-registers a same-name block item) plus, when it hosts a
// block entity, every non-empty inventory slot across its data containers.
// Content/carrier decoupling: future loot tables replace the fixed self-drop.
public static class DropResolver
{
    // The registry has no block->item reverse query, so a lazily built map
    // fills the gap (first break happens well after Freeze).
    private static Dictionary<string, ushort> itemIdByBlockName = null;

    public static List<ItemStack> Collect(Dimension dim, Vector3Int coord)
    {
        var drops = new List<ItemStack>();
        ushort stateId = dim.GetBlockAt(coord);
        if(stateId != 0)
        {
            var blockDef = ResourceSystem.Instance.GetState(stateId)?.Block;
            if(blockDef != null && TryGetBlockItemId(blockDef.FullName, out ushort itemId))
                drops.Add(new ItemStack { itemId = itemId, amount = 1 });
        }

        if(dim.TryGetBlockEntity(coord, out BlockEntity be))
        {
            // Breaking empties every inventory container regardless of
            // insert/extract policy (vanilla semantics). Stacks are copied so
            // the drops stay decoupled from the BE being torn down.
            foreach(var container in be.DataContainers.Values)
                if(container is InventoryDataContainer inv)
                    foreach(var stack in inv.Inv.itemStacks)
                        if(stack != null && !stack.IsEmpty())
                            drops.Add(new ItemStack { itemId = stack.itemId, amount = stack.amount });
        }
        return drops;
    }

    private static bool TryGetBlockItemId(string blockFullName, out ushort itemId)
    {
        itemId = 0;
        if(itemIdByBlockName == null)
        {
            itemIdByBlockName = new Dictionary<string, ushort>();
            foreach(var def in ResourceSystem.Instance.ItemDefinitions.Values)
                if(!string.IsNullOrEmpty(def.BlockFullName)
                   && ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(def.FullName, out ushort id))
                    itemIdByBlockName[def.BlockFullName] = id;
        }
        return itemIdByBlockName.TryGetValue(blockFullName, out itemId);
    }
}
