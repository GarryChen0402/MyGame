using UnityEngine;

// Shared physiology of biological entities (design doc Docs/Buff系统-规则设计.md
// §3.2, plan B): health, hurt, invincibility window, heal and the death entry
// live here so MobEntity and Player stop duplicating them. Subclasses keep
// their own channels - MobEntity receives damage eventified (MobEntityHurtEvent)
// and broadcasts death, Player is hurt by direct calls and resets in place -
// so Hurt and Dead are virtual entry points over one shared flow.
public class LivingEntity : Entity
{
    public ValueEntry MaxHealth {get; protected set;}
    public float CurrentHealth {get; protected set;}
    public bool IsDead {get; protected set;} = false;

    // Last TickPhysics ground result (Entity.OnGround), exposed so AI jump
    // gating and the input layer share one shape.
    public bool IsOnGround => OnGround;

    // Generic hurt flow (Hurt/Dead entry both virtual so subclass channels
    // stay their own): dead/invincibility gates -> damage -> hurt window ->
    // horizontal knockback write (vertical motion kept) -> death dispatch at
    // zero health. The hit log sits before the dispatch so a lethal frame
    // prints the pre-reset health, matching both former implementations.
    public virtual void Hurt(Entity attacker, float damage, Vector3 knockbackVelocity = default)
    {
        if(IsDead)return;
        if(InvincibleTimer > 0)return;
        CurrentHealth = Mathf.Clamp(CurrentHealth - damage, 0, MaxHealth.CurrentValue);
        InvincibleTimer = HurtInvincibleSeconds;
        if(knockbackVelocity != Vector3.zero)
            Motion = new Vector3(knockbackVelocity.x, Motion.y, knockbackVelocity.z);

        Debug.Log($"[{GetType().Name}] took {damage:F1} damage -> {CurrentHealth:F1}/{MaxHealth.CurrentValue:F1} health");
        if(CurrentHealth <= 0f)
            Dead(attacker);
    }

    public void Heal(float heal)
    {
        if(IsDead)return;
        CurrentHealth = Mathf.Clamp(CurrentHealth + heal, 0, MaxHealth.CurrentValue);
    }

    // Death entry, dispatched by Hurt at zero health with the lethal hit's
    // attacker. Subclasses run their own death channel from here.
    protected virtual void Dead(Entity attacker) { }
}
