using System.Collections.Generic;

// UI-side receiver of container packs (P2 of Docs/UI槽位编码与解析映射-实施文档.md,
// design D3): a group of SlotUIs fed by packed container snapshots. ApplyPack
// splits the text ("code=payload" entries joined by ';'), routes each entry to
// the slots whose binding target matches the code - the pack keys are
// data-side slot codes, so routing never depends on slot order - and hands
// the payload to the slot's parser. Entries without a matching slot are
// dropped silently (a container may report codes this panel does not show).
// Per-pack text caching skips the split when nothing changed (D5).
public class InventoryUI
{
    private readonly List<SlotUI> slots = new();
    private readonly List<string> appliedPacks = new();   // last text per pack index

    public void Add(SlotUI slot) => slots.Add(slot);

    // Reopen/rebind point (D4): drops the pack caches and every slot's pack
    // override so the next pass replays the packs from scratch.
    public void Reset()
    {
        appliedPacks.Clear();
        foreach(var slot in slots)slot.ClearValue();
    }

    public void ApplyPack(int index, string pack)
    {
        if(index < 0 || string.IsNullOrEmpty(pack))return;
        while(appliedPacks.Count <= index)appliedPacks.Add(null);
        if(appliedPacks[index] == pack)return;
        appliedPacks[index] = pack;

        foreach(var entry in pack.Split(';'))
        {
            int eq = entry.IndexOf('=');
            if(eq <= 0)continue;   // malformed entry (no code): dropped
            string code = entry.Substring(0, eq);
            string payload = entry.Substring(eq + 1);
            foreach(var slot in slots)
            {
                var binding = slot.BindingEntry;
                if(binding == null || binding.Target != code)continue;
                if(binding.Parser.TryDecodeSlot(payload, out var value))slot.ApplyValue(value);
            }
        }
    }
}
