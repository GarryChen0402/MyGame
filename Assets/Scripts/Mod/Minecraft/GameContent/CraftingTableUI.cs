using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class CraftingTableUI : UIBehavior
{
    private SlotUI[] gridSlots = new SlotUI[9];
    private SlotUI resultSlot = null;

    private void Awake()
    {
        var rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(920, 420);
        rt.localScale = new Vector3(0.8f, 0.8f, 0.8f);
        rt.localPosition += new Vector3(0, 160, 0);

        var bgGo = UIWidgetBackground.CreateNewBackground();
        bgGo.transform.SetParent(transform, false);

        // 3x3 grid, row-major with rows top-to-bottom (index = row*3+col),
        // 110 px pitch around the left half; result slot on the right.
        for (int i = 0; i < gridSlots.Length; i++)
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

    // Phase C: the open data is the value-only PanelData packet (no BE
    // reference). Canonical slot order of the workbench data: 0..8 grid
    // cells, 9 result. Every SetData re-binds all ten slots: slots beyond
    // the packet (or a slot left over from a previous session) go
    // display-only, so stale addresses can never linger. Preview refresh/
    // clear on open/close moved to the logic side (OpenPanel/ClosePanel
    // commands).
    public override void SetData(object data)
    {
        if(data is not PanelData panel)return;
        for(int i = 0; i < gridSlots.Length; i++)
        {
            bool bound = i < panel.Capacity;
            gridSlots[i].BindInteractive(bound ? panel : null, bound ? i : 0, new SlotAddr
            {
                scope = SlotScope.Panel,
                panelModelId = panel.SessionId,
                slot = i
            });
        }
        int resultIndex = gridSlots.Length;
        bool resultBound = resultIndex < panel.Capacity;
        resultSlot.BindInteractive(resultBound ? panel : null, resultBound ? resultIndex : 0, new SlotAddr
        {
            scope = SlotScope.Panel,
            panelModelId = panel.SessionId,
            slot = resultIndex
        });
    }

    public override void Refresh()
    {
        foreach(var slot in gridSlots)slot.Refresh();
        resultSlot.Refresh();
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
        Factory = () => {
            var go = new GameObject("Crafting Table UI", typeof(CraftingTableUI));
            return go;
        }
    };
}
