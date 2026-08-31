using System.Collections.Generic;
using UnityEngine;

// Inventory module: the only data carrier in the module system. Every item
// stored in a block entity lives in one of these. Access is role-checked
// (InsertPolicy / ExtractPolicy) and filtered (AllowedItems / AllowedTags);
// the policies are static declaration config, only the slot data is saved.
public class InventoryModule : BlockEntityModule
{
    public Inventory Inventory;
    // Bumped on every successful insert/extract; behavior modules use it to
    // detect input changes instead of comparing slots every frame.
    public int Version { get; private set; }

    public InventoryModule(ModuleDefinition config)
    {
        Config = config;
        Inventory = new Inventory(Mathf.Max(1, config.Capacity), false);
    }

    public bool CanInsert(ItemStack stack, InventoryAccess requester)
    {
        if (stack == null || stack.IsEmpty()) return false;
        if (Config.InsertPolicy == InventoryAccess.None) return false;
        if (Config.InsertPolicy != InventoryAccess.Any && Config.InsertPolicy != requester) return false;
        return IsAllowedItem(stack);
    }

    public bool CanExtract(InventoryAccess requester)
    {
        if (Config.ExtractPolicy == InventoryAccess.None) return false;
        return Config.ExtractPolicy == InventoryAccess.Any || Config.ExtractPolicy == requester;
    }

    // Role-checked storage: the player UI passes Player, module logic passes Module.
    public bool TryInsert(ItemStack stack, InventoryAccess requester)
    {
        if (!CanInsert(stack, requester)) return false;
        if (!Inventory.TryAddItemStack(stack)) return false;
        Version++;
        MarkDirty();
        return true;
    }

    public bool TryExtract(int index, int amount, InventoryAccess requester)
    {
        if (!CanExtract(requester)) return false;
        if (!Inventory.TryConsumeItemAt(index, amount)) return false;
        Version++;
        MarkDirty();
        return true;
    }

    // Filtering constrains insertion only; extraction follows the slot content.
    // AllowedItems and AllowedTags form a union; both empty = unrestricted.
    private bool IsAllowedItem(ItemStack stack)
    {
        if (!ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(stack.itemId, out var def)) return false;
        if (Config.AllowedItems != null && Config.AllowedItems.Contains(def.FullName)) return true;
        if (Config.AllowedTags != null && def.Tags != null)
            foreach (var tag in Config.AllowedTags)
                if (def.Tags.Contains(tag)) return true;
        return Config.AllowedItems == null && Config.AllowedTags == null;
    }

    // ---- serialization: slot list, item ids as full names ----

    [System.Serializable]
    private class InventorySaveData
    {
        public List<ItemStackSaveData> slots = new();
    }

    public override string SerializeToJson()
    {
        var data = new InventorySaveData();
        foreach (var stack in Inventory.itemStacks)
        {
            if (stack == null || stack.IsEmpty()) continue;
            if (!ResourceSystem.Instance.ItemDefinitions.TryGetStringId(stack.itemId, out string name)) continue;
            data.slots.Add(new ItemStackSaveData { itemId = name, amount = stack.amount });
        }
        return JsonUtility.ToJson(data);
    }

    public override void DeserializeFromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        var data = JsonUtility.FromJson<InventorySaveData>(json);
        if (data?.slots == null) return;
        foreach (var slot in data.slots)
        {
            if (slot == null || slot.amount <= 0 || string.IsNullOrEmpty(slot.itemId)) continue;
            if (!ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(slot.itemId, out ushort itemId)) continue;
            Inventory.TryAddItemAsMax(new ItemStack { itemId = itemId, amount = slot.amount });
        }
    }
}
