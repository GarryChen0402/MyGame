using System.Collections.Generic;
using UnityEngine;

public class InventoryDataContainer : DataContainer
{
    [System.Serializable]
    public class Config
    {
        public int Capacity = 1;
        public SlotRule[] SlotRules;   // per-slot access rules; null = all slots fully permissive
        public string[] SlotCodes;     // data-side code per slot (= pack key); undeclared = no pack
    }

    private Config cfg;   // declaration config, read-only, not persisted
    public Inventory Inv; // runtime slot data, serialized with the BE

    public InventoryDataContainer(DataContainerConfig dataConfig)
    {
        cfg = JsonUtility.FromJson<Config>(dataConfig.Parameters);
        Inv = new Inventory(cfg.Capacity);
        // Declared codes must cover every slot; a mismatch disables the pack
        // (explicit declaration, no inferred fallback).
        if (cfg.SlotCodes != null && cfg.SlotCodes.Length != cfg.Capacity)
        {
            Debug.LogWarning($"[InventoryDataContainer] SlotCodes length {cfg.SlotCodes.Length} != capacity {cfg.Capacity}; pack disabled");
            cfg.SlotCodes = null;
        }
        // JsonUtility round-trips an undeclared (null) rule table as an empty
        // array, so an empty table means "not declared": all slots stay
        // permissive like any other container without rules.
        if (cfg.SlotRules != null && cfg.SlotRules.Length == 0) cfg.SlotRules = null;
        // Declared rules must cover every slot; a mismatch denies every
        // direction on every slot - a misdeclared permission must not
        // silently open the container.
        if (cfg.SlotRules != null && cfg.SlotRules.Length != cfg.Capacity)
        {
            Debug.LogWarning($"[InventoryDataContainer] SlotRules length {cfg.SlotRules.Length} != capacity {cfg.Capacity}; all slots denied");
            cfg.SlotRules = new SlotRule[cfg.Capacity];
            for (int i = 0; i < cfg.SlotRules.Length; i++)
                cfg.SlotRules[i] = new SlotRule
                {
                    InsertRequesters = ContainerAccess.None,
                    ExtractRequesters = ContainerAccess.None
                };
        }
    }

    private SlotRule RuleAt(int index)
        => cfg.SlotRules != null && index >= 0 && index < cfg.SlotRules.Length ? cfg.SlotRules[index] : null;

    // Per-slot policy: the rule at index gates requesters and tags; a null
    // table leaves every slot fully permissive.
    public bool CanInsert(int index, ItemStack stack, ContainerAccess requester)
    {
        if (stack == null || stack.IsEmpty()) return false;
        if (Inv == null) return false;
        var rule = RuleAt(index);
        if (rule != null && !rule.CanInsert(stack, requester)) return false;
        return Inv.CanAddItem(stack);
    }

    public bool CanExtract(int index, ContainerAccess requester)
    {
        if (Inv == null) return false;
        var rule = RuleAt(index);
        if (rule != null && !rule.CanExtract(Inv.GetItemStackAt(index), requester)) return false;
        return true;
    }

    // The index selects the rule; filling itself stays container-wide (all
    // current callers are single-slot output containers).
    public bool TryInsert(int index, ItemStack stack, ContainerAccess requester)
    {
        if (stack == null || stack.IsEmpty()) return false;
        if (!CanInsert(index, stack, requester)) return false;
        if (!Inv.TryAddItemStack(stack)) return false;
        MarkDirty();
        return true;
    }

    public bool TryExtract(int index, int amount, ContainerAccess requester)
    {
        if (!CanExtract(index, requester)) return false;
        if (!Inv.TryConsumeItemAt(index, amount)) return false;
        MarkDirty();
        return true;
    }

    public ItemStack GetItemStackAt(int index)
    {
        return Inv.GetItemStackAt(index);
    }

    // ---- pack (P2) ----

    // Data-side code of one slot (the pack key); null when undeclared - panel
    // self-report and pack emission both skip undeclared slots.
    public string SlotCode(int index)
        => cfg.SlotCodes != null && index >= 0 && index < cfg.SlotCodes.Length ? cfg.SlotCodes[index] : null;

    // Pack format (paired with the UI-side group + ItemDataParser by format
    // only, no shared code): entries "code=payload" joined by ';'; payload is
    // "empty" for an empty slot, "itemId,amount" otherwise.
    public override string GetPackData()
    {
        if (cfg.SlotCodes == null) return null;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < Inv.MaxSlotCount; i++)
        {
            if (i > 0) sb.Append(';');
            sb.Append(cfg.SlotCodes[i]).Append('=');
            var stack = Inv.GetItemStackAt(i);
            if (stack == null || stack.IsEmpty()) sb.Append("empty");
            else sb.Append(stack.itemId).Append(',').Append(stack.amount);
        }
        return sb.ToString();
    }

    // ---- persistence ----

    // Slot data only: capacity, rules and codes are declaration config
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
