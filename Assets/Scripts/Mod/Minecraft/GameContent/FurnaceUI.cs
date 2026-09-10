using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
// using UnityEngine.UIElements;

public class FurnaceUI : UIBehavior
{
    private SlotUI inputSlot = null;
    private SlotUI fuelSlot = null;
    private SlotUI outputSlot = null;
    private ProgressBarUI fireProgress = null;   // fuel burn gauge above the fuel slot
    private ProgressBarUI cookProgress = null;   // recipe progress arrow
    private PanelData data = null;               // data packet of the open session

    private void Awake()
    {
        var rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(920, 420);
        rt.localScale = new Vector3(0.8f, 0.8f, 0.8f);
        rt.localPosition += new Vector3(0, 160, 0);

        var bgGo = UIWidgetBackground.CreateNewBackground();
        bgGo.transform.SetParent(transform, false);

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

    // Phase C: the open data is the value-only PanelData packet (no BE
    // reference). Canonical slot order of the furnace data: 0 = input,
    // 1 = fuel, 2 = output; a shorter packet (future container configs)
    // leaves the remaining layout slots display-only and unclickable.
    public override void SetData(object data)
    {
        if (data is not PanelData panel) return;
        this.data = panel;
        BindSlot(inputSlot, panel, 0);
        BindSlot(fuelSlot, panel, 1);
        BindSlot(outputSlot, panel, 2);
    }

    private static void BindSlot(SlotUI slotUI, PanelData data, int slot)
    {
        bool bound = slot < data.Capacity;
        slotUI.BindInteractive(bound ? data : null, bound ? slot : 0, new SlotAddr
        {
            scope = SlotScope.Panel,
            panelModelId = data.SessionId,
            slot = slot
        });
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

    // Pushes the channel state into the two gauges while the panel is open
    // (Update stops when the UI is hidden): the sync layer copies the work
    // container's four tick ints into channels 0..3 every render frame, so
    // the gauges and slots always read the last settled tick. Channel order:
    // 0 = lit remainder, 1 = lit total, 2 = cook progress, 3 = cook total.
    private void Update()
    {
        if(data != null && data.ChannelCount >= 4)
        {
            fireProgress.Progress = data.GetChannel(0) <= 0 ? 0f
                : (float)data.GetChannel(0) / Mathf.Max(1, data.GetChannel(1));
            cookProgress.Progress = data.GetChannel(3) <= 0 ? 0f
                : (float)data.GetChannel(2) / data.GetChannel(3);
        }
        Refresh();
    }

    public override void Refresh()
    {
        inputSlot.Refresh();
        outputSlot.Refresh();
        fuelSlot.Refresh();
    }
}