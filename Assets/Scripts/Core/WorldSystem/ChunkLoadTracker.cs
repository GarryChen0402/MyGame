using System;
using System.Collections.Generic;
using UnityEngine;

// One-shot region-ready gate (Part B §4.3): counts ChunkLoaded arrivals for a
// target coordinate list and fires RegionReady once the landing square (center
// ±readyRadius) is complete. The gate is intentionally narrower than the
// progress denominator - the rest of the ring keeps loading (tick pump + the
// player focus's increments) after the gate opens: "enter first, keep loading
// while playing". Main thread throughout: mounted right after the enter-world
// ring submission (step m3), fired inside a ChunkLoaded publish. Precondition:
// no tracked target fires ChunkLoaded before the mount (the fresh-dimension
// enter-world path satisfies this).
public class ChunkLoadTracker
{
    private readonly Dimension dim;
    private readonly List<Vector2Int> pending;   // tracked targets not ready yet
    private readonly HashSet<Vector2Int> landing;   // the ready-gate set
    private readonly int total;
    private int ready;
    private int landingLeft;
    private bool fired;

    public event Action RegionReady;

    public ChunkLoadTracker(Dimension dim, Vector2Int centerChunk, Vector2Int[] targets, float readyRadius)
    {
        this.dim = dim;
        total = targets.Length;
        pending = new List<Vector2Int>(targets);
        landing = new HashSet<Vector2Int>();
        int radius = Mathf.CeilToInt(readyRadius);
        foreach(Vector2Int c in targets)
            if(Mathf.Max(Mathf.Abs(c.x - centerChunk.x), Mathf.Abs(c.y - centerChunk.y)) <= radius)
                landing.Add(c);
        landingLeft = landing.Count;

        EventBus.Instance.Subscribe<ChunkLoadedEvent>(OnChunkLoaded);
        // Already-enabled targets never fire another load event: snapshot them
        // as ready now.
        for(int i = pending.Count - 1; i >= 0; i--)
        {
            Vector2Int c = pending[i];
            if(!dim.IsChunkEnabled(c))continue;
            pending.RemoveAt(i);
            ready++;
            if(landing.Contains(c))landingLeft--;
        }
    }

    // Overlay progress source: ready count over the full ring (Part B §5.1).
    public float Progress => total == 0 ? 1f : (float)ready / total;

    public bool IsRegionReady => landingLeft <= 0;

    private void OnChunkLoaded(ChunkLoadedEvent evt)
    {
        // Reference-equality gate: the tracked dimension's chunk instance, so a
        // same-coordinate chunk from another dimension never counts.
        if(!dim.TryGetChunk(evt.Chunk.ChunkCoord, out Chunk chunk) || chunk != evt.Chunk)return;
        if(!pending.Remove(evt.Chunk.ChunkCoord))return;   // untracked target / double count
        ready++;
        if(landing.Contains(evt.Chunk.ChunkCoord))
        {
            landingLeft--;
            if(landingLeft <= 0)FireRegionReady();
        }
    }

    // Fires the gate when the landing square is already complete. The host
    // calls this right after wiring RegionReady: when every landing chunk was
    // enabled before the mount, the normal event-driven path would never fire.
    public void CheckReadyNow()
    {
        if(!fired && landingLeft <= 0)FireRegionReady();
    }

    private void FireRegionReady()
    {
        if(fired)return;   // one-shot: the gate never fires twice
        fired = true;
        EventBus.Instance.Unsubscribe<ChunkLoadedEvent>(OnChunkLoaded);   // self-terminate
        RegionReady?.Invoke();
    }
}
