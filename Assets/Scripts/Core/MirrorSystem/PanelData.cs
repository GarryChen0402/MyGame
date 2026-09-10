using System.Collections.Generic;

// Instance-level data packet of one open block-entity panel session (S1 of
// Docs/改造提案-UI数据流拆分方案.md). The UI reads nothing else. Value types
// only (no object references, no Unity types), so a future network packet can
// fill the same structure: slot snapshots plus generic integer channels (the
// vanilla ContainerData analogue). The only writer is MirrorSync's panel
// bindings (rule R-C1-0); the UI face stays a pure read.
public class PanelData : ISlotReadSource
{
    public int SessionId { get; internal set; }   // session identity (= the former PanelModel.ModelId)
    public readonly string[] SlotNames;           // alignment names (container self-report); index order = contribution order
    public readonly SlotMirror[] Slots;           // value array; slot order = container contribution order
    public readonly string[] ChannelNames;        // channel alignment names, contribution order
    public readonly int[] Channels;               // generic integer channels; channel semantics live in the UI class

    public int Version { get; private set; }
    private bool changed;

    public PanelData(int sessionId, IReadOnlyList<string> slotNames, IReadOnlyList<string> channelNames)
    {
        SessionId = sessionId;
        SlotNames = CopyNames(slotNames);
        ChannelNames = CopyNames(channelNames);
        Slots = new SlotMirror[SlotNames.Length];
        Channels = new int[ChannelNames.Length];
    }

    private static string[] CopyNames(IReadOnlyList<string> names)
    {
        var copy = new string[names.Count];
        for(int i = 0; i < copy.Length; i++)copy[i] = names[i];
        return copy;
    }

    public int Capacity => Slots.Length;
    public int ChannelCount => Channels.Length;

    // Name -> index lookup, frozen at open time (the layout description and
    // the UI bind by name in S3); -1 when the name is absent.
    public int SlotIndex(string name)
    {
        for(int i = 0; i < SlotNames.Length; i++)
            if(SlotNames[i] == name)return i;
        return -1;
    }

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
