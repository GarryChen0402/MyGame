using UnityEngine;

// Wander sub-states (design doc §3.1): stops and walks alternate over a
// rolled duration window. The window range (and the stop state's turn speed)
// are injected by ZombieAI.Configure from its AIDefinition Config - states
// carry no parameter source of their own.
public class ZombieWanderStopState : AIState
{
    private readonly float turnSpeed;
    private readonly float windowMin, windowMax;
    private float targetYaw;      // world-space heading rolled on enter
    private float duration;
    private float elapsed;
    public bool DurationExpired;  // read by the parent machine's transition table

    public ZombieWanderStopState(float turnSpeed, float windowMin, float windowMax)
    {
        this.turnSpeed = turnSpeed;
        this.windowMin = windowMin;
        this.windowMax = windowMax;
    }

    public override void OnEnter()
    {
        targetYaw = Random.Range(0f, 360f);
        duration = Random.Range(windowMin, windowMax);
        elapsed = 0f;
        DurationExpired = false;
    }

    public override void Tick(float dt)
    {
        elapsed += dt;
        DurationExpired = elapsed >= duration;
        Brain.Intent.MoveDirection = Vector3.zero;   // rooted in place
        var mob = Brain.Mob;
        mob.yaw = Mathf.MoveTowardsAngle(mob.yaw, targetYaw, turnSpeed * dt);
    }
}

// Second wander half-window: walk straight in the heading the stop window
// ended on (design doc §3.1; +Z forward matches the player convention).
// Wall contact cuts the walk short: the parent machine's Move->Stop transition
// reads Mob.HorizontalBlocked, the previous physics tick's truncation signal -
// one frame of lag is imperceptible for a sustained bump.
public class ZombieWanderMoveState : AIState
{
    private readonly float windowMin, windowMax;
    private Vector3 direction;    // fixed on enter from the mob's yaw at that moment
    private float duration;
    private float elapsed;
    public bool DurationExpired;

    public ZombieWanderMoveState(float windowMin, float windowMax)
    {
        this.windowMin = windowMin;
        this.windowMax = windowMax;
    }

    public override void OnEnter()
    {
        float rad = Brain.Mob.yaw * Mathf.Deg2Rad;
        direction = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        duration = Random.Range(windowMin, windowMax);
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
