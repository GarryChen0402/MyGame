using System.Collections.Generic;

public class MobDefinition : ResourceType
{
    public List<string> Category;
    public float BaseMaxHealth;
    public float BaseDamage;
    public float BaseMoveSpeed;

    // AI behavior tuning (design doc §7.2). v1 zombie reads these directly;
    // the future AIDefinition round moves them into a data asset.
    public float SenseRange;
    public float ChaseRange;
    public float AttackRange;
    public float AttackInterval;
    public float TurnSpeed;
    public float KnockbackStrength;

    // EntityModel resource id (FullName) rendered as this mob's visual.
    public string ModelId;

    public List<AABB> CollisionBoxes;

    // public string AIDefinition;
}