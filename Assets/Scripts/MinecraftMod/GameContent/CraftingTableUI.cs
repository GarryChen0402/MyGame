using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class CraftingTableUI : UIBehavior
{
    private BlockEntity target = null;
    private CraftingWorkContainer work = null;   // work container of the bound BE
    private SlotUI[] gridSlots = new SlotUI[9];
    private SlotUI resultSlot = null;

    // Click access points of the 3x3 grid + the result slot, rebuilt whenever
    // the UI binds to a new block entity (10 slots -> shift-move direction
    // and reachable-slot list come out right via the base ContainerSlots).
    private readonly List<ISlotAccess> containerSlots = new();
    public override IReadOnlyList<ISlotAccess> ContainerSlots => containerSlots;

    private void Awake()
    {
        var rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(920, 420);
        rt.localScale = new Vector3(0.8f, 0.8f, 0.8f);
        rt.localPosition += new Vector3(0, 160, 0);

        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(transform, false);
        var image = bgGo.AddComponent<Image>();
        rt = bgGo.GetOrAddComponent<RectTransform>();
        image.type = Image.Type.Sliced;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        image.sprite = Resources.Load<Sprite>("Textures/UI/universal_bg");

        // 3x3 grid, row-major with rows top-to-bottom (index = row*3+col),
        // 110 px pitch around the left half; result slot on the right.
        for(int i = 0; i < gridSlots.Length; i++)
        {
            var go = new GameObject($"Grid Slot {i}");
            gridSlots[i] = go.AddComponent<SlotUI>();
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(-200 + (i % 3) * 110, (1 - i / 3) * 110, 0);
        }
        var resultGo = new GameObject("Result Slot");
        resultSlot = resultGo.AddComponent<SlotUI>();
        resultGo.transform.SetParent(transform, false);
        resultGo.transform.localPosition = new Vector3(200, 0, 0);
    }

    public override void SetData(object data)
    {
        if(data is not BlockEntity be)return;
        if(target == be)
        {
            work?.RefreshPreview();   // OnDisable cleared the preview on close
            Refresh();
            return;
        }
        containerSlots.Clear();
        target = be;
        work = null;
        foreach(var wc in be.WorkContainers)
            if(wc is CraftingWorkContainer cwc){ work = cwc; break; }
        if(work == null)return;

        for(int i = 0; i < gridSlots.Length; i++)
        {
            var access = new CraftingGridSlotAccess(work.Grid, i, work);
            gridSlots[i].Bind(access);
            gridSlots[i].SetItemStack(access.Get());
            containerSlots.Add(access);
        }
        var resultAccess = new CraftingResultSlotAccess(work.Result, 0, work);
        resultSlot.Bind(resultAccess);
        resultSlot.SetItemStack(resultAccess.Get());
        containerSlots.Add(resultAccess);

        // The grid persists across sessions/UI opens: rebuild the live preview
        // (grid containers only hold materials; the preview never saved).
        work.RefreshPreview();
        Refresh();
    }

    public override void Refresh()
    {
        foreach(var slot in gridSlots)slot.Refresh();
        resultSlot.Refresh();
    }

    // UI close clears the virtual preview result (never-consumed materials)
    // so a later block break or save cannot leak it into drops or disk.
    private void OnDisable() => work?.ClearPreview();

    public static UIDefinition craftingTableUIDefinition = new()
    {
        modId = "minecraft",
        name = "crafting_table",
        Kind = UIKind.SinglePanel,
        InputHandlerId = "minecraft:ui_input_handler",
        OpenWithPlayerInventory = true,
        Factory = () => {
            var go = new GameObject("Crafting Table UI", typeof(CraftingTableUI));
            return go;
        }
    };
}
