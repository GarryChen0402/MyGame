// Perception-layer output (design doc §2.1): refreshed by MobAI on a fixed
// cadence. The decision layer only reads this cache - it never queries the
// world itself.
public class AIContext
{
    public MobEntity Self;

    // Single-player target: always the player. PlayerDeadEvent clears it to
    // release the chase lock; the next sense tick re-acquires it.
    public Entity Target;

    // Horizontal distance to Target, cached per sense tick (the same x/z
    // plane the A* grid works in). LOS is intentionally not consumed in v1.
    public float TargetDistance;
}
