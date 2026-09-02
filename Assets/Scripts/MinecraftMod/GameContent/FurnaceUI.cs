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
        targetFurance = be;
        var input = be.GetDataContainer<InventoryDataContainer>("input");
        if(input != null)inputSlot.SetItemStack(input.GetItemStackAt(0));
        var fuel = be.GetDataContainer<InventoryDataContainer>("fuel");
        if(fuel != null)fuelSlot.SetItemStack(fuel.GetItemStackAt(0));
        var output = be.GetDataContainer<InventoryDataContainer>("output");
        if(output != null)outputSlot.SetItemStack(output.GetItemStackAt(0));
    }

    public static UIDefinition furanceUIDefinition = new()
    {
        modId = "minecraft",
        name = "furnace",
        Kind = UIKind.SinglePanel,
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