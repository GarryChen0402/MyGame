using System.Collections.Generic;
using UnityEngine;

// Element handles returned by PanelLayoutRunner.Build, keyed by element id.
public class PanelLayoutHandles
{
    public readonly Dictionary<string, SlotUI> Slots = new();
    public readonly Dictionary<string, ProgressBarUI> Bars = new();

    // Explicit-mapping binding (P0): resolve the UI-side code through the
    // descriptor's dictionary, address the packet by the resolved data code
    // and hand the entry (parser + action ids) to the slot. A code without an
    // entry, or a target the packet lacks, leaves the slot display-only - a
    // binding can never land on the wrong cell by position.
    public void Bind(PanelDescriptor descriptor, string id, PanelData data)
    {
        if(!Slots.TryGetValue(id, out var slot))return;
        var entry = descriptor?.Resolve(id);
        if(entry == null)return;
        int index = data.SlotIndex(entry.Target);
        if(index < 0)return;
        slot.BindInteractive(data, index, new SlotAddr
        {
            scope = SlotScope.Panel,
            beId = data.BeId,
            slot = index
        }, entry);
    }

    // Clears every slot binding: a panel that refused to bind (name mismatch)
    // shows blank slots instead of stale values from a previous session.
    public void ClearBindings()
    {
        foreach(var slot in Slots.Values)slot.BindDisplay(null, 0);
    }
}

// Interpreter of one PanelLayout: builds the panel frame plus one GameObject
// per element and returns the handles. Panels build once from
// UIBehavior.OnDefinitionReady (after UIManager injected the definition, so
// the layout may come from a JSON asset) - every number comes from the
// layout data.
public static class PanelLayoutRunner
{
    public static PanelLayoutHandles Build(UIBehavior host, PanelLayout layout)
    {
        UIPanelBuilder.BuildFrame(host, layout);
        var handles = new PanelLayoutHandles();
        foreach(var element in layout.Elements)
        {
            switch(element)
            {
                case SlotElement slot:
                    handles.Slots[slot.Id] = BuildSlot(host.transform, slot.Id, slot.Pos, slot.Frame);
                    break;
                case GridSlotElement grid:
                    for(int i = 0; i < grid.Rows * grid.Cols; i++)
                    {
                        string id = grid.IdPrefix + i;
                        var pos = new Vector2(
                            grid.Origin.x + (i % grid.Cols) * grid.Pitch,
                            grid.Origin.y - (i / grid.Cols) * grid.Pitch);
                        handles.Slots[id] = BuildSlot(host.transform, id, pos, null);
                    }
                    break;
                case BarElement bar:
                    handles.Bars[bar.Id] = BuildBar(host.transform, bar);
                    break;
            }
        }
        return handles;
    }

    private static SlotUI BuildSlot(Transform parent, string id, Vector2 pos, SpriteRef frame)
    {
        var go = new GameObject(id);
        var slot = go.AddComponent<SlotUI>();
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
        if(frame != null)slot.SetFrame(UISprites.Resolve(frame.Sprite, "minecraft:slot"), frame.Tint);
        return slot;
    }

    private static ProgressBarUI BuildBar(Transform parent, BarElement bar)
    {
        var go = ProgressBarUI.AddProgressBar(bar.Id, new Vector3(bar.Pos.x, bar.Pos.y, 0f), bar.Dir,
            bar.Front == null ? null : UISprites.Resolve(bar.Front.Sprite),
            bar.Back == null ? null : UISprites.Resolve(bar.Back.Sprite));
        go.transform.SetParent(parent, false);
        return go.GetComponent<ProgressBarUI>();
    }
}
