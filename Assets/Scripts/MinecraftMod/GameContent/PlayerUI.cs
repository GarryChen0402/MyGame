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

    // Click access points of the 2x2 grid + the result slot (5 slots ->
    // shift-move direction and reachable-slot list come out right via the
    // base ContainerSlots, exactly like CraftingTableUI).
    private readonly List<ISlotAccess> containerSlots = new();
    public override IReadOnlyList<ISlotAccess> ContainerSlots => containerSlots;

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
        var player = Player.Instance;
        for(int i = 0; i < gridSlots.Length; i++)
        {
            var go = new GameObject($"Crafting Slot {i}");
            gridSlots[i] = go.AddComponent<SlotUI>();
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(85 + (i % 2) * 110, 110 - (i / 2) * 110, 0);
            var access = new CraftingGridSlotAccess(player.CraftingGrid, i, player);
            gridSlots[i].Bind(access);
            gridSlots[i].SetItemStack(access.Get());
            containerSlots.Add(access);
        }
        var resultGo = new GameObject("Crafting Result Slot");
        resultSlot = resultGo.AddComponent<SlotUI>();
        resultGo.transform.SetParent(transform, false);
        resultGo.transform.localPosition = new Vector3(375f, 55f, 0f);
        var resultAccess = new CraftingResultSlotAccess(player.CraftingResult, 0, player);
        resultSlot.Bind(resultAccess);
        resultSlot.SetItemStack(resultAccess.Get());
        containerSlots.Add(resultAccess);
    }

    private void OnEnable()
    {
        // OnDisable cleared the preview result on close; materials stay in
        // the grid, so re-match to show a still-matching recipe again.
        Player.Instance.Crafting?.RefreshPreview();
        Refresh();
    }

    public override void Refresh()
    {
        foreach(var slot in gridSlots)slot.Refresh();
        resultSlot.Refresh();
    }

    // UI close clears the virtual preview result (never-consumed materials)
    // so it cannot leak into the player save as a phantom stack.
    private void OnDisable()
    {
        if(Player.Instance?.Crafting != null)Player.Instance.Crafting.ClearPreview();
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
        Factory = () =>
        {
            var go = new GameObject("Player UI");
            go.AddComponent<PlayerUI>();
            return go;
        }
    };
}
