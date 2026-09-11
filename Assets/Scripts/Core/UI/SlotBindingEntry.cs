using System.Collections.Generic;
using UnityEngine;

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

// UI-side parser of one slot's serialized payload. TryDecodeSlot receives the
// payload half of one pack entry ("code=payload", P2) and either yields the
// slot value or declines the payload.
public interface ISlotDataParser
{
    bool TryDecodeSlot(string payload, out SlotMirror value);
}

// Item payload parser: "empty" is an explicit empty slot; "itemId,amount" is
// a filled one. A malformed payload is declined with a one-shot warning per
// parser instance (the instance lives on the UIDefinition, so the warning
// fires once per panel type, never per frame).
public class ItemDataParser : ISlotDataParser
{
    private bool warned;

    public bool TryDecodeSlot(string payload, out SlotMirror value)
    {
        value = default;
        if(string.IsNullOrEmpty(payload))return false;
        if(payload == "empty")return true;
        int comma = payload.IndexOf(',');
        if(comma > 0
            && ushort.TryParse(payload.Substring(0, comma), out ushort itemId)
            && int.TryParse(payload.Substring(comma + 1), out int amount)
            && amount > 0)
        {
            value = new SlotMirror { itemId = itemId, amount = amount };
            return true;
        }
        if(!warned)
        {
            warned = true;
            Debug.LogWarning($"[ItemDataParser] malformed slot payload '{payload}', ignored");
        }
        return false;
    }
}

// Integer payload parser: channel payloads (e.g. EnergyDataContainer) never
// feed slot values, so it declines every slot decode; consumed once a
// channel-side unpacking path lands.
public class IntDataParser : ISlotDataParser
{
    public bool TryDecodeSlot(string payload, out SlotMirror value)
    {
        value = default;
        return false;
    }
}

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
