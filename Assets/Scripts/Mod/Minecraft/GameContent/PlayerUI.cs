using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.VisualScripting;

// Player UI (design doc Docs/玩家界面-模型预览组件与2x2个人合成设计方案.md
// §5): opens with E. Upper area = the 3D player model preview (left half)
// and the 2x2 personal crafting grid with its result slot (right half,
// workbench arrangement); the lower 36-slot backpack is the shared
// PlayerInventoryUI panel attached via OpenWithPlayerInventory - the same
// two-panel structure the workbench UI uses.
public class PlayerUI : UIBehavior
{
    // Panel geometry as data (L4 of Docs/可视化UI布局编辑器-实施文档.md): the 2x2
    // grid and the result slot ride the layout; the 3D model preview stays
    // code-created (design D7: no preview element type yet) and is built in
    // OnDefinitionReady right after the layout.
    public static readonly PanelLayout Layout = new()
    {
        Offset = new(0, 180),
        Elements =
        {
            new GridSlotElement { IdPrefix = "craft", Rows = 2, Cols = 2, Pitch = 110, Origin = new(85, 110) },
            new SlotElement { Id = "result", Pos = new(375, 55) },
        }
    };

    private PanelLayoutHandles handles;
    // Two groups isolate the two container packs (P2, D3): the grid and the
    // result containers both key their slots 'slot_0', so one group per
    // container keeps the code routing unambiguous.
    private InventoryUI gridGroup;
    private InventoryUI resultGroup;

    // Placeholder skin mapping until the player gains a real skin texture; all
    // six faces share one registered texture (same setup as EntityVisualTest).
    private static readonly Dictionary<string, string> PlayerFaceTextures = new()
    {
        ["top"] = "minecraft:firefly",
        ["bottom"] = "minecraft:firefly",
        ["front"] = "minecraft:firefly",
        ["back"] = "minecraft:firefly",
        ["left"] = "minecraft:firefly",
        ["right"] = "minecraft:firefly"
    };

    // Builds the panel from the definition-borne layout (L4): UIManager calls
    // this right after injecting uIDefinition and before SetData. Not Awake:
    // at factory time the definition is not injected yet.
    public override void OnDefinitionReady()
    {
        handles = PanelLayoutRunner.Build(this, uIDefinition.Panel.Layout);

        // Preview area (left): square so the square RenderTexture never
        // stretches. Content spans [-415, 425] local so the two halves sit
        // balanced around the panel center.
        var previewGo = EntityModelPreviewUI.AddEntityModelPreview(
            "Player Model", new Vector3(-300f, 0f, 0f), new Vector2(240f * 1.8f, 240 * 1.8f),
            "minecraft:player", PlayerFaceTextures);
        previewGo.transform.SetParent(transform, false);

        // Phase C: display + click addresses bind the resident mirrors
        // (scope PlayerCrafting, canonical slot order: grid 0-3, result 4).
        // The binding entries come from the type-level descriptor (P2): they
        // feed the pack routing and never depend on uIDefinition injection.
        var ms = MirrorSync.Instance;
        var descriptor = playerUIDefinition.Panel;
        gridGroup = new InventoryUI();
        for(int i = 0; i < 4; i++)
        {
            var slot = handles.Slots[$"craft{i}"];
            slot.BindInteractive(ms.PlayerCraftGridMirror, i,
                new SlotAddr { scope = SlotScope.PlayerCrafting, slot = i },
                descriptor.Resolve($"craft{i}"));
            gridGroup.Add(slot);
        }
        var resultSlot = handles.Slots["result"];
        resultSlot.BindInteractive(ms.PlayerCraftResultMirror, 0,
            new SlotAddr { scope = SlotScope.PlayerCrafting, slot = 4 },
            descriptor.Resolve("result"));
        resultGroup = new InventoryUI();
        resultGroup.Add(resultSlot);
    }

    // Preview refresh on open moved to the logic side (OpenPlayerInventory);
    // the mirror sweep picks the result up within one render frame.
    public override void Refresh()
    {
        foreach(var slot in handles.SlotOrder)slot.Refresh();
    }

    // Per-frame mirror sweep while the panel is visible: captures the echo of
    // every click/drag settlement and the live 2x2 preview. Each group also
    // consumes its own container pack (P2); a not-yet-registered mirror reads
    // as no pack and simply skips.
    private void Update()
    {
        var ms = MirrorSync.Instance;
        gridGroup.ApplyPack(0, ms.PlayerCraftGridMirror?.Pack);
        resultGroup.ApplyPack(0, ms.PlayerCraftResultMirror?.Pack);
        Refresh();
    }

    public static UIDefinition playerUIDefinition = new()
    {
        modId = "minecraft",
        name = "player_ui",
        Kind = UIKind.SinglePanel,
        InputHandlerId = "minecraft:ui_input_handler",
        // The panel carries only the upper area (model + 2x2 grid); the 36-slot
        // backpack rides along as the shared lower panel (see class comment).
        OpenWithPlayerInventory = true,
        // Resident binding carrier (P0, A9): the 2x2 grid and result codes,
        // written in code for now; the ids follow the layout naming (L4:
        // craft0..3 / result). The grid and result containers report no
        // names, so their data codes are the write-in slot_<i> (grid: 0..3,
        // result: 0).
        Panel = new PanelDescriptor
        {
            Layout = PanelLayoutAssets.Load("UILayouts/player_ui", Layout),
            Bindings =
            {
                ["craft0"] = new SlotBindingEntry("slot_0", new ItemDataParser()).WithCoreActions(),
                ["craft1"] = new SlotBindingEntry("slot_1", new ItemDataParser()).WithCoreActions(),
                ["craft2"] = new SlotBindingEntry("slot_2", new ItemDataParser()).WithCoreActions(),
                ["craft3"] = new SlotBindingEntry("slot_3", new ItemDataParser()).WithCoreActions(),
                ["result"] = new SlotBindingEntry("slot_0", new ItemDataParser()).WithCoreActions(),
            }
        },
        Factory = () =>
        {
            var go = new GameObject("Player UI");
            go.AddComponent<PlayerUI>();
            return go;
        }
    };
}
