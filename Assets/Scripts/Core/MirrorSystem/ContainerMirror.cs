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

// Container mirror: capacity fixed (identical to the source container),
// content a per-slot value copy. Version bumps whenever any slot content
// changed (rule R-C1-1). Pure data - no logic reference, no public writer
// (the only writer is MirrorSync).
public class ContainerMirror
{
    public readonly int Capacity;
    public int Version { get; private set; }

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
    public void Apply(int index, ushort itemId, int amount)
    {
        if(index < 0 || index >= Capacity)return;
        var s = slots[index];
        if(s.itemId == itemId && s.amount == amount)return;
        slots[index] = new SlotMirror { itemId = itemId, amount = amount };
        changed = true;
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
