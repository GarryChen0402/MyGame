using System;
using UnityEngine;

// Drives one mob's three-layer pipeline (design doc §3): a fixed-cadence
// perception refresh fills AIContext, the root state machine decides every
// frame into AIIntent, and Apply turns the intent into Motion writes. Plain
// data class like the rest of the entity layer - MobEntity.Update calls it on
// the single tick path; it is never a MonoBehaviour.
public class MobAI
{
    public MobEntity Mob;
    public AIContext Context = new();
    public AIIntent Intent = new();
    public AIStateMachine RootMachine;
    // yaw convergence rate on moving frames (deg/s; injected by the assembler
    // from its Config). 0 = heading stays owned by the decision states.
    public float TurnSpeed;
    // Shared by Chase/Attack for A* following; reuses its waypoints/cursor
    // across state switches so a path never restarts from scratch.
    public ZombieAIPath Path = new();

    public const float SenseInterval = 0.1f;   // perception cadence (design doc §1)
    private float senseAccumulator;

    private Action<PlayerDeadEvent> deadHandler;   // kept to unsubscribe by instance

    public static MobAI Create(MobEntity mob)
    {
        MobAI ai = new MobAI();
        ai.Mob = mob;
        ai.Context.Self = mob;
        ai.RootMachine = new AIStateMachine();
        ai.RootMachine.SetBrain(ai);

        // Player death releases the chase target lock immediately; the next
        // sense tick re-acquires the player (single-player: always the player).
        ai.deadHandler = _ => ai.Context.Target = null;
        EventBus.Instance.Subscribe(ai.deadHandler);
        return ai;
    }

    public void Update(float dt)
    {
        // One-frame intent contract: every decision state writes what it wants
        // this tick, so a frame that requests nothing must not inherit a stale
        // jump or look target from the previous one (wander states never set
        // them).
        Intent.JumpRequested = false;
        Intent.LookAt = null;

        senseAccumulator += dt;
        if(senseAccumulator >= SenseInterval)
        {
            senseAccumulator = 0f;
            RefreshSense();
        }

        RootMachine.Tick(dt);   // no-op while no states are registered

        Apply(dt);
        UpdateHead(dt);
    }

    private void RefreshSense()
    {
        if(Context.Target == null)
            Context.Target = Player.Instance;

        Entity target = Context.Target;
        if(target == null)return;

        // Horizontal distance cached per sense tick (same x/z plane the A* grid uses).
        Vector3 delta = Mob.Position - target.Position;
        Context.TargetDistance = Mathf.Sqrt(delta.x * delta.x + delta.z * delta.z);
    }

    private const float JumpSpeed = 6.4f;   // m/s; player-jump parity (apex ~1.28 clears a 1-block ledge)

    // Head convergence rates (deg/s): fast while locked on a look target so
    // the head snaps ahead of the slower body turn; slower when settling back
    // onto the body heading.
    private const float HeadTurnSpeed = 540f;
    private const float HeadReturnSpeed = 360f;

    private void Apply(float dt)
    {
        // Hurt stun (InvincibleTimer > 0) holds horizontal writes so the
        // knockback slide plays out; target speed resumes once it ends.
        if(Mob.InvincibleTimer > 0f)return;

        // Step-up jump: fired before the horizontal early-out so a mob standing
        // flush at a ledge foot (no horizontal step left) still climbs.
        if(Intent.JumpRequested && Mob.IsOnGround)
            Mob.Motion.y = JumpSpeed;

        Vector3 dir = Intent.MoveDirection;
        if(Mathf.Abs(dir.x) < 1e-4f && Mathf.Abs(dir.z) < 1e-4f)return;   // stand intent: no write

        // Heading converges to the movement intent (data-layer turn; the render
        // shell only mirrors yaw). Stand frames never reach here, so idle
        // turning stays owned by the decision states (WanderStop).
        if(TurnSpeed > 0f)
        {
            float target = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            Mob.yaw = Mathf.MoveTowardsAngle(Mob.yaw, target, TurnSpeed * dt);
        }

        float speed = Mob.Definition.BaseMoveSpeed;
        Mob.Motion = new Vector3(dir.x * speed, Mob.Motion.y, dir.z * speed);
        Mob.AiWroteMotion = true;
    }

    // Heading-of-the-head evolution (one-way; the render shell only mirrors
    // HeadYaw onto the head node while HeadLocked). A look intent locks the
    // head onto the target's horizontal angle; without one the head settles
    // back onto the body heading so a later lock never starts from a stale
    // pose. Death stops the AI, so a corpse keeps whatever the die clip does.
    private void UpdateHead(float dt)
    {
        Entity look = Intent.LookAt;
        if(look == null)
        {
            Mob.HeadLocked = false;
            Mob.HeadYaw = Mathf.MoveTowardsAngle(Mob.HeadYaw, Mob.yaw, HeadReturnSpeed * dt);
            return;
        }
        Mob.HeadLocked = true;
        Vector3 d = look.Position - Mob.Position;
        d.y = 0f;
        if(d.sqrMagnitude < 1e-6f)return;   // overlapped with the target: keep the current heading
        float target = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        Mob.HeadYaw = Mathf.MoveTowardsAngle(Mob.HeadYaw, target, HeadTurnSpeed * dt);
    }

    public void Stop()
    {
        if(deadHandler == null)return;
        EventBus.Instance.Unsubscribe(deadHandler);
        deadHandler = null;
    }
}
