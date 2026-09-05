using UnityEngine;
public class MobEntity : Entity
{
    public MobDefinition Definition {get; private set;} = null;

    public float CurrentHealth {get; private set;} = 0;
    public ValueEntry MaxHealth {get; private set;}

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

    public bool IsDead {get; private set;} = false;

    // Last TickPhysics ground result, exposed for AI jump gating (same shape
    // as Player.IsOnGround).
    public bool IsOnGround => OnGround;
    private float deathTimer;
    private const float DeathDelaySeconds = 1.5f;   // visual margin (design doc §4.6)

    public void Hurt(float damage, Vector3 knockbackVelocity = default)
    {
        if(IsDead)return;
        if(InvincibleTimer > 0)return;
        CurrentHealth = Mathf.Clamp(CurrentHealth - damage, 0, MaxHealth.CurrentValue);
        InvincibleTimer = HurtInvincibleSeconds;
        if(knockbackVelocity != Vector3.zero)
            Motion = new Vector3(knockbackVelocity.x, Motion.y, knockbackVelocity.z);

        Debug.Log($"[Mob] {MobName} took {damage:F1} damage -> {CurrentHealth:F1}/{MaxHealth.CurrentValue:F1} health");
        EventBus.Instance.Publish(new HurtEntity(){entity = this, amount = damage});
        if(CurrentHealth <= 0)Dead();
    }

    public void Heal(float heal)
    {
        if(IsDead)return;
        CurrentHealth = Mathf.Clamp(CurrentHealth + heal, 0, MaxHealth.CurrentValue);
    }
    public void Dead()
    {
        if(IsDead)return;   // idempotent: hurt-triggered and external calls converge on one flow
        IsDead = true;
        AI?.Stop();         // unsubscribe PlayerDeadEvent - the corpse no longer senses
        Session.Completed = true;   // a corpse never settles a swing (silent: no Interrupted event)
        Debug.Log($"[Mob] {MobName} died");
        EventBus.Instance.Publish(new DeathEntity(){entity = this});
    }

    private string MobName => Definition != null ? Definition.FullName : GetType().Name;

    // Death flow: the corpse lingers for DeathDelaySeconds (death anim/other
    // DeathEntity subscribers watch it), then self-removes - the same removal
    // path as despawn: unregister from the tick set, then OnDestroy publishes
    // DestroyEntity so PhysicsManager forgets the AABBs and EntityRenderManager
    // destroys the shell.
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