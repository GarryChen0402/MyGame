using System.Collections.Generic;
using System.IO.Compression;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class PlayerInventoryUI : UIBehavior
{
    // Panel geometry as data (L4 of Docs/可视化UI布局编辑器-实施文档.md): the 36
    // cells split into two grids - the bottom hotbar row (cells 0-8) and the
    // 3x9 main grid - because the old loop starts at the bottom row and the
    // two segments sit 120 px apart while the row pitch is 100. Build order
    // matches the old cell order (hotbar row first), so SlotOrder[i] is
    // backpack cell i.
    public static readonly PanelLayout Layout = new()
    {
        Anchor = new(0.5f, 0.25f),
        Offset = new(0, 50),
        Elements =
        {
            new GridSlotElement { IdPrefix = "hotbar", Rows = 1, Cols = 9, Pitch = 100, Origin = new(-400, -160) },
            new GridSlotElement { IdPrefix = "inv", Rows = 3, Cols = 9, Pitch = 100, Origin = new(-400, 160) },
        }
    };

    private PanelLayoutHandles handles;
    private InventoryUI packGroup;

    // Builds the panel from the definition-borne layout (L4): UIManager's
    // EnsurePlayerInventoryRoot calls this right after injecting the
    // definition - the backpack root never passes through OpenUI. Not Awake:
    // at factory time the definition is not injected yet.
    public override void OnDefinitionReady()
    {
        handles = PanelLayoutRunner.Build(this, uIDefinition.Panel.Layout);
        // Pack group (P4): the 36 cells route the backpack container's packed
        // snapshot through their binding entries (data codes slot_<i>).
        packGroup = new InventoryUI();
        foreach(var slot in handles.SlotOrder)packGroup.Add(slot);
    }

    public static UIDefinition playerInvUIDefinition = new()
    {
        modId = "minecraft",
        name = "player_inventory",
        Kind = UIKind.PlayerInventory,
        OpenWithPlayerInventory = false,
        Panel = BuildPanel(),
        Factory = () =>
        {
            var go = new GameObject("Player Inventory");
            go.AddComponent<PlayerInventoryUI>();
            return go;
        }
    };

    // Resident binding carrier (P0, A9): 36 codes, written in code for now.
    // Every slot maps to its backpack container cell slot_<i>; the ids follow
    // the layout naming (L4: hotbar0..8 / inv0..26), the data codes stay put.
    private static PanelDescriptor BuildPanel()
    {
        var panel = new PanelDescriptor
        {
            Layout = PanelLayoutAssets.Load("UILayouts/player_inventory", Layout)
        };
        for(int i = 0; i < 36; i++)
            panel.Bindings[i < 9 ? $"hotbar{i}" : $"inv{i - 9}"] = new SlotBindingEntry($"slot_{i}", new ItemDataParser()).WithCoreActions();
        return panel;
    }

    // Phase C: display + click address bind to the resident backpack mirror
    // (addresses are resolved by ContainerCommandProcessor, no accessor is
    // held here). Binding is idempotent, so re-binding on every show keeps
    // the display in sync with the mirror registration. The binding entries
    // come from the type-level descriptor (P2/P4): they feed the pack routing
    // and never depend on uIDefinition injection.
    private void OnEnable()
    {
        // The factory-built root is active the moment it is created, before
        // UIManager injects the definition (which is what builds the slots) -
        // that first activation is a no-op. The root is deactivated right
        // after, and SetActive(true) on every open binds with everything
        // built. No display bind before the mirror exists: it registers when
        // the player is created, which may be after this panel is built
        // (UIManager.Awake pre-fabricates it) - reading Player here would
        // move the creation point earlier.
        if(handles == null)return;
        var mirror = MirrorSync.Instance?.PlayerInventoryMirror;
        packGroup.Reset();   // reopen: replay the pack from scratch (P2, D4)
        var descriptor = playerInvUIDefinition.Panel;
        for(int i = 0; i < handles.SlotOrder.Count; i++)
            handles.SlotOrder[i].BindInteractive(mirror, i, new SlotAddr { scope = SlotScope.PlayerInventory, slot = i },
                descriptor.Resolve(i < 9 ? $"hotbar{i}" : $"inv{i - 9}"));
    }

    // Refresh sweeps every slot to reflect merges / swaps / quick-moves.
    public override void Refresh()
    {
        foreach(var slot in handles.SlotOrder)slot.Refresh();
    }

    // Live sync while the panel is open: SlotUI.Refresh is nearly free when
    // nothing changed (it compares its last-rendered state against the mirror
    // and only re-renders the 3D icon on a difference), so a per-frame sweep
    // also picks up direct inventory writes - give commands, pickups,
    // crafting - that never pass through the click path. Unity skips Update
    // on inactive GameObjects, so this runs only while the panel is visible.
    // The pack pass (P4) consumes the backpack container's packed snapshot
    // before the sweep; a not-yet-registered mirror reads as no pack.
    private void Update()
    {
        packGroup.ApplyPack(0, MirrorSync.Instance?.PlayerInventoryMirror?.Pack);
        Refresh();
    }
}