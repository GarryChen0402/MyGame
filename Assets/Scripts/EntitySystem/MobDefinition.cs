using System.Collections.Generic;

public class MobDefinition : ResourceType
{
    public List<string> Category;
    public float BaseMaxHealth;
    public float BaseDamage;
    public float BaseMoveSpeed;

    // EntityModel resource id (FullName) rendered as this mob's visual.
    public string ModelId;

    public List<AABB> CollisionBoxes;

    // public string AIDefinition;
}