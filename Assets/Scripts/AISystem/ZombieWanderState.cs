using UnityEngine;

// Wander duration window (design doc §3.1): rolled on each state entry so
// stops and walks alternate 3-6s. v1 code constants - the AIDefinition round
// migrates them into the resource.
public static class ZombieWanderWindows
{
    public const float Min = 3f;
    public const float Max = 6f;
    public static float Roll() => Random.Range(Min, Max);
}

// First wander half-window: stand still while turning to a random heading
// (design doc §3.1). The yaw stays data-layer in v1 - shell rotation sync is a
// later round - so this reads as the mob facing a new way before it walks.
public class ZombieWanderStopState : AIState
{
    private float targetYaw;      // world-space heading rolled on enter
    private float duration;
    private float elapsed;
    public bool DurationExpired;  // read by the parent machine's transition table

    public override void OnEnter()
    {
        targetYaw = Random.Range(0f, 360f);
        duration = ZombieWanderWindows.Roll();
        elapsed = 0f;
        DurationExpired = false;
    }

    public override void Tick(float dt)
    {
        elapsed += dt;
        DurationExpired = elapsed >= duration;
        Brain.Intent.MoveDirection = Vector3.zero;   // rooted in place
        var mob = Brain.Mob;
        mob.yaw = Mathf.MoveTowardsAngle(mob.yaw, targetYaw, mob.Definition.TurnSpeed * dt);
    }
}

// Second wander half-window: walk straight in the heading the stop window
// ended on (design doc §3.1; +Z forward matches the player convention).
// Wall contact cuts the walk short: the parent machine's Move->Stop transition
// reads Mob.HorizontalBlocked, the previous physics tick's truncation signal -
// one frame of lag is imperceptible for a sustained bump.
public class ZombieWanderMoveState : AIState
{
    private Vector3 direction;    // fixed on enter from the mob's yaw at that moment
    private float duration;
    private float elapsed;
    public bool DurationExpired;

    public override void OnEnter()
    {
        float rad = Brain.Mob.yaw * Mathf.Deg2Rad;
        direction = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        duration = ZombieWanderWindows.Roll();
        elapsed = 0f;
        DurationExpired = false;
    }

    public override void Tick(float dt)
    {
        elapsed += dt;
        DurationExpired = elapsed >= duration;
        Brain.Intent.MoveDirection = direction;
    }
}
