using System.Collections.Generic;
using UnityEngine;

// Element handles returned by PanelLayoutRunner.Build, keyed by element id.
public class PanelLayoutHandles
{
    public readonly Dictionary<string, SlotUI> Slots = new();
    public readonly Dictionary<string, ProgressBarUI> Bars = new();

    // Name-aligned binding (S3): resolve the id against the packet's frozen
    // name table and point the slot at the resolved index - runtime
    // addressing stays a plain int (SlotAddr). A name absent from the packet
    // leaves the slot display-only, so a binding can never land on the wrong
    // cell by position.
    public void Bind(string id, PanelData data)
    {
        if(!Slots.TryGetValue(id, out var slot))return;
        int index = data.SlotIndex(id);
        if(index < 0)return;
        slot.BindInteractive(data, index, new SlotAddr
        {
            scope = SlotScope.Panel,
            panelModelId = data.SessionId,
            slot = index
        });
    }

    // Clears every slot binding: a panel that refused to bind (name mismatch)
    // shows blank slots instead of stale values from a previous session.
    public void ClearBindings()
    {
        foreach(var slot in Slots.Values)slot.BindDisplay(null, 0);
    }
}

// Interpreter of one PanelLayout: builds the panel frame plus one GameObject
// per element and returns the handles. Panels build once in Awake - every
// number comes from the layout data.
public static class PanelLayoutRunner
{
    public static PanelLayoutHandles Build(UIBehavior host, PanelLayout layout)
    {
        UIPanelBuilder.BuildFrame(host, layout.Width, layout.Height, layout.Offset);
        var handles = new PanelLayoutHandles();
        foreach(var element in layout.Elements)
        {
            switch(element)
            {
                case SlotElement slot:
                    handles.Slots[slot.Id] = BuildSlot(host.transform, slot.Id, slot.Pos);
                    break;
                case GridSlotElement grid:
                    for(int i = 0; i < grid.Rows * grid.Cols; i++)
                    {
                        string id = grid.IdPrefix + i;
                        var pos = new Vector2(
                            grid.Origin.x + (i % grid.Cols) * grid.Pitch,
                            grid.Origin.y - (i / grid.Cols) * grid.Pitch);
                        handles.Slots[id] = BuildSlot(host.transform, id, pos);
                    }
                    break;
                case BarElement bar:
                    handles.Bars[bar.Id] = BuildBar(host.transform, bar);
                    break;
            }
        }
        return handles;
    }

    private static SlotUI BuildSlot(Transform parent, string id, Vector2 pos)
    {
        var go = new GameObject(id);
        var slot = go.AddComponent<SlotUI>();
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
        return slot;
    }

    private static ProgressBarUI BuildBar(Transform parent, BarElement bar)
    {
        var go = ProgressBarUI.AddProgressBar(bar.Id, new Vector3(bar.Pos.x, bar.Pos.y, 0f), bar.Dir,
            Resources.Load<Sprite>("Textures/UI/" + bar.Front),
            bar.Back == null ? null : Resources.Load<Sprite>("Textures/UI/" + bar.Back));
        go.transform.SetParent(parent, false);
        return go.GetComponent<ProgressBarUI>();
    }
}
