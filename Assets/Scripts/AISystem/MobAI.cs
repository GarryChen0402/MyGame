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
        senseAccumulator += dt;
        if(senseAccumulator >= SenseInterval)
        {
            senseAccumulator = 0f;
            RefreshSense();
        }

        RootMachine.Tick(dt);   // no-op while no states are registered

        Apply();
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

    private void Apply()
    {
        // Hurt stun (InvincibleTimer > 0) holds horizontal writes so the
        // knockback slide plays out; target speed resumes once it ends.
        if(Mob.InvincibleTimer > 0f)return;

        Vector3 dir = Intent.MoveDirection;
        if(Mathf.Abs(dir.x) < 1e-4f && Mathf.Abs(dir.z) < 1e-4f)return;   // stand intent: no write

        float speed = Mob.Definition.BaseMoveSpeed;
        Mob.Motion = new Vector3(dir.x * speed, Mob.Motion.y, dir.z * speed);
        Mob.AiWroteMotion = true;
    }

    public void Stop()
    {
        if(deadHandler == null)return;
        EventBus.Instance.Unsubscribe(deadHandler);
        deadHandler = null;
    }
}
