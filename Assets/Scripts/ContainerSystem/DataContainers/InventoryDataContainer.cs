using System.Collections.Generic;
using UnityEngine;

public enum ContainerAccess
{
    Any,
    Player,
    Module,
    None
}

public class InventoryDataContainer : DataContainer
{
    [System.Serializable]
    public class Config
    {
        public int Capacity = 1;
        public ContainerAccess InsertPolicy = ContainerAccess.Any;
        public ContainerAccess ExtractPolicy = ContainerAccess.Any;
        public List<string> AllowedItems;   // whitelist of item full names
        public List<string> AllowedTags;    // whitelist of item tags
    }

    private Config cfg;                       // declaration config, read-only, not persisted
    private HashSet<ushort> allowedItemIds;   // AllowedItems parsed to numeric ids once at construction
    public Inventory Inv;                     // runtime slot data, serialized with the BE

    public InventoryDataContainer(DataContainerConfig dataConfig)
    {
        cfg = JsonUtility.FromJson<Config>(dataConfig.Parameters);
        Inv = new Inventory(cfg.Capacity);
        if (cfg.AllowedItems != null)
        {
            allowedItemIds = new HashSet<ushort>();
            foreach (string fullName in cfg.AllowedItems)
            {
                if (ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(fullName, out ushort itemId))
                    allowedItemIds.Add(itemId);
                else
                    Debug.LogWarning($"[InventoryDataContainer] Unknown allowed item '{fullName}', ignored");
            }
        }
    }

    public bool CanInsert(ItemStack stack, ContainerAccess requester)
    {
        if (stack == null || stack.IsEmpty()) return false;
        if (cfg.InsertPolicy == ContainerAccess.None) return false;
        if (cfg.InsertPolicy != ContainerAccess.Any && requester != cfg.InsertPolicy) return false;
        if (Inv == null) return false;
        if (!IsItemAllowed(stack)) return false;
        return Inv.CanAddItem(stack);
    }

    public bool CanExtract(ContainerAccess requester)
    {
        if (cfg.ExtractPolicy == ContainerAccess.None) return false;
        if (cfg.ExtractPolicy != ContainerAccess.Any && requester != cfg.ExtractPolicy) return false;
        return Inv != null;
    }

    public bool TryInsert(ItemStack stack, ContainerAccess requester)
    {
        if (stack == null || stack.IsEmpty()) return false;
        if (!CanInsert(stack, requester)) return false;
        if (!Inv.TryAddItemStack(stack)) return false;
        MarkDirty();
        return true;
    }

    public bool TryExtract(int index, int amount, ContainerAccess requester)
    {
        if (!CanExtract(requester)) return false;
        if (!Inv.TryConsumeItemAt(index, amount)) return false;
        MarkDirty();
        return true;
    }

    // AllowedItems and AllowedTags form a whitelist union; both empty = unrestricted.
    private bool IsItemAllowed(ItemStack stack)
    {
        bool hasItems = cfg.AllowedItems != null && cfg.AllowedItems.Count > 0;
        bool hasTags = cfg.AllowedTags != null && cfg.AllowedTags.Count > 0;
        if (!hasItems && !hasTags) return true;
        if (!ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(stack.itemId, out var def)) return false;
        if (hasItems && allowedItemIds != null && allowedItemIds.Contains(stack.itemId)) return true;
        if (hasTags && def.Tags != null)
        {
            foreach (string tag in cfg.AllowedTags)
                if (def.Tags.Contains(tag)) return true;
        }
        return false;
    }

    public ItemStack GetItemStackAt(int index)
    {
        return Inv.GetItemStackAt(index);
    }

    // ---- persistence ----

    // Slot data only: capacity, policies and whitelists are declaration config
    // carried by the BE definition, never written to disk (design doc §7).
    [System.Serializable]
    public class SaveData
    {
        public List<ItemStackSaveData> slots = new();
    }

    public override string Serialize() => JsonUtility.ToJson(ExportSave());

    // Exports the slot content (id/amount/pinned slotIndex) without wrapping
    // it in JSON - shared by block-entity saves and the player save file, so
    // both write through the same slot-pinning rules.
    public SaveData ExportSave()
    {
        var save = new SaveData();
        var stacks = Inv.itemStacks;
        for (int i = 0; i < stacks.Count; i++)
        {
            var stack = stacks[i];
            if (stack == null || stack.IsEmpty()) continue;
            if (ResourceSystem.Instance.ItemDefinitions.TryGetStringId(stack.itemId, out string itemName))
                // slotIndex pins each stack to its slot so a reload restores
                // the exact layout (crafting grids match recipes by position).
                save.slots.Add(new ItemStackSaveData { itemId = itemName, amount = stack.amount, slotIndex = i });
        }
        return save;
    }

    // Restores each entry into the slot it was saved from, keeping the exact
    // layout. Entries without a valid slotIndex (older saves) and entries
    // whose saved slot is already occupied fall back to the first empty slot
    // in file order. Unknown items are skipped with a warning.
    public void RestoreSave(SaveData save)
    {
        if (save == null || save.slots == null) return;
        Inv.itemStacks.Clear();
        for (int i = 0; i < Inv.MaxSlotCount; i++) Inv.itemStacks.Add(new ItemStack());
        foreach (var entry in save.slots)
        {
            if (entry == null || entry.amount <= 0 || string.IsNullOrEmpty(entry.itemId)) continue;
            if (!ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(entry.itemId, out ushort itemId))
            {
                Debug.LogWarning($"[InventoryDataContainer] unknown item '{entry.itemId}' in save; skipped");
                continue;
            }
            int slot = entry.slotIndex >= 0 && entry.slotIndex < Inv.itemStacks.Count
                ? entry.slotIndex : -1;
            if (slot < 0 || !Inv.GetItemStackAt(slot).IsEmpty())
                slot = Inv.itemStacks.FindIndex(s => s.IsEmpty());
            if (slot < 0) continue;   // no free slot left (duplicated/overflowing save)
            Inv.itemStacks[slot] = new ItemStack { itemId = itemId, amount = entry.amount };
        }
    }

    public override void Deserialize(string json)
    {
        var save = JsonUtility.FromJson<SaveData>(json);
        RestoreSave(save);
    }
}
