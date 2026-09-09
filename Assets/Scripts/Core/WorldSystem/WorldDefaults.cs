using UnityEngine;

// Engine-side defaults for world attributes (decision B: spawn belongs to the
// world, not the player). The default spawn position lives here so Player's
// ctor no longer carries "spawn" semantics - the real spawn follows the active
// world's world.json v2, and this value is only the fresh-world initial value.
public static class WorldDefaults
{
    // Feet-center pivot above the tallest biome surface (~106): a fresh world
    // spawns mid-air and the player falls onto the already-ready terrain below
    // (legacy semantics preserved). Same pivot convention as PlayerSaveData.position.
    public static readonly Vector3 DefaultSpawnPosition = new(0f, 115f, 0f);
}
