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

        HUDRoot = new GameObject("HUD");
        HUDRoot.transform.SetParent(transform);
        var rt = HUDRoot.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        SinglePanelRoot = new GameObject("SinglePanel");
        SinglePanelRoot.transform.SetParent(transform);
        rt = SinglePanelRoot.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        // rt.
        // PlayerInventoryRoot = new GameObject("Player Inventory");
        // PlayerInventoryRoot.transform.SetParent(transform);
        if(ResourceSystem.Instance.UIDefinitions.TryGetResourceWithFullName("minecraft:player_inventory", out var def))
        {
            PlayerInventoryRoot = def.Factory();
            PlayerInventoryRoot.transform.SetParent(transform, false);
            PlayerInventoryRoot.SetActive(false);
        }


        TooltipRoot = new GameObject("Tool tip");
        TooltipRoot.transform.SetParent(transform);
        rt = TooltipRoot.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }


    private UIBehavior currentUI = null;
    private bool CurrentUIhasInputHandler = false;
    private void Start()
    {
        // if(ResourceSystem.Instance.UIDefinitions.TryGetResourceWithFullName("minecraft:player_inventory", out var def))
        // {
        //     PlayerInventoryRoot = def.Factory();
        //     PlayerInventoryRoot.transform.SetParent(transform, false);
        //     PlayerInventoryRoot.SetActive(false);
        // }

        OpenUI("minecraft:crosshair");
        OpenUI("minecraft:hotbar");
        OpenUI("minecraft:held_item");

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
            if(uiDef.OpenWithPlayerInventory)PlayerInventoryRoot.SetActive(true);
            CurrentUIhasInputHandler = InputHandlerManager.Instance.TryPush(uiDef.InputHandlerId);
            // if(inputHandler != null)InputHandlerManager.Instance.Push(inputHandler);
            return;
        }

        var uiGo = uiDef.Factory();
        if(uiDef.Kind == UIKind.HUD)uiGo.transform.SetParent(HUDRoot.transform, false);
        else if(uiDef.Kind == UIKind.Tooltip)uiGo.transform.SetParent(TooltipRoot.transform, false);
        else uiGo.transform.SetParent(SinglePanelRoot.transform, false);
        
        currentUI = uiGo.GetComponent<UIBehavior>();
        currentUI.SetData(data);
        currentUI.Open();
        if(uiDef.OpenWithPlayerInventory)PlayerInventoryRoot.SetActive(true);
        CurrentUIhasInputHandler = InputHandlerManager.Instance.TryPush(uiDef.InputHandlerId);
        UICache[uiId] = currentUI;
    }

    public void CloseUI()
    {
        CancelDrag();   // panel closed mid-drag: drop the session untouched
        currentUI?.Close();
        if(currentUI != null && CurrentUIhasInputHandler)
        {
            InputHandlerManager.Instance.Pop();
            CurrentUIhasInputHandler = false;
        }
        currentUI = null;
        if(PlayerInventoryRoot != null)PlayerInventoryRoot.SetActive(false);
    }

    // Entry point of every slot click (SlotUI.OnPointerClick → here): gate the
    // click, resolve it against the held stack, then refresh every open panel.
    public void HandleSlotClicked(SlotUI slotUI, PointerEventData eventData)
    {
        if(currentUI == null)return;
        if(slotUI == null || slotUI.Access == null)return;   // display-only slot: not clickable
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool rightClick = eventData.button == PointerEventData.InputButton.Right;
        if(shift)SlotClickProcessor.QuickMove(slotUI.Access, currentUI.ContainerSlots);
        else SlotClickProcessor.Click(slotUI.Access, rightClick, HeldItemStack);
        RefreshVisibleSlots();
    }

    // Refresh the main panel and the linked player inventory panel after a
    // click; both Refresh()s rebind from the same slot objects the click
    // mutated, so a full sweep is simpler than tracking changed slots.
    private void RefreshVisibleSlots()
    {
        currentUI?.Refresh();
        if(PlayerInventoryRoot != null && PlayerInventoryRoot.TryGetComponent<UIBehavior>(out var invUi))
            invUi.Refresh();
    }

    // ---- drag-to-distribute session (Docs/物品拖拽分配交互实现方案.md §4) ----

    // Active while a drag is in progress: the source is the held stack, and
    // every slot hovered over (deduped) receives one settlement on release.
    // Slot contents never change mid-drag, so collection order and validity
    // are exactly what DragEnd settles over. Hovered slots keep their
    // SlotUI so the session can drive the accept/reject outlines.
    private class DragSession
    {
        public bool Right;
        public ItemStack Carried;                    // UIManager.HeldItemStack reference
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
        if(slotUI == null || slotUI.Access == null)return;   // display-only slot: not draggable
        if(activeDrag != null)return;                        // one drag at a time

        var carried = HeldItemStack;
        if(carried == null || carried.IsEmpty())
        {
            // One-gesture pick-up: an empty cursor picks the source slot up
            // (left = whole, right = half) so a press-and-drag works like
            // vanilla; the emptied source slot never receives the spread.
            var src = slotUI.Access;
            var t = src.Get();
            if(t == null || t.IsEmpty() || !src.CanTake())return;   // nothing to pick: no session
            SlotClickProcessor.Click(src, eventData.button == PointerEventData.InputButton.Right, carried);
            RefreshVisibleSlots();
        }
        if(carried == null || carried.IsEmpty())return;

        activeDrag = new DragSession
        {
            Right = eventData.button == PointerEventData.InputButton.Right,
            Carried = carried
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
        var accesses = new List<ISlotAccess>(session.TargetUis.Count);
        foreach(var targetUi in session.TargetUis)accesses.Add(targetUi.Access);
        SlotClickProcessor.DragEnd(accesses, session.Right, session.Carried);
        RefreshVisibleSlots();
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

        if(hover == null || hover.Access == null)return;          // outside any operable slot
        if(activeDrag.TargetUis.Contains(hover))return;           // already accepted: outline stays

        if(!SlotClickProcessor.CanReceive(hover.Access, activeDrag.Carried))
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

    public ItemStack HeldItemStack{get; set;} = new();
    public SlotUI CurrentHoverSlotUI {get; set;} = null;
}