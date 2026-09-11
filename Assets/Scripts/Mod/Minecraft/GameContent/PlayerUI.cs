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
    private SlotUI[] gridSlots = new SlotUI[4];
    private SlotUI resultSlot = null;
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

    private void Awake()
    {
        var rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(920, 420);
        rt.localScale = new Vector3(0.8f, 0.8f, 0.8f);
        rt.localPosition += new Vector3(0, 180, 0);

        var bgGo = UIWidgetBackground.CreateNewBackground();
        bgGo.transform.SetParent(transform, false);

        // Preview area (left): square so the square RenderTexture never
        // stretches. Content spans [-415, 425] local so the two halves sit
        // balanced around the panel center.
        var previewGo = EntityModelPreviewUI.AddEntityModelPreview(
            "Player Model", new Vector3(-300f, 0f, 0f), new Vector2(240f * 1.8f, 240 * 1.8f),
            "minecraft:player", PlayerFaceTextures);
        previewGo.transform.SetParent(transform, false);

        // 2x2 crafting grid, row-major with rows top-to-bottom (index =
        // row*2+col), 110 px pitch; result slot to the right, workbench habit.
        // Phase C: display + click addresses bind the resident mirrors
        // (scope PlayerCrafting, canonical slot order: grid 0-3, result 4).
        // The binding entries come from the type-level descriptor (P2): they
        // feed the pack routing and never depend on uIDefinition injection.
        var ms = MirrorSync.Instance;
        var descriptor = playerUIDefinition.Panel;
        gridGroup = new InventoryUI();
        for(int i = 0; i < gridSlots.Length; i++)
        {
            var go = new GameObject($"Crafting Slot {i}");
            gridSlots[i] = go.AddComponent<SlotUI>();
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(85 + (i % 2) * 110, 110 - (i / 2) * 110, 0);
            gridSlots[i].BindInteractive(ms.PlayerCraftGridMirror, i,
                new SlotAddr { scope = SlotScope.PlayerCrafting, slot = i },
                descriptor.Resolve($"player_craft_grid_{i}"));
            gridGroup.Add(gridSlots[i]);
        }
        var resultGo = new GameObject("Crafting Result Slot");
        resultSlot = resultGo.AddComponent<SlotUI>();
        resultGo.transform.SetParent(transform, false);
        resultGo.transform.localPosition = new Vector3(375f, 55f, 0f);
        resultSlot.BindInteractive(ms.PlayerCraftResultMirror, 0,
            new SlotAddr { scope = SlotScope.PlayerCrafting, slot = gridSlots.Length },
            descriptor.Resolve("player_craft_result"));
        resultGroup = new InventoryUI();
        resultGroup.Add(resultSlot);
    }

    // Preview refresh on open moved to the logic side (OpenPlayerInventory);
    // the mirror sweep picks the result up within one render frame.
    public override void Refresh()
    {
        foreach(var slot in gridSlots)slot.Refresh();
        resultSlot.Refresh();
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
        // Resident binding carrier (P0, A9): the hand-written 2x2 grid and
        // result slots, codes written in code for now (moved to layout JSON
        // at L4). The grid and result containers report no names, so their
        // data codes are the write-in slot_<i> (grid: 0..3, result: 0).
        Panel = new PanelDescriptor
        {
            Bindings =
            {
                ["player_craft_grid_0"] = new SlotBindingEntry("slot_0", new ItemDataParser()).WithCoreActions(),
                ["player_craft_grid_1"] = new SlotBindingEntry("slot_1", new ItemDataParser()).WithCoreActions(),
                ["player_craft_grid_2"] = new SlotBindingEntry("slot_2", new ItemDataParser()).WithCoreActions(),
                ["player_craft_grid_3"] = new SlotBindingEntry("slot_3", new ItemDataParser()).WithCoreActions(),
                ["player_craft_result"] = new SlotBindingEntry("slot_0", new ItemDataParser()).WithCoreActions(),
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
