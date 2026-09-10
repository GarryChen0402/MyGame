// Instance-level data packet of one open block-entity panel session (S1 of
// Docs/改造提案-UI数据流拆分方案.md). The UI reads nothing else. Value types
// only (no object references, no Unity types), so a future network packet can
// fill the same structure: slot snapshots plus generic integer channels (the
// vanilla ContainerData analogue). The only writer is MirrorSync's panel
// bindings (rule R-C1-0); the UI face stays a pure read.
public class PanelData : ISlotReadSource
{
    public int SessionId { get; internal set; }   // session identity (= the former PanelModel.ModelId)
    public readonly SlotMirror[] Slots;           // value array; slot order = container contribution order
    public readonly int[] Channels;               // generic integer channels; channel semantics live in the UI class

    public int Version { get; private set; }
    private bool changed;

    public PanelData(int sessionId, int slotCount, int channelCount)
    {
        SessionId = sessionId;
        Slots = new SlotMirror[slotCount];
        Channels = new int[channelCount];
    }

    public int Capacity => Slots.Length;
    public int ChannelCount => Channels.Length;

    public SlotMirror GetSlot(int index) => Slots[index];
    public int GetChannel(int index) => Channels[index];

    // Writer side (MirrorSync panel bindings only): record the value, remember
    // the change so one CommitChanged bumps the version once per sync pass.
    internal void ApplySlot(int index, ushort itemId, int amount)
    {
        if(index < 0 || index >= Slots.Length)return;
        var s = Slots[index];
        if(s.itemId == itemId && s.amount == amount)return;
        Slots[index] = new SlotMirror { itemId = itemId, amount = amount };
        changed = true;
    }

    internal void ApplyChannel(int index, int value)
    {
        if(index < 0 || index >= Channels.Length)return;
        if(Channels[index] == value)return;
        Channels[index] = value;
        changed = true;
    }

    internal void CommitChanged()
    {
        if(!changed)return;
        changed = false;
        Version++;
    }
}

// One channel source: a logic-side reader that fills a PanelData channel
// segment with its integer state (the furnace tick counters ride here - the
// former FurnaceProgressView, now generic channels). Implemented by work
// containers; the semantics of each channel index live in the UI class.
public interface IChannelSource
{
    int ChannelCount { get; }
    void ReadChannels(PanelData data, int start);
}
