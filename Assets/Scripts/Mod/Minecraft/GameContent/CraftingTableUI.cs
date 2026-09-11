using UnityEngine;

public class CraftingTableUI : UIBehavior
{
    // Panel geometry as data (S3 + layout doc P1); every value equals the
    // former hardcoded literal, so the migration is visually zero-change.
    // Slot ids grid0..grid8 / result are UI-side codes; the descriptor's
    // explicit Bindings map each to its container-reported data name (P0).
    // L1: this static layout is the C# fallback for the JSON asset
    // (UILayouts/crafting_table) and the export source (Tools/UI Layout).
    public static readonly PanelLayout Layout = new()
    {
        Elements =
        {
            // 3x3 grid, row-major with rows top-to-bottom, 110 px pitch around
            // the left half; result slot on the right.
            new GridSlotElement
            {
                IdPrefix = "grid", Rows = 3, Cols = 3,
                Pitch = UIStyle.SlotPitchCompact, Origin = new(-200, 110)
            },
            new SlotElement { Id = "result", Pos = new(200, 0) },
        }
    };

    private PanelLayoutHandles handles;
    private InventoryUI packGroup;   // one group over the whole panel: grid and result packs route by code (P2)
    private PanelData data = null;   // data packet of the open session

    // Builds the panel from the definition-borne layout (L1): UIManager calls
    // this right after injecting uIDefinition and before SetData, so the
    // JSON-resolved layout (UIDefs registration) is what gets built. Not
    // Awake - at factory time the definition is not injected yet.
    public override void OnDefinitionReady()
    {
        handles = PanelLayoutRunner.Build(this, uIDefinition.Panel.Layout);
        packGroup = new InventoryUI();
        for(int i = 0; i < 9; i++)packGroup.Add(handles.Slots[$"grid{i}"]);
        packGroup.Add(handles.Slots["result"]);
    }

    // Phase C: the open data is the value-only PanelData packet (no BE
    // reference). Every SetData re-binds all ten slots through the
    // descriptor's explicit mapping (P0): an undeclared slot or missing
    // target is warned about and stays unbound, so stale addresses can never
    // linger. Preview refresh/clear on open/close moved to the logic side
    // (OpenPanel/ClosePanel commands).
    public override void SetData(object data)
    {
        if(data is not PanelData panel)return;
        this.data = panel;
        packGroup.Reset();   // reopen: replay the packs from scratch (P2, D4)
        if(!ValidatePanelData(panel, handles))return;
        for(int i = 0; i < 9; i++)handles.Bind(uIDefinition.Panel, $"grid{i}", panel);
        handles.Bind(uIDefinition.Panel, "result", panel);
    }

    public override void Refresh()
    {
        foreach(var slot in handles.Slots.Values)slot.Refresh();
    }

    // Per-frame mirror sweep while the panel is visible: captures the echo of
    // every click/drag settlement and the live 3x3 preview. The container
    // packs ride along the same sweep (P2).
    private void Update()
    {
        if(data != null)
            for(int i = 0; i < data.Packs.Count; i++)packGroup.ApplyPack(i, data.Packs[i]);
        Refresh();
    }

    public static UIDefinition craftingTableUIDefinition = UIDefs.SinglePanel<CraftingTableUI>(
        "crafting_table", "Crafting Table UI", "UILayouts/crafting_table", Layout, new PanelDescriptor
        {
            ChannelNames = new string[0],   // the workbench container contributes no channels
            // Explicit slot declarations (P0): ui code -> data code + parser
            // + responsive actions.
            Bindings =
            {
                ["grid0"] = new SlotBindingEntry("grid0", new ItemDataParser()).WithCoreActions(),
                ["grid1"] = new SlotBindingEntry("grid1", new ItemDataParser()).WithCoreActions(),
                ["grid2"] = new SlotBindingEntry("grid2", new ItemDataParser()).WithCoreActions(),
                ["grid3"] = new SlotBindingEntry("grid3", new ItemDataParser()).WithCoreActions(),
                ["grid4"] = new SlotBindingEntry("grid4", new ItemDataParser()).WithCoreActions(),
                ["grid5"] = new SlotBindingEntry("grid5", new ItemDataParser()).WithCoreActions(),
                ["grid6"] = new SlotBindingEntry("grid6", new ItemDataParser()).WithCoreActions(),
                ["grid7"] = new SlotBindingEntry("grid7", new ItemDataParser()).WithCoreActions(),
                ["grid8"] = new SlotBindingEntry("grid8", new ItemDataParser()).WithCoreActions(),
                ["result"] = new SlotBindingEntry("result", new ItemDataParser()).WithCoreActions(),
            }
        });
}
