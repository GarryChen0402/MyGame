// Pure value snapshot of one slot: the UI reads itemId/amount for rendering
// and never touches a live ItemStack (rule R-C1-1: mirrors share no logic
// objects and carry no policy - what may enter/leave a slot is decided by the
// command settlement result, not by the mirror).
public struct SlotMirror
{
    public ushort itemId;
    public int amount;
    public bool IsEmpty => amount == 0;
}

// Read face shared by ContainerMirror (resident HUD/backpack) and PanelData
// (block-entity panel sessions): SlotUI binds against this and never knows
// which kind of source it renders.
public interface ISlotReadSource
{
    int Capacity { get; }
    SlotMirror GetSlot(int index);
}

// Container mirror: capacity fixed (identical to the source container),
// content a per-slot value copy. Version bumps whenever any slot content
// changed (rule R-C1-1). Pure data - no logic reference, no public writer
// (the only writer is MirrorSync).
public class ContainerMirror : ISlotReadSource
{
    public int Capacity { get; }
    public int Version { get; private set; }

    // Pack text of the source container (P2, design D3): a keyed string built
    // by the binding from the container's own pack contract; null until the
    // first pass (or for sources without a pack contract). The UI-side group
    // unpacks it into slot values.
    public string Pack { get; private set; }

    private readonly SlotMirror[] slots;
    private bool changed;

    public ContainerMirror(int capacity)
    {
        Capacity = capacity;
        slots = new SlotMirror[Capacity];
    }

    public SlotMirror GetSlot(int index) => slots[index];

    // Writer side (MirrorSync bindings only): record the value, remember the
    // change so one CommitChanged can bump the version once per sync pass.
    // Returns true when the value actually changed (drives the pack rebuild
    // in the binding, D5).
    public bool Apply(int index, ushort itemId, int amount)
    {
        if(index < 0 || index >= Capacity)return false;
        var s = slots[index];
        if(s.itemId == itemId && s.amount == amount)return false;
        slots[index] = new SlotMirror { itemId = itemId, amount = amount };
        changed = true;
        return true;
    }

    // Writer side (MirrorSync bindings only): store the pack snapshot; equal
    // text is dropped so a caller racing a rebuild never churns the string.
    public void ApplyPack(string pack)
    {
        if(Pack == pack)return;
        Pack = pack;
    }

    public void CommitChanged()
    {
        if(!changed)return;
        changed = false;
        Version++;
    }
}

// Cursor-held mirror (read view of Player.CursorStack): content + version.
// The UI renders the cursor item from here.
public class HeldMirror
{
    public int Version { get; private set; }
    public SlotMirror Content { get; private set; }

    // Value copy; version only bumps on a content change (drives the
    // version-gated held icon, so a pickup frame never renders stale).
    public void Apply(ItemStack source)
    {
        ushort id = 0;
        int amount = 0;
        if(source != null)
        {
            id = source.itemId;
            amount = source.amount;
        }
        if(Content.itemId == id && Content.amount == amount)return;
        Content = new SlotMirror { itemId = id, amount = amount };
        Version++;
    }
}
