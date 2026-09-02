using System.Collections.Generic;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;
using UnityEngine.UI;

public class FurnaceUI : UIBehavior
{
    private BlockEntity targetFurance = null;
    private SlotUI inputSlot = null;
    private SlotUI fuelSlot = null;
    private SlotUI outputSlot = null;

    // Click access points of this furnace's three containers (input/fuel/
    // output), rebuilt whenever the UI binds to a new block entity.
    private readonly List<ISlotAccess> containerSlots = new();
    public override IReadOnlyList<ISlotAccess> ContainerSlots => containerSlots;

    private void Awake()
    {
        var rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(920, 420);
        rt.localScale = new Vector3(0.8f, 0.8f, 0.8f);
        rt.localPosition += new Vector3(0, 160, 0);

        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(transform, false);
        var image = bgGo.AddComponent<Image>();
        rt = bgGo.GetOrAddComponent<RectTransform>();
        image.type = Image.Type.Sliced;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        image.sprite = Resources.Load<Sprite>("Textures/UI/universal_bg");

        //-120 220
        var inputGo = new GameObject("Input Slot");
        inputSlot = inputGo.AddComponent<SlotUI>();
        inputGo.transform.SetParent(transform, false);
        inputGo.transform.localPosition = new Vector3(-120, 100, 0);

        // Fuel Slot UI : -120 90
        var fuelGo = new GameObject("Fuel Slot");
        fuelSlot = fuelGo.AddComponent<SlotUI>();
        fuelGo.transform.SetParent(transform, false);
        fuelGo.transform.localPosition = new Vector3(-120, -100, 0);
        
        // Output Slot UI :+120 155
        var outputGo = new GameObject("Output Slot");
        outputSlot = outputGo.AddComponent<SlotUI>();
        outputGo.transform.SetParent(transform, false);
        outputGo.transform.localPosition = new Vector3(120, 0, 0);

    }

    public override void SetData(object data)
    {
        if (data is not BlockEntity be) return;
        if(targetFurance == be)
        {
            Refresh();
            return;
        }
        containerSlots.Clear();
        targetFurance = be;
        BindContainerSlot("input", inputSlot);
        BindContainerSlot("fuel", fuelSlot);
        BindContainerSlot("output", outputSlot);
    }

    // Resolves one named container of the BE, binds the slot's click access
    // point and shows its content (same slot object the click logic mutates).
    private void BindContainerSlot(string name, SlotUI slotUI)
    {
        var container = targetFurance.GetDataContainer<InventoryDataContainer>(name);
        if(container == null)return;
        var access = new ContainerSlotAccess(container, 0);
        slotUI.Bind(access);
        slotUI.SetItemStack(access.Get());
        containerSlots.Add(access);
    }

    public static UIDefinition furanceUIDefinition = new()
    {
        modId = "minecraft",
        name = "furnace",
        Kind = UIKind.SinglePanel,
        InputHandlerId = "minecraft:ui_input_handler",
        OpenWithPlayerInventory = true,
        Factory = () =>{
            var go = new GameObject("Furnace UI", typeof(FurnaceUI));
            return go;
        }
    };

    public override void Refresh()
    {
        inputSlot.Refresh();
        outputSlot.Refresh();
        fuelSlot.Refresh();
    }
}