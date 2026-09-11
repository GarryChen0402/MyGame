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
    private GameObject TooltipRoot = null;

    private Dictionary<string, UIBehavior> UICache = new();

    private void Awake()
    {
        if(instance != null)Destroy(gameObject);
        else instance = this;

        HUDRoot = UIPanelBuilder.BuildStretchRoot(transform, "HUD").gameObject;
        SinglePanelRoot = UIPanelBuilder.BuildStretchRoot(transform, "SinglePanel").gameObject;
        // Not registered yet during stepper-driven startup (L1) - the root is
        // built lazily at first use instead (Part B §5.4).
        EnsurePlayerInventoryRoot();

        TooltipRoot = UIPanelBuilder.BuildStretchRoot(transform, "Tool tip").gameObject;

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

    private UIBehavior currentUI = null;
    private bool CurrentUIhasInputHandler = false;
    // Data packet of the panel UI this manager opened (data as PanelData);
    // the close command needs its session id to tear down the BE session
    // (logic side). null = no session (player UI / widget test / editors).
    private PanelData currentPanel;
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

    // Mod-facing seam: builds a new stretch root between the panel layer and
    // the tooltip / loading layers (mod overlay content such as JEI renders
    // above panels but below the cursor and topmost layers). Mods call this
    // once from their own initialization; the returned root is theirs to host.
    public GameObject CreateOverlayRoot(string name)
    {
        var root = UIPanelBuilder.BuildStretchRoot(transform, name).gameObject;
        if(TooltipRoot != null)root.transform.SetSiblingIndex(TooltipRoot.transform.GetSiblingIndex());
        return root;
    }

    public void OpenUI(string uiId, object data = null)
    {
        if(!ResourceSystem.Instance.UIDefinitions.TryGetResourceWithFullName(uiId, out var uiDef))return;
        // ResourceSystem.Instance.InputHandlers.TryGetResourceWithFullName(uiDef.InputHandlerId, out var inputHandler);
        if(UICache.TryGetValue(uiId, out var ui))
        {
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
            return;
        }

        var uiGo = uiDef.Factory();
        if(uiDef.Kind == UIKind.HUD)uiGo.transform.SetParent(HUDRoot.transform, false);
        else if(uiDef.Kind == UIKind.Tooltip)uiGo.transform.SetParent(TooltipRoot.transform, false);
        else uiGo.transform.SetParent(SinglePanelRoot.transform, false);

        currentUI = uiGo.GetComponent<UIBehavior>();
        currentUI.uIDefinition = uiDef;   // the panel's registration data (Panel descriptor rides here)
        currentUI.SetData(data);
        currentUI.Open();
        if(uiDef.OpenWithPlayerInventory)
        {
            EnsurePlayerInventoryRoot();
            PlayerInventoryRoot?.SetActive(true);
        }
        CurrentUIhasInputHandler = InputHandlerManager.Instance.TryPush(uiDef.InputHandlerId);
        if(uiDef.Kind == UIKind.SinglePanel)currentPanel = data as PanelData;
        UICache[uiId] = currentUI;
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

    // Entry point of every slot click (SlotUI.OnPointerClick → here): gate
    // the click, then settle it as a command (rule R-C1-0b). No same-frame
    // explicit refresh: the mirror sync + per-frame panel sweep echo the
    // change within one render frame (the approved ≤1-frame semantics).
    public void HandleSlotClicked(SlotUI slotUI, PointerEventData eventData)
    {
        if(currentUI == null)return;
        var addr = slotUI == null ? null : slotUI.Addr;
        if(addr == null)return;   // display-only slot: not clickable
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool rightClick = eventData.button == PointerEventData.InputButton.Right;
        var processor = ContainerCommandProcessor.Instance;
        if(shift)processor.QuickMove(addr.Value);
        else processor.Click(addr.Value, rightClick);
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
        if(activeDrag != null)PollPointer();
    }

    internal void HandleDragBegin(SlotUI slotUI, PointerEventData eventData)
    {
        if(currentUI == null)return;
        var addr = slotUI == null ? null : slotUI.Addr;
        if(addr == null)return;   // display-only slot: not draggable
        if(activeDrag != null)return;   // one drag at a time

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
            bool picked = processor.Click(addr.Value,
                eventData.button == PointerEventData.InputButton.Right);
            if(!picked)return;   // nothing to pick: no session
        }

        activeDrag = new DragSession
        {
            Right = eventData.button == PointerEventData.InputButton.Right
        };
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