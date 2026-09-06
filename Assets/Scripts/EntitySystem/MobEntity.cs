using System;
using UnityEngine;
public class MobEntity : LivingEntity
{
    public MobDefinition Definition {get; private set;} = null;

    // Head heading + lock flag (data layer, same shape as Entity.yaw): MobAI
    // evolves HeadYaw toward the look target while pursuing and sets
    // HeadLocked, and settles HeadYaw back onto the body heading otherwise
    // (unlocked - the animation owns the head again). The render shell
    // mirrors HeadYaw onto the model's head node only while locked.
    public float HeadYaw;
    public bool HeadLocked;

    // Drives perception/decision/execution while alive (null until Init).
    // Stopped on death: a corpse is never AI-driven again.
    public MobAI AI {get; private set;}

    // True on frames MobAI.Apply wrote a horizontal target speed; such frames
    // skip ExternalDamping (the motion already is the intended speed, damping
    // would drag it ~5% low at 60fps). Reset every frame inside TickPhysics.
    public bool AiWroteMotion {get; set;}

    private const float ExternalDamping = 3f;
    protected override void TickPhysics(float dt)
    {
        // Exponential horizontal decay at ExternalDamping/s: a 5 m/s
        // knockback slides ~1.7 m (Pow(3, dt) would amplify instead).
        // Only undriven frames (hurt stun / stand intent) are damped, so the
        // blow slides out while AI target writes stay exact.
        if(!AiWroteMotion)
        {
            Motion.x *= Mathf.Exp(-ExternalDamping * dt);
            Motion.z *= Mathf.Exp(-ExternalDamping * dt);
        }
        AiWroteMotion = false;
        base.TickPhysics(dt);
    }

    public MobEntity()
    {
        EntityManager.Instance.Register(this);
        CurrentHealth = 0;
        MaxHealth = new ValueEntry(0);
    }

    public void Init(MobDefinition def)
    {
        // Definition = def;
        // AABBs.Clear();
        // foreach(var box in def.CollisionBoxes)AABBs.Add(box);
        Init(def, Vector3.zero);
    }

    public void Init(MobDefinition def, Vector3 pos)
    {
        Definition = def;
        AABBs.Clear();
        foreach(var box in def.CollisionBoxes)
        {
            AABBs.Add( new AABB()
            {
                MinRange = box.MinRange + pos,
                MaxRange = box.MaxRange + pos
            });
        }
        MaxHealth.SetBaseValue(def.BaseMaxHealth);
        CurrentHealth = MaxHealth.CurrentValue;

        AI = MobAI.Create(this);

        // AIDefinition round: registry lookup replaces the v1 ModelId branch
        // (design doc §2.3). Unknown/missing ids keep the empty-root no-op and
        // warn once per spawn - a bad mod entry must never crash the world.
        if(!string.IsNullOrEmpty(def.AIDefinitionFullName))
        {
            if(ResourceSystem.Instance.AIDefinitions.TryGetResourceWithFullName(def.AIDefinitionFullName, out var aiDef))
                aiDef.Assembler?.Invoke(this, aiDef);
            else
                Debug.LogWarning($"[Mob] {def.FullName}: AIDefinition '{def.AIDefinitionFullName}' not found - empty-root no-op");
        }
    }

    private float deathTimer;
    private const float DeathDelaySeconds = 1.5f;   // visual margin (design doc §4.6)

    // Hurt event main handler (registered once in the static ctor): the mob
    // hurt channel stays eventified - LivingEntity.Hurt dispatches the shared
    // flow from here. The death event is *not* subscribed: MobEntity.Dead
    // publishes it, and a self-subscription would re-enter Dead on its own
    // broadcast. A victim instance implies the type is already initialized,
    // so registration always precedes any publish.
    private static readonly Action<MobEntityHurtEvent> hurtHandler = OnHurtEvent;

    static MobEntity()
    {
        EventBus.Instance.Subscribe(hurtHandler);
    }

    private static void OnHurtEvent(MobEntityHurtEvent evt)
        => evt.entity.Hurt(evt.attacker, evt.amount, evt.knockbackVelocity);

    // Death state transition, dispatched by LivingEntity.Hurt at zero health
    // (virtual Dead entry; the hurt channel above stays eventified). Settles
    // the corpse state first, then broadcasts the death event so LootManager
    // reacts to a stable corpse - same frame as the lethal hit, published
    // exactly once (no subscriber here, so no re-entry).
    protected override void Dead(Entity attacker)
    {
        if(IsDead)return;   // idempotent: one corpse state per entity
        IsDead = true;
        AI?.Stop();         // unsubscribe PlayerDeadEvent - the corpse no longer senses
        Session.Completed = true;   // a corpse never settles a swing (silent: no Interrupted event)
        Debug.Log($"[Mob] {MobName} died");
        EventBus.Instance.Publish(new MobEntityDeathEvent(){entity = this, attacker = attacker});
    }

    private string MobName => Definition != null ? Definition.FullName : GetType().Name;

    // Death flow: the corpse lingers for DeathDelaySeconds (death anim/other
    // MobEntityDeathEvent subscribers watch it), then self-removes - the same
    // removal path as despawn: unregister from the tick set, then OnDestroy
    // publishes DestroyEntity so PhysicsManager forgets the AABBs and
    // EntityRenderManager destroys the shell.
    public override void OnUpdate(float deltaTime)
    {
        // AI decides before base physics so its Motion writes are consumed by
        // this same frame's TickPhysics (the input layer shares this ordering).
        if(!IsDead)AI?.Update(deltaTime);
        base.OnUpdate(deltaTime);
        if(!IsDead)return;
        deathTimer += deltaTime;
        if(deathTimer >= DeathDelaySeconds)
        {
            EntityManager.Instance.Unregister(this);
            OnDestroy();
        }
    }
}