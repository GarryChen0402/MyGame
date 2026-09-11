using System.Collections.Generic;

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
        for(int i=0;i<MaxSlotCount;i++)
        {
            itemStacks.Add(new ItemStack());
        }
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
        if (itemStack == null || itemStack.IsEmpty()) return false;
        if (!CanAddItem(itemStack)) return false;   // capacity pre-check, never partially consumes on failure
        return AddItemStackInternal(itemStack);
    }

    public bool TryAddItemAsMax(ItemStack itemStack)
    {
        return TryAddItemStack(itemStack);
    }

    // Merge into same-item stacks first, then place the remainder into the
    // first empty slot, then expand only when CanAutoExpand.
    private bool AddItemStackInternal(ItemStack itemStack)
    {
        foreach (var slot in itemStacks)
        {
            if (slot.TryAddItemAsMax(itemStack)) return true;   // fully merged
        }
        foreach (var slot in itemStacks)
        {
            if (slot.IsEmpty())
            {
                slot.itemId = itemStack.itemId;
                slot.amount = itemStack.amount;
                itemStack.Clear();
                return true;
            }
        }
        if (CanAutoExpand)
        {
            itemStacks.Add(itemStack);
            return true;
        }
        return false;   // full and nothing mergeable
    }

    public bool TryConsumeItemAt(int index, int amout = 1)
    {
        if(!IsCorrectSlotIndex(index))return false;
        return itemStacks[index].TryConsumeItem(amout);
    }

    public bool CanAddItem(ItemStack itemStack)
    {
        if(!ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(itemStack.itemId, out var def))return false;
        if(CanAutoExpand)return true;
        if(itemStacks.Count < MaxSlotCount)return true;
        int leftCapacity = 0;
        foreach(var item in itemStacks)
        {
            if(item.IsEmpty())leftCapacity+=def.MaxStack;
            else if(item.itemId == itemStack.itemId)leftCapacity += (def.MaxStack - item.amount);
        }
        return leftCapacity >= itemStack.amount;
    }
}