using System.Collections.Generic;

public class MobDefinition : ResourceType
{
    public List<string> Category;
    public float BaseMaxHealth;
    public float BaseDamage;
    public float BaseMoveSpeed;

    public List<AABB> CollisionBoxes;

    // public string AIDefinition;
}