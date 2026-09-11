using UnityEngine;

public class FurnaceUI : UIBehavior
{
    // Panel geometry as data (S3 + layout doc P1): every value equals the
    // former hardcoded literal, so the migration is visually zero-change.
    // Slot ids are UI-side codes; the descriptor's explicit Bindings map each
    // to its container-reported data name (P0).
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
    private InventoryUI packGroup;               // one group over the whole panel: container packs route by code (P2)
    private ProgressBarUI fireProgress = null;   // fuel burn gauge above the fuel slot
    private ProgressBarUI cookProgress = null;   // recipe progress arrow
    private PanelData data = null;               // data packet of the open session

    private void Awake()
    {
        handles = PanelLayoutRunner.Build(this, Layout);
        fireProgress = handles.Bars["fire"];
        cookProgress = handles.Bars["cook"];
        packGroup = new InventoryUI();
        packGroup.Add(handles.Slots["input"]);
        packGroup.Add(handles.Slots["fuel"]);
        packGroup.Add(handles.Slots["output"]);
    }

    // Phase C: the open data is the value-only PanelData packet (no BE
    // reference). Slots bind through the descriptor's explicit mapping (P0),
    // so the packet's contribution order never matters; an undeclared slot or
    // missing target is warned about and left unbound.
    public override void SetData(object data)
    {
        if (data is not PanelData panel) return;
        this.data = panel;
        packGroup.Reset();   // reopen: replay the packs from scratch (P2, D4)
        if (!ValidatePanelData(panel, handles)) return;
        handles.Bind(uIDefinition.Panel, "input", panel);
        handles.Bind(uIDefinition.Panel, "fuel", panel);
        handles.Bind(uIDefinition.Panel, "output", panel);
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
            ChannelNames = new[] { "fuel_left", "fuel_total", "cook_progress", "cook_total" },
            // Explicit slot declarations (P0): ui code -> data code + parser
            // + responsive actions.
            Bindings =
            {
                ["input"] = new SlotBindingEntry("input", new ItemDataParser()).WithCoreActions(),
                ["fuel"] = new SlotBindingEntry("fuel", new ItemDataParser()).WithCoreActions(),
                ["output"] = new SlotBindingEntry("output", new ItemDataParser()).WithCoreActions(),
            }
        },
        Factory = () =>{
            var go = new GameObject("Furnace UI", typeof(FurnaceUI));
            return go;
        }
    };

    // Pushes the channel state into the two gauges while the panel is open
    // (Update stops when the UI is hidden): the sync layer copies the work
    // container's four tick ints into channels 0..3 every render frame, so
    // the gauges and slots always read the last settled tick. The container
    // packs ride along the same sweep (P2): one pack entry per container.
    private void Update()
    {
        if(data != null && data.ChannelCount >= 4)
        {
            fireProgress.Progress = data.GetChannel(0) <= 0 ? 0f
                : (float)data.GetChannel(0) / Mathf.Max(1, data.GetChannel(1));
            cookProgress.Progress = data.GetChannel(3) <= 0 ? 0f
                : (float)data.GetChannel(2) / data.GetChannel(3);
        }
        if(data != null)
            for(int i = 0; i < data.Packs.Count; i++)packGroup.ApplyPack(i, data.Packs[i]);
        Refresh();
    }

    public override void Refresh()
    {
        foreach(var slot in handles.Slots.Values)slot.Refresh();
    }
}
