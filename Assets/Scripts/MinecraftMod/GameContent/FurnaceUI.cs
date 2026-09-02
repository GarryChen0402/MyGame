using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
// using UnityEngine.UIElements;

public class FurnaceUI : UIBehavior
{
    private BlockEntity targetFurance = null;
    private SlotUI inputSlot = null;
    private SlotUI fuelSlot = null;
    private SlotUI outputSlot = null;
    private ProgressBarUI fireProgress = null;   // fuel burn gauge above the fuel slot
    private ProgressBarUI cookProgress = null;   // recipe progress arrow
    private ProcessingWorkContainer work = null; // work container of the bound BE

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

        // Fire Progress: full when a fuel piece is lit, its visible top edge
        // sinks as the burn runs out (Progress 1 -> 0 keeps the bottom part).
        // fireProgress = AddProgressBar("Fire Progress", new Vector3(-120, -25, 0),
        //     ProgressBarUI.Direction.BottomToTop,
        //     Resources.Load<Sprite>("Textures/UI/furnace_fire_front"),
        //     Resources.Load<Sprite>("Textures/UI/furnace_fire_back"));
        var fireProgressGo = ProgressBarUI.AddProgressBar(
            "Fire Progress", new Vector3(-120, 0, 0),
            ProgressBarUI.Direction.BottomToTop,
            Resources.Load<Sprite>("Textures/UI/furnace_fire_front"),
            Resources.Load<Sprite>("Textures/UI/furnace_fire_back")
        );
        fireProgressGo.transform.SetParent(transform, false);
        fireProgress = fireProgressGo.GetComponent<ProgressBarUI>();

        // Craft Progress: front grows left-to-right while a recipe cooks and
        // resets to back-only once the output is crafted.

        var craftProgressGo = ProgressBarUI.AddProgressBar("Cook Progress", Vector3.zero,
            ProgressBarUI.Direction.LeftToRight,
            Resources.Load<Sprite>("Textures/UI/furnace_progress_front"),
            Resources.Load<Sprite>("Textures/UI/furnace_progress_back"));
        craftProgressGo.transform.SetParent(transform, false);
        cookProgress = craftProgressGo.GetComponent<ProgressBarUI>();
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
        work = null;
        foreach(var wc in be.WorkContainers)
            if(wc is ProcessingWorkContainer pwc){ work = pwc; break; }
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

    // SlotUI-style: no explicit size - the GO's RectTransform defaults to the
    // 100x100 slot cell and the child images fill it (preserveAspect keeps the
    // small pixel textures from stretching).
    private ProgressBarUI AddProgressBar(string name, Vector3 pos, ProgressBarUI.Direction dir,
        Sprite front, Sprite back)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.AddComponent<RectTransform>();
        var bar = go.AddComponent<ProgressBarUI>();
        bar.Setup(front, dir, back);
        return bar;
    }

    // Pushes the work container's live tick state into the two gauges while
    // the panel is open (Update stops when the UI is hidden).
    private void Update()
    {
        if(work == null)return;
        fireProgress.Progress = work.FuelLeftTickTime <= 0 ? 0f
            : (float)work.FuelLeftTickTime / ProcessingWorkContainer.FUEL_BURN_TICKS;
        cookProgress.Progress = work.TotalTickTime <= 0 ? 0f
            : (float)work.CurrentTickProgress / work.TotalTickTime;
        inputSlot.Refresh();
        fuelSlot.Refresh();
        outputSlot.Refresh();
    }

    public override void Refresh()
    {
        inputSlot.Refresh();
        outputSlot.Refresh();
        fuelSlot.Refresh();
    }
}