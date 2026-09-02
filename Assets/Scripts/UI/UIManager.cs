using System.Collections.Generic;
using UnityEngine;

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
    private void Start()
    {
        if(ResourceSystem.Instance.UIDefinitions.TryGetResourceWithFullName("minecraft:player_inventory", out var def))
        {
            PlayerInventoryRoot = def.Factory();
            PlayerInventoryRoot.transform.SetParent(transform, false);
            PlayerInventoryRoot.SetActive(false);
        }

        OpenUI("minecraft:crosshair");
        OpenUI("minecraft:hotbar");

    }

    public void OpenUI(string uiId, object data = null)
    {
        if(!ResourceSystem.Instance.UIDefinitions.TryGetResourceWithFullName(uiId, out var uiDef))return;
        if(UICache.TryGetValue(uiId, out var ui))
        {
            if(uiDef.Kind == UIKind.SinglePanel)currentUI = ui;

            ui.SetData(data);
            ui.Open();

            if(uiDef.OpenWithPlayerInventory)PlayerInventoryRoot.SetActive(true);
            return;
        }

        var uiGo = uiDef.Factory();
        if(uiDef.Kind == UIKind.HUD)uiGo.transform.SetParent(HUDRoot.transform, false);
        else if(uiDef.Kind == UIKind.Tooltip)uiGo.transform.SetParent(TooltipRoot.transform, false);
        else uiGo.transform.SetParent(SinglePanelRoot.transform, false);
        
        currentUI = uiGo.GetComponent<UIBehavior>();
        currentUI.SetData(ui);
        currentUI.Open();
        if(uiDef.OpenWithPlayerInventory)PlayerInventoryRoot.SetActive(true);
        UICache[uiId] = currentUI;
    }

    public void CloseUI()
    {
        currentUI?.Close();
    }

    public ItemStack HeldItemStack{get; set;} = new();
    public SlotUI CurrentHoverSlotUI {get; set;} = null;
}