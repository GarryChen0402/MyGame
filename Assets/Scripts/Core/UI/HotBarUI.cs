using System.Collections.Generic;
using UnityEngine;

public class HotBarUI : UIBehavior
{
    private static HotBarUI instance = null;
    public static HotBarUI Instance => instance;

    // Panel geometry as data (L4 of Docs/可视化UI布局编辑器-实施文档.md): one 9-cell
    // row, scale 1 and no background - the former Awake literals. Width and
    // height only frame the editor canvas; absolutely positioned children are
    // unaffected by sizeDelta.
    public static readonly PanelLayout Layout = new()
    {
        Width = 920,
        Height = 100,
        Anchor = new(0.5f, 0),
        Offset = new(0, 50),
        Scale = 1,
        Background = null,
        Elements =
        {
            new GridSlotElement { IdPrefix = "hotbar", Rows = 1, Cols = 9, Pitch = 100, Origin = new(-400, 0) },
        }
    };

    private PanelLayoutHandles handles;
    [SerializeField]
    private List<SlotUI> itemIcons = new();

    private bool bound;
    private InventoryUI packGroup;

    private void Awake()
    {
        if(instance == null)instance = this;
        else Destroy(gameObject);
    }

    // Builds the row from the definition-borne layout (L4): UIManager calls
    // this right after injecting uIDefinition and before SetData. Not Awake:
    // at factory time the definition is not injected yet.
    public override void OnDefinitionReady()
    {
        handles = PanelLayoutRunner.Build(this, uIDefinition.Panel.Layout);
        itemIcons.Clear();
        itemIcons.AddRange(handles.SlotOrder);
        // Pack group (P4): the 9 HUD cells route the backpack container's
        // packed snapshot through their binding entries (data codes slot_<i>).
        packGroup = new InventoryUI();
        foreach(var slot in itemIcons)packGroup.Add(slot);
    }

    // Phase C: reads the resident backpack mirror instead of the live
    // inventory. SlotUI.Refresh diffs internally, so a per-frame sweep only
    // re-renders changed slots. Hotbar slots never bind interactive - they
    // stay unclickable, exactly like before. The pack pass (P4) consumes the
    // backpack container's packed snapshot; the binding entries come from the
    // type-level descriptor (P2/P4) and feed its code routing.
    private void Update()
    {
        var mirror = MirrorSync.Instance?.PlayerInventoryMirror;
        if(mirror == null)return;   // resident sources register when the player is created
        if(!bound)
        {
            var descriptor = hotbarDefinition.Panel;
            for(int i = 0; i < itemIcons.Count; i++)
                itemIcons[i].BindDisplay(mirror, i, descriptor.Resolve($"hotbar{i}"));
            bound = true;
        }
        packGroup.ApplyPack(0, mirror.Pack);
        for(int i = 0; i < itemIcons.Count; i++)itemIcons[i].Refresh();
    }

    public static UIDefinition hotbarDefinition = new()
    {
        modId = "minecraft",
        name = "hotbar",
        OpenWithPlayerInventory = false,
        Panel = BuildPanel(),
        Factory = () =>
        {
            var hotBarGo = new GameObject("HotBar");
            hotBarGo.AddComponent<HotBarUI>();
            return hotBarGo;
        }
    };

    // Resident binding carrier (P0, A9): 9 codes, written in code for now.
    // The HUD row and the backpack's bottom row map to the same container
    // cells; actionIds stay empty - the HUD is display-only today and R/U
    // arrive via JEI's PreFreeze injection (P7). The ids follow the layout
    // naming (L4: hotbar0..8), the data codes stay slot_<i>.
    private static PanelDescriptor BuildPanel()
    {
        var panel = new PanelDescriptor
        {
            Layout = PanelLayoutAssets.Load("UILayouts/hotbar", Layout)
        };
        for(int i = 0; i < 9; i++)
            panel.Bindings[$"hotbar{i}"] = new SlotBindingEntry($"slot_{i}", new ItemDataParser());
        return panel;
    }
}