using System.Collections.Generic;
using MCPForUnity.Editor.Services.AssetGen.Providers;

public class Inventory
{
    public readonly List<ItemStack> itemStacks;
    public bool CanAutoExpand = false;
    public int MaxSlotCount;


    public Inventory(int capacity, bool canExpand = false)
    {
        CanAutoExpand = canExpand;
        MaxSlotCount = capacity;
        itemStacks = new List<ItemStack>
        {
            Capacity = MaxSlotCount
        };
    }
    private bool IsCorrectSlotIndex(int index)
    {
        return index >= 0 && index < itemStacks.Count;
    }
    public ItemStack GetItemStackAt(int index)
    {
        if(IsCorrectSlotIndex(index))return itemStacks[index];
        return null;
    }

    public bool TryAddItemStack(ItemStack itemStack)
    {
        int leftCapasity = 0;
        foreach(var item in itemStacks)
        {
            if(!ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(item.itemId, out var itemDef))continue;
            if(itemStack.Equals(item))leftCapasity += (itemDef.MaxStack - item.amount);
        }
        if(leftCapasity >= itemStack.amount || itemStacks.Count < MaxSlotCount || (itemStacks.Count == MaxSlotCount && CanAutoExpand))
        {
            foreach(var item in itemStacks)
            {
                if(item.TryAddItemAsMax(itemStack))return true;
            }
            if(CanAutoExpand || itemStacks.Count < MaxSlotCount)
            {
                itemStacks.Add(itemStack);
                return true;
            }
            return true;
        }
        else
        {
            return false;
        }
    }

    public bool TryAddItemAsMax(ItemStack itemStack)
    {
        foreach(var item in itemStacks)
        {
            if(item.TryAddItemAsMax(itemStack))return true;
        }
        if(CanAutoExpand || itemStacks.Count < MaxSlotCount)
        {
            itemStacks.Add(itemStack);
            return true;
        }
        return false;
    }

    public bool TryConsumeItemAt(int index, int amout = 1)
    {
        if(!IsCorrectSlotIndex(index))return false;
        return itemStacks[index].TryConsumeItem(amout);
    }
}