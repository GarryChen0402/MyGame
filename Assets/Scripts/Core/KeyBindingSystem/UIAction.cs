using System;
using UnityEngine.EventSystems;

// UI-side action resource (P5 of Docs/UI槽位编码与解析映射-实施文档.md): slot
// actions and UI-native actions (open/close panels...). Extends the legacy
// binding fields with the UI-action metadata of the design's §2.6: SlotTargeted
// marks slot-targeted actions (the slot actionIds gate applies from P6 on;
// global actions like open_inventory carry false and never enter the gate),
// and Callback is the response entry the UI action ring invokes after gating
// (P6: the ring in UIManager polls/locates actions and invokes these).
public class UIAction : KeyBinding
{
    public bool SlotTargeted;
    public Action<UIActionContext> Callback;
}

// Minimal callback context (design §2.6 sub-detail 1; fields detailed in P6):
// the target slot for slot-targeted actions (null for global ones); EventData
// is the pointer event of the triggering entry (null for key-driven actions).
public class UIActionContext
{
    public SlotUI Slot;
    public PointerEventData EventData;
}
