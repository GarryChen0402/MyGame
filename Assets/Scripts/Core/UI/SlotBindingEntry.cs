using System.Collections.Generic;

// One slot's explicit binding declaration (P0 of Docs/UI槽位编码与解析映射-实施文档.md,
// design §2.3): maps the UI-side slot code to the data-side slot code, names
// the parser that reads the slot's serialized payload, and carries the
// ui_action ids this slot responds to. Type-level: one entry serves every
// instance of the owning UIDefinition. The layout slot id is the dictionary
// key (PanelDescriptor.Bindings) - never inferred from either code.
public class SlotBindingEntry
{
    public string Target;
    public ISlotDataParser Parser;
    public readonly HashSet<string> ActionIds = new();

    public SlotBindingEntry(string target, ISlotDataParser parser)
    {
        Target = target;
        Parser = parser;
    }

    // Declares the five core slot actions (click pair, drag, shift pair).
    public SlotBindingEntry WithCoreActions()
    {
        foreach(var action in CoreSlotActions.All)ActionIds.Add(action);
        return this;
    }
}

// UI-side parser of one slot's serialized payload. P0 only carries the
// declaration; the read contract is consumed from P2 on (item payload ->
// itemId/amount, channel payload -> int).
public interface ISlotDataParser { }
public class ItemDataParser : ISlotDataParser { }
public class IntDataParser : ISlotDataParser { }

// Core slot ui_action ids (design §2.6); registered as ui_action resources
// in P6, declared by core slots' binding entries here.
public static class CoreSlotActions
{
    public const string LeftClick = "minecraft:left_click_action";
    public const string RightClick = "minecraft:right_click_action";
    public const string Drag = "minecraft:drag_action";
    public const string ShiftLeftClick = "minecraft:shift_left_click_action";
    public const string ShiftRightClick = "minecraft:shift_right_click_action";

    public static readonly string[] All = { LeftClick, RightClick, Drag, ShiftLeftClick, ShiftRightClick };
}
