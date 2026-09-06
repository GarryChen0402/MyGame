using System.Collections.Generic;

public class MobDefinition : ResourceType
{
    public List<string> Category;
    public float BaseMaxHealth;
    public float BaseDamage;
    public float BaseMoveSpeed;

    // AI behavior spec (AIDefinitions registry id, design doc §4.1). Missing
    // entries log a warning at spawn and leave the empty-root no-op (no
    // behavior). v1: "minecraft:zombie". Behavior numbers live in the
    // AIDefinition's ConfigJson, not here - these fields stay capability-only.
    public string AIDefinitionFullName;

    // EntityModel resource id (FullName) rendered as this mob's visual.
    public string ModelId;

    public List<AABB> CollisionBoxes;

    // Loot table full names (LootSystem); null/empty = no drops on death.
    public List<string> LootTables = null;
}