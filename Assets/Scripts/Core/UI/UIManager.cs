using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIManager : MonoBehaviour
{
    private static UIManager instance = null;
    public static UIManager Instance => instance;
    private GameObject HUDRoot = null;
    private GameObject PlayerInventoryRoot = null;
    private GameObject SinglePanelRoot = null;
    private GameObject OverlayRoot = null;
    private GameObject TooltipRoot = null;

    private Dictionary<string, UIBehavior> UICache = new();

    private void Awake()
    {
        if(instance != null)Destroy(gameObject);
        else instance = this;

        HUDRoot = UIPanelBuilder.BuildStretchRoot(transform, "HUD").gameObject;
        SinglePanelRoot = UIPanelBuilder.BuildStretchRoot(transform, "SinglePanel").gameObject;
        TooltipRoot = UIPanelBuilder.BuildStretchRoot(transform, "Tool tip").gameObject;
        // Coexistence layer (P8): above the panel layer, below the player
        // inventory and tooltip layers and the topmost loading overlay.
        OverlayRoot = UIPanelBuilder.BuildStretchRoot(transform, "Overlay").gameObject;
        OverlayRoot.transform.SetSiblingIndex(TooltipRoot.transform.GetSiblingIndex());
        // Not registered yet during stepper-driven startup (L1) - the root is
        // built lazily at first use instead (Part B §5.4). Created after
        // TooltipRoot so its lazy insert-point anchor (below Tooltip) holds.
        EnsurePlayerInventoryRoot();

        // Topmost overlay layer (added last = renders above every other root of
        // this canvas): hosts the loading backdrop of the async enter-world
        // gate (Part B §5.1). Built here - not through the registry - so any
        // session stage can call LoadingOverlay.Show/Hide.
        var overlayRt = UIPanelBuilder.BuildStretchRoot(transform, "LoadingOverlay");
        overlayRt.gameObject.AddComponent<LoadingOverlay>();
        overlayRt.gameObject.SetActive(false);
    }


    // Lazy player-inventory root (Part B §5.4): during stepper-driven startup
    // the UI definitions are not registered yet when this scene's Awake runs,
    // so a missing root is expected then. Every consumer builds it on first
    // use - UI opens only ever happen after bootstrap completion, so the
    // first-use build is equivalent to the old eager one.
    private void EnsurePlayerInventoryRoot()
    {
        if(PlayerInventoryRoot != null)return;
        if(!ResourceSystem.Instance.UIDefinitions.TryGetResourceWithFullName("minecraft:player_inventory", out var def))return;
        PlayerInventoryRoot = def.Factory();
        PlayerInventoryRoot.transform.SetParent(transform, false);
        // Lazy creation would append above every existing root (including the
        // tooltip / loading layers); insert below TooltipRoot so the mouse-
        // following layers and the topmost overlay keep rendering on top.
        if(TooltipRoot != null)PlayerInventoryRoot.transform.SetSiblingIndex(TooltipRoot.transform.GetSiblingIndex());
        PlayerInventoryRoot.SetActive(false);
    }

    // In-game HUD is no longer opened on Start: the session controller opens
    // it when entering a world, so the menu state stays HUD-free.
    public void OpenGameHUD()
    {
        OpenUI("minecraft:crosshair");
        OpenUI("minecraft:hotbar");
        // Tooltip below the cursor-held item (opened last): the item being
        // carried must never be covered by hover text.
        OpenUI("minecraft:tooltip");
        OpenUI("minecraft:held_item");
    }

    private UIBehavior currentUI = null;
    private bool CurrentUIhasInputHandler = false;
    // Data packet of the panel UI this manager opened (data as PanelData);
    // the close command needs its session id to tear down the BE session
    // (logic side). null = no session (player UI / widget test / editors).
    private PanelData currentPanel;

    // Returns the built (or cached) behaviour instance so callers that need a
    // typed handle (e.g. the JEI panel, P8) do not have to re-find it.
    public UIBehavior OpenUI(string uiId, object data = null)
    {
        if(!ResourceSystem.Instance.UIDefinitions.TryGetResourceWithFullName(uiId, out var uiDef))return null;
        // ResourceSystem.Instance.InputHandlers.TryGetResourceWithFullName(uiDef.InputHandlerId, out var inputHandler);
        if(UICache.TryGetValue(uiId, out var ui))
        {
            // Overlay stays outside the single-panel session: reopening it is
            // an idempotent no-op (P8), so CloseUI keeps tracking its own UI.
            if(uiDef.Kind == UIKind.SinglePanel)currentUI = ui;

            ui.SetData(data);
            ui.Open();
            if(uiDef.OpenWithPlayerInventory)
            {
                EnsurePlayerInventoryRoot();
                PlayerInventoryRoot?.SetActive(true);
            }
            CurrentUIhasInputHandler = InputHandlerManager.Instance.TryPush(uiDef.InputHandlerId);
            // if(inputHandler != null)InputHandlerManager.Instance.Push(inputHandler);
            if(uiDef.Kind == UIKind.SinglePanel)currentPanel = data as PanelData;
            return ui;
        }

        var uiGo = uiDef.Factory();
        switch(uiDef.Kind)
        {
            case UIKind.HUD: uiGo.transform.SetParent(HUDRoot.transform, false); break;
            case UIKind.Tooltip: uiGo.transform.SetParent(TooltipRoot.transform, false); break;
            // Coexistence layer (P8): a persistent panel that pays no single-
            // panel semantics - currentUI/currentPanel are deliberately left
            // untouched (see the cache-hit branch below).
            case UIKind.Overlay: uiGo.transform.SetParent(OverlayRoot.transform, false); break;
            default: uiGo.transform.SetParent(SinglePanelRoot.transform, false); break;
        }

        var built = uiGo.GetComponent<UIBehavior>();
        built.uIDefinition = uiDef;   // the panel's registration data (Panel descriptor rides here)
        built.OnDefinitionReady();    // definition ready, data not yet injected (L1): panels build their layout here
        built.SetData(data);
        built.Open();
        if(uiDef.OpenWithPlayerInventory)
        {
            EnsurePlayerInventoryRoot();
            PlayerInventoryRoot?.SetActive(true);
        }
        // Overlay never joins the single-panel session (P8): it is a coexisting
        // layer, not the UI CloseUI later tears down.
        if(uiDef.Kind != UIKind.Overlay)
        {
            currentUI = built;
            CurrentUIhasInputHandler = InputHandlerManager.Instance.TryPush(uiDef.InputHandlerId);
            if(uiDef.Kind == UIKind.SinglePanel)currentPanel = data as PanelData;
        }
        UICache[uiId] = built;
        return built;
    }

    public void CloseUI()
    {
        CancelDrag();   // panel closed mid-drag: drop the session untouched
        // Panel close command first (logic side): BE sessions run their close
        // action and deregister their mirror bindings (rule R-C1-0b).
        if(currentPanel != null)ContainerCommandProcessor.Instance.ClosePanel(currentPanel.BeId);
        currentPanel = null;
        currentUI?.Close();
        if(currentUI != null && CurrentUIhasInputHandler)
        {
            InputHandlerManager.Instance.Pop();
            CurrentUIhasInputHandler = false;
        }
        currentUI = null;
        if(PlayerInventoryRoot != null)PlayerInventoryRoot.SetActive(false);
    }

    // Entry point of every slot click (SlotUI.OnPointerClick → here): resolve
    // the gesture to a core slot ui_action, gate it against the slot's
    // declared actionIds (P6 ring) and route it to the action's callback,
    // which settles it as a command (rule R-C1-0b). No same-frame explicit
    // refresh: the mirror sync + per-frame panel sweep echo the change within
    // one render frame (the approved ≤1-frame semantics).
    public void HandleSlotClicked(SlotUI slotUI, PointerEventData eventData)
    {
        if(currentUI == null)return;
        var addr = slotUI == null ? null : slotUI.Addr;
        if(addr == null)return;   // display-only slot: not clickable
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool rightClick = eventData.button == PointerEventData.InputButton.Right;
        string actionId = shift
            ? (rightClick ? CoreSlotActions.ShiftRightClick : CoreSlotActions.ShiftLeftClick)
            : (rightClick ? CoreSlotActions.RightClick : CoreSlotActions.LeftClick);
        InvokeSlotAction(actionId, slotUI, eventData);
    }

    // ---- UI action ring (P6 of Docs/UI槽位编码与解析映射-实施文档.md) ----

    // Key-driven ui_actions poll here (single point): press → slot gate (only
    // slot-targeted actions; a hovered slot must declare the id) → response
    // callback. Callback-less actions stay with their legacy pollers (P6: the
    // three open-entry actions in PlayerInputHandler). No hovered slot at all
    // is outside the gate's reach (P7: consumers filter their own hover
    // domain, e.g. JEI cells) - such a press still routes to the callback.
    private void PollUIActions()
    {
        var keys = KeyBindingManager.Instance;
        var actions = ResourceSystem.Instance.UIActions;
        for(ushort id = 0; id < actions.Count; id++)
        {
            if(!actions.TryGetResourceWithNumberId(id, out var action))continue;
            if(action.Callback == null)continue;
            if(!keys.WasPressed(action.FullName))continue;
            if(action.SlotTargeted)
            {
                var hover = CurrentHoverSlotUI;
                if(hover != null)
                {
                    var entry = hover.BindingEntry;
                    if(entry == null || !entry.ActionIds.Contains(action.FullName))continue;
                }
                action.Callback(new UIActionContext { Slot = hover });
            }
            else action.Callback(new UIActionContext());
        }
    }

    // Resolve → gate → route for pointer entries: the slot must declare the
    // action id on its binding entry (P0 declarations), then the action's own
    // callback carries the response.
    private void InvokeSlotAction(string actionId, SlotUI slot, PointerEventData eventData)
    {
        if(!ResourceSystem.Instance.UIActions.TryGetResourceWithFullName(actionId, out var action))return;
        if(action.SlotTargeted)
        {
            var entry = slot.BindingEntry;
            if(entry == null || !entry.ActionIds.Contains(actionId))return;
        }
        action.Callback?.Invoke(new UIActionContext { Slot = slot, EventData = eventData });
    }

    // ---- drag-to-distribute session (Docs/物品拖拽分配交互实现方案.md §4) ----

    // Active while a drag is in progress: the cursor stack is the source, and
    // every slot hovered over (deduped) receives one settlement on release.
    // Slot contents never change mid-drag, so collection order and validity
    // are exactly what DragEnd settles over. Hovered slots keep their SlotUI
    // so the session can drive the accept/reject outlines; the carried stack
    // itself lives on the logic side (Player.CursorStack).
    private class DragSession
    {
        public bool Right;
        public readonly List<SlotUI> TargetUis = new();
        public SlotUI Rejected;                      // hovered but cannot receive (red outline)
    }

    private DragSession activeDrag = null;
    private readonly PointerEventData dragPointerData = new(EventSystem.current);
    private readonly List<RaycastResult> dragRaycastResults = new();

    private void Update()
    {
        PollUIActions();
        if(activeDrag != null)PollPointer();
    }

    internal void HandleDragBegin(SlotUI slotUI, PointerEventData eventData)
    {
        if(currentUI == null)return;
        var addr = slotUI == null ? null : slotUI.Addr;
        if(addr == null)return;   // display-only slot: not draggable
        if(activeDrag != null)return;   // one drag at a time
        InvokeSlotAction(CoreSlotActions.Drag, slotUI, eventData);
    }

    // drag_action's built-in response (see the core registrations): open the
    // distribution session, picking the source up first when the cursor is
    // empty. Invoked back through the ring after the gate passed.
    public void BeginSlotDrag(SlotUI slot, PointerEventData eventData)
    {
        var addr = slot.Addr;
        if(addr == null)return;   // display-only slot: not draggable
        bool right = eventData != null && eventData.button == PointerEventData.InputButton.Right;
        var processor = ContainerCommandProcessor.Instance;
        var heldMirror = MirrorSync.Instance.PlayerHeldMirror;
        bool cursorEmpty = heldMirror == null || heldMirror.Content.IsEmpty;
        if(cursorEmpty)
        {
            // One-gesture pick-up: an empty cursor picks the source slot up
            // (left = whole, right = half) so a press-and-drag works like
            // vanilla; the emptied source slot never receives the spread. The
            // command returns whether the cursor now holds items - the mirror
            // would only reflect the pickup on the next frame.
            bool picked = processor.Click(addr.Value, right);
            if(!picked)return;   // nothing to pick: no session
        }

        activeDrag = new DragSession { Right = right };
        PollPointer();
    }

    internal void HandleDragEnd(SlotUI slotUI, PointerEventData eventData)
    {
        if(activeDrag == null)return;
        PollPointer();                     // final poll: the release position joins the collection
        var session = activeDrag;
        activeDrag = null;
        ClearDragHighlights(session);
        if(session.TargetUis.Count == 0)return;   // never hovered a receivable slot: keep the items
        var targets = new List<SlotAddr>(session.TargetUis.Count);
        foreach(var targetUi in session.TargetUis)targets.Add(targetUi.Addr.Value);
        ContainerCommandProcessor.Instance.DragEnd(targets, session.Right);
    }

    // Drops the session without touching any slot data (panel closed mid-drag:
    // held items are handled by the regular close cleanup).
    private void CancelDrag()
    {
        if(activeDrag == null)return;
        ClearDragHighlights(activeDrag);
        activeDrag = null;
    }

    private static void ClearDragHighlights(DragSession session)
    {
        foreach(var ui in session.TargetUis)ui.SetDragHighlight(SlotUI.DragHighlight.None);
        session.Rejected?.SetDragHighlight(SlotUI.DragHighlight.None);
        session.Rejected = null;
    }

    // While a drag is active, Unity stops routing pointer enter/exit to other
    // elements, so hover detection must raycast manually every frame. Slot
    // icons are child graphics and sort ahead of the slot image, so resolve
    // the first SlotUI ancestor instead of trusting the topmost result.
    private void PollPointer()
    {
        if(activeDrag == null)return;
        if(EventSystem.current == null)return;
        dragPointerData.position = Input.mousePosition;
        dragRaycastResults.Clear();
        EventSystem.current.RaycastAll(dragPointerData, dragRaycastResults);
        SlotUI hover = null;
        foreach(var result in dragRaycastResults)
        {
            hover = result.gameObject.GetComponentInParent<SlotUI>();
            if(hover != null)break;
        }

        // Clear the previous reject outline once the pointer left that slot.
        var prevRejected = activeDrag.Rejected;
        if(prevRejected != null && prevRejected != hover)
        {
            prevRejected.SetDragHighlight(SlotUI.DragHighlight.None);
            activeDrag.Rejected = null;
        }
        CurrentHoverSlotUI = hover;                    // hover slot during drag (null = outside)

        if(hover == null || hover.Addr == null)return;           // outside any operable slot
        if(activeDrag.TargetUis.Contains(hover))return;          // already accepted: outline stays

        // Receive check goes through the query command - mirrors carry no
        // policy, so the drag validation must ask the logic side.
        if(!ContainerCommandProcessor.Instance.CanReceive(hover.Addr.Value))
        {
            if(activeDrag.Rejected != hover)
            {
                activeDrag.Rejected = hover;
                hover.SetDragHighlight(SlotUI.DragHighlight.Reject);
            }
            return;
        }
        activeDrag.TargetUis.Add(hover);
        hover.SetDragHighlight(SlotUI.DragHighlight.Accept);
    }

    public SlotUI CurrentHoverSlotUI {get; set;} = null;

    // Hover feed of the general tooltip layer, written by SlotUI / JEI cells
    // (independent of CurrentHoverSlotUI above, which settles drags only).
    public IHoverItemSource CurrentHoverInfo {get; set;} = null;

    // Drag in progress: Unity stops routing enter/exit mid-drag, so the hover
    // feed is stale by design - the tooltip hides itself while this is true.
    public bool DragActive => activeDrag != null;
}