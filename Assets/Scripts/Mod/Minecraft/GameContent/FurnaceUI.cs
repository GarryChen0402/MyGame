using UnityEngine;

public class FurnaceUI : UIBehavior
{
    // Panel geometry as data (S3 + layout doc P1): every value equals the
    // former hardcoded literal, so the migration is visually zero-change.
    // The slot ids are the alignment keys that must equal the container-
    // reported data names (ProcessingWorkContainer reports input/fuel/output).
    public static readonly PanelLayout Layout = new()
    {
        Elements =
        {
            new SlotElement { Id = "input", Pos = new(-120, 100) },
            new SlotElement { Id = "fuel", Pos = new(-120, -100) },
            new SlotElement { Id = "output", Pos = new(120, 0) },
            // Fire gauge: full when a fuel piece is lit, its visible top edge
            // sinks as the burn runs out (Progress 1 -> 0 keeps the bottom).
            new BarElement
            {
                Id = "fire", Pos = new(-120, 0), Dir = ProgressBarUI.Direction.BottomToTop,
                Front = "furnace_fire_front", Back = "furnace_fire_back"
            },
            // Cook gauge: the arrow fills left-to-right while a recipe cooks.
            new BarElement
            {
                Id = "cook", Pos = Vector2.zero, Dir = ProgressBarUI.Direction.LeftToRight,
                Front = "furnace_progress_front", Back = "furnace_progress_back"
            },
        }
    };

    private PanelLayoutHandles handles;
    private ProgressBarUI fireProgress = null;   // fuel burn gauge above the fuel slot
    private ProgressBarUI cookProgress = null;   // recipe progress arrow
    private PanelData data = null;               // data packet of the open session

    private void Awake()
    {
        handles = PanelLayoutRunner.Build(this, Layout);
        fireProgress = handles.Bars["fire"];
        cookProgress = handles.Bars["cook"];
    }

    // Phase C: the open data is the value-only PanelData packet (no BE
    // reference). Slots bind by name (S3), so the packet's contribution order
    // never matters; a descriptor/data mismatch is listed and refused.
    public override void SetData(object data)
    {
        if (data is not PanelData panel) return;
        this.data = panel;
        if (!ValidatePanelData(panel, handles)) return;
        handles.Bind("input", panel);
        handles.Bind("fuel", panel);
        handles.Bind("output", panel);
    }

    public static UIDefinition furanceUIDefinition = new()
    {
        modId = "minecraft",
        name = "furnace",
        Kind = UIKind.SinglePanel,
        InputHandlerId = "minecraft:ui_input_handler",
        OpenWithPlayerInventory = true,
        Panel = new PanelDescriptor
        {
            Layout = Layout,
            // Channel order mirrors ReadChannels: 0 lit remainder, 1 lit
            // total, 2 cook progress, 3 cook total (vanilla furnace order).
            ChannelNames = new[] { "fuel_left", "fuel_total", "cook_progress", "cook_total" }
        },
        Factory = () =>{
            var go = new GameObject("Furnace UI", typeof(FurnaceUI));
            return go;
        }
    };

    // Pushes the channel state into the two gauges while the panel is open
    // (Update stops when the UI is hidden): the sync layer copies the work
    // container's four tick ints into channels 0..3 every render frame, so
    // the gauges and slots always read the last settled tick.
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
        foreach(var slot in handles.Slots.Values)slot.Refresh();
    }
}
