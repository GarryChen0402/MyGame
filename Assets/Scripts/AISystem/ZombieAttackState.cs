using UnityEngine;

// Attack state (design doc §3.3): approach move + an InteractionSession swing
// loop. Enters with a swing already started; each settled blow (the injected
// interval later) opens the next. The session is AI-driven - Entity skips its
// player-facing interruption rules - and completes exactly once per swing.
// Damage/knockback/interval are injected by ZombieAI.Configure (damage = the
// species capability, the other two = AIDefinition Config numbers).
public class ZombieAttackState : AIState
{
    private readonly float damage;
    private readonly float knockbackSpeed;
    private readonly float interval;

    public ZombieAttackState(float damage, float knockbackSpeed, float interval)
    {
        this.damage = damage;
        this.knockbackSpeed = knockbackSpeed;
        this.interval = interval;
    }

    public override void OnEnter()
    {
        if(Brain.Context.Target != null)StartSwing();
    }

    public override void Tick(float dt)
    {
        Brain.Path.Follow(Brain.Context, Brain.Intent, dt);   // shared approach move (design doc §3.2)
        Brain.Intent.LookAt = Brain.Context.Target;           // keep facing the victim between swings
        if(Brain.Mob.Session.Completed)StartSwing();          // previous blow settled -> next round
    }

    // Leaving mid-swing (target lost / knocked out of range / player dead):
    // the running session must never settle after the state ends.
    public override void OnExit() => Brain.Mob.AbortInteractionSession();

    private void StartSwing()
    {
        var mob = Brain.Mob;
        var target = Brain.Context.Target;   // non-null by transition guarantee; guarded anyway
        if(target == null)return;
        mob.Session.IsAIControlled = true;   // exempt from player-facing interruption rules (design doc §6.1); SetSession keeps the flag
        if(!mob.SetSession(InteractionManager.AttackBindingName, InteractionSessionTargetType.Entity,
            () =>
            {
                // Settlement guard: Dead() already settles the session, so this
                // is a second defense - a corpse never deals damage.
                if(mob.IsDead || !(mob.Session.entity is Player player))return;
                Vector3 d = player.Position - mob.Position;
                d.y = 0f;
                if(d.sqrMagnitude > 1e-6f)d.Normalize();
                else d = Quaternion.Euler(0f, mob.yaw, 0f) * Vector3.forward;   // overlap fallback: own heading
                // The mob enters the player damage channel as the event's
                // attacker; Player's main handler applies the shared hurt flow
                // synchronously from this publish (Publish dispatches inline).
                EventBus.Instance.Publish(new PlayerHurtEvent
                {
                    player = player, attacker = mob,
                    amount = damage, knockbackVelocity = d * knockbackSpeed
                });
            },
            interval, target, default, null))
        {
            // Slot busy: Tick only re-swings once the previous blow settled
            // (Completed), so this is a defensive path - aborting resets the
            // slot and the next Tick retries instead of wedging the state.
            mob.AbortInteractionSession();
        }
    }
}
