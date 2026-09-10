using UnityEngine;

public class CraftingTableUI : UIBehavior
{
    // Panel geometry as data (S3 + layout doc P1); every value equals the
    // former hardcoded literal, so the migration is visually zero-change.
    // The slot ids grid0..grid8 / result must equal the container-reported
    // data names (CraftingWorkContainer).
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

    private void Awake()
    {
        handles = PanelLayoutRunner.Build(this, Layout);
    }

    // Phase C: the open data is the value-only PanelData packet (no BE
    // reference). Every SetData re-binds all ten slots by name (S3): a slot
    // the packet lacks is listed by ValidatePanelData and nothing is bound,
    // so stale addresses can never linger. Preview refresh/clear on
    // open/close moved to the logic side (OpenPanel/ClosePanel commands).
    public override void SetData(object data)
    {
        if(data is not PanelData panel)return;
        if(!ValidatePanelData(panel, handles))return;
        for(int i = 0; i < 9; i++)handles.Bind($"grid{i}", panel);
        handles.Bind("result", panel);
    }

    public override void Refresh()
    {
        foreach(var slot in handles.Slots.Values)slot.Refresh();
    }

    // Per-frame mirror sweep while the panel is visible: captures the echo of
    // every click/drag settlement and the live 3x3 preview.
    private void Update()
    {
        Refresh();
    }

    public static UIDefinition craftingTableUIDefinition = new()
    {
        modId = "minecraft",
        name = "crafting_table",
        Kind = UIKind.SinglePanel,
        InputHandlerId = "minecraft:ui_input_handler",
        OpenWithPlayerInventory = true,
        Panel = new PanelDescriptor
        {
            Layout = Layout,
            ChannelNames = new string[0]   // the workbench container contributes no channels
        },
        Factory = () => {
            var go = new GameObject("Crafting Table UI", typeof(CraftingTableUI));
            return go;
        }
    };
}
