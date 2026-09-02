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

    public ItemStack HeldItemStack{get; set;} = new();
    public SlotUI CurrentHoverSlotUI {get; set;} = null;
}