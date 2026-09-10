using System.Collections.Generic;

// Type-level parsing description (S3 of Docs/改造提案-UI数据流拆分方案.md):
// "given a PanelData, how does this UI read and lay it out". Pure data - the
// layout geometry plus the channel names the UI consumes; the rendering math
// stays in the UI class. Attached to the UIDefinition of a container panel.
public class PanelDescriptor
{
    public PanelLayout Layout;
    public string[] ChannelNames;

    // Open-time name-set check: every slot name the layout declares and every
    // channel name must appear in the packet's name tables, and vice versa.
    // Lists the differences so a mismatch names the offending ids instead of
    // letting a position-based bind land on the wrong cell.
    public bool Matches(PanelData data, out string mismatch)
    {
        var declaredSlots = new List<string>();
        if(Layout != null)foreach(var element in Layout.Elements)element.CollectSlotNames(declaredSlots);

        var missing = new List<string>();
        var extra = new List<string>();
        Diff(declaredSlots, data.SlotNames, missing, extra);
        Diff(ChannelNames, data.ChannelNames, missing, extra);

        if(missing.Count == 0 && extra.Count == 0)
        {
            mismatch = null;
            return true;
        }
        mismatch = $"missing [{string.Join(", ", missing)}] extra [{string.Join(", ", extra)}]";
        return false;
    }

    private static void Diff(IReadOnlyList<string> declared, IReadOnlyList<string> actual,
        List<string> missing, List<string> extra)
    {
        if(declared != null)
            foreach(var name in declared)
                if(!Contains(actual, name))missing.Add(name);
        if(actual != null)
            foreach(var name in actual)
                if(!Contains(declared, name))extra.Add(name);
    }

    private static bool Contains(IReadOnlyList<string> names, string name)
    {
        if(names == null)return false;
        for(int i = 0; i < names.Count; i++)
            if(names[i] == name)return true;
        return false;
    }
}
