using System.Collections.Generic;
using UnityEngine;

// Shared item-icon render targets for the JEI list: one 64x64 RT per item,
// shared across every cell that shows it. Rendering is a synchronous camera
// render (ItemIconRenderSystem.RenderItemIcon), so requests are queued and
// pumped with a per-frame budget - the first fill of a full screen trickles
// in over a few frames instead of hitching one.
public class JEIIconCache : MonoBehaviour
{
    private const int IconSize = 64;
    private const int RendersPerFrame = 8;

    private readonly Dictionary<ushort, RenderTexture> cache = new();
    private readonly Queue<ushort> pending = new();
    private readonly HashSet<ushort> queued = new();

    // Cache lookup for the cell Update tick; a miss enqueues the render.
    // null until the icon is ready (the cell shows an empty slot meanwhile).
    public RenderTexture Get(ushort itemId)
    {
        if(itemId == JEICell.NoItem)return null;
        if(cache.TryGetValue(itemId, out var ready))return ready;
        if(queued.Add(itemId))pending.Enqueue(itemId);
        return null;
    }

    private void Update()
    {
        for(int i = 0; i < RendersPerFrame && pending.Count > 0; i++)
        {
            ushort itemId = pending.Dequeue();
            queued.Remove(itemId);
            if(cache.ContainsKey(itemId))continue;
            var rt = new RenderTexture(IconSize, IconSize, 0, RenderTextureFormat.ARGB32);
            rt.Create();
            ItemIconRenderSystem.RenderItemIcon(itemId, rt);
            cache[itemId] = rt;
        }
    }

    private void OnDestroy()
    {
        foreach(var rt in cache.Values)
        {
            rt.Release();
            Destroy(rt);
        }
        cache.Clear();
        pending.Clear();
        queued.Clear();
    }
}
