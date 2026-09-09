using UnityEngine;

// Load-center abstraction (Part B §4.1, decision C): the "who loads/unloads
// around what" of world streaming generalizes from the single player point into
// a multi-focus union. Dynamic focuses refresh against the authoritative player
// position at tick granularity (rule L2 - never the view mirror); WorldManager.
// UpdateLoadCenter consumes the dirty flags and submits load rings / sweeps
// unloads per dimension.
//
// v1 registers only PlayerLoadFocus, the exact equivalent of the retired
// single-center ForceLoadAround; spawn residency (SpawnLoadFocus) and API
// focuses stay out until a v2 decision enables them (doc §4.2).
public abstract class LoadFocus
{
    public int LoadRadius;        // load ring radius in chunks (default = legacy view distance)
    public int UnloadRadius;      // sweep radius: enabled chunks beyond EVERY focus's unload radius are unloaded
    public Vector2Int FocusChunk; // effective center (main thread only; workers never read it)
    public bool Dirty;            // set by RefreshFocus, consumed and cleared by UpdateLoadCenter

    // Refreshes the effective center once per logic tick. Static focuses never
    // move and never go dirty. Returns whether the center moved.
    public virtual bool RefreshFocus() => false;
}

// Dynamic player focus: owns the run-time center by reading the authoritative
// Player position at tick granularity (rule L2). Constructed already-synced on
// the landing chunk (the enter-world ring submission covered it), so the first
// tick after the gate opens does not re-submit the whole ring.
public class PlayerLoadFocus : LoadFocus
{
    private Vector2Int lastChunk;

    public PlayerLoadFocus(Vector2Int landingChunk)
    {
        LoadRadius = WorldManager.ChunkLoadRange;
        UnloadRadius = WorldManager.ChunkLoadRange;
        FocusChunk = landingChunk;
        lastChunk = landingChunk;
    }

    public override bool RefreshFocus()
    {
        Vector2Int coord = Dimension.WorldPosToChunkCoord(Player.Instance.Position);
        if(coord == lastChunk)return false;
        lastChunk = coord;
        FocusChunk = coord;
        Dirty = true;
        return true;
    }
}
