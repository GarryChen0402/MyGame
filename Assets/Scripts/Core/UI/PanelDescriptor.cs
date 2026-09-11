using System.Collections.Generic;

// Type-level parsing description (S3 of Docs/改造提案-UI数据流拆分方案.md):
// "given a PanelData, how does this UI read and lay it out". Pure data - the
// layout geometry, the channel names the UI consumes, and the explicit
// uiCode -> binding-entry dictionary (P0 of Docs/UI槽位编码与解析映射-实施文档.md);
// the rendering math stays in the UI class. Attached to the UIDefinition of
// a container panel (Layout stays null for hand-written resident UIs), ready
// at registration time and read-only afterwards.
public class PanelDescriptor
{
    public PanelLayout Layout;
    public string[] ChannelNames;

    // UI-side slot code (layout element id) -> declaration. Declared, never
    // inferred: a layout slot with no entry warns at open time and stays
    // unbound (A4); duplicate codes are refused by the open-time validation.
    public readonly Dictionary<string, SlotBindingEntry> Bindings = new();

    public SlotBindingEntry Resolve(string uiCode)
        => Bindings.TryGetValue(uiCode, out var entry) ? entry : null;
}
