using System;

// UI-side action resource (P5 of Docs/UI槽位编码与解析映射-实施文档.md): slot
// actions and UI-native actions (open/close panels...). Extends the legacy
// binding fields with the UI-action metadata of the design's §2.6: SlotTargeted
// marks slot-targeted actions (the slot actionIds gate applies from P6 on;
// global actions like open_inventory carry false and never enter the gate),
// and Callback is the response entry the UI action ring invokes after gating
// (P6). P5 declares the shape only - nothing consumes these fields yet.
public class UIAction : KeyBinding
{
    public bool SlotTargeted;
    public Action<UIActionContext> Callback;
}

// Minimal callback context (design §2.6 sub-detail 1): the target slot when
// the action is slot-targeted, null for global actions.
public class UIActionContext
{
    public SlotUI Slot;
}
