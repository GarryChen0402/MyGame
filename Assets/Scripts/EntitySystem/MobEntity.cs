using UnityEngine;
public class MobEntity : Entity
{
    public MobDefinition Definition {get; private set;} = null;

    public float CurrentHealth {get; private set;} = 0;
    public ValueEntry MaxHealth {get; private set;}

    private const float ExternalDamping = 3f;
    protected override void TickPhysics(float dt)
    {
        Motion.x *= Mathf.Pow(ExternalDamping, dt);
        Motion.z *= Mathf.Pow(ExternalDamping, dt);
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

    }

    public bool IsDead {get; private set;} = false;
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