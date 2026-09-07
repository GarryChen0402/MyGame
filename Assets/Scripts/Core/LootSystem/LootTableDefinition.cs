using System.Collections.Generic;

// Loot table model (design doc): table -> groups -> entries. Tables register
// as ResourceType; group/entry data is code-filled, no ctor scaffolding -
// registration happens before Freeze, where validation runs (rule §6).
public class LootTableDefinition : ResourceType
{
    // At least one group (rule §2); group/entry emptiness needs no special
    // handling - rolls over empty lists just add nothing (rule R1).
    public List<LootGroup> Groups;
}

public class LootGroup
{
    // Null condition = the group always fires (rule R1). Non-null values are
    // single ICondition instances; ConditionGroup nests arbitrary logic.
    public ICondition Condition;
    public List<LootEntry> Entries;
}

public class LootEntry
{
    public ItemLootInfo ItemInfo;
    public float Weight;          // hit probability per roll, >= 0, no upper bound (rule R3)
}

public class ItemLootInfo
{
    public string ItemFullName;   // ItemDefinitions reference, resolved at roll time
    public int Amount;            // per hit, >= 1 (rule R5)

    // Phase 2 (rule §9): factories invoked per spawned stack once ItemStack
    // carries runtime DataContainers. Phase 1 keeps the field, never invokes.
    public List<System.Func<LootContext, DataContainer>> DataContainers = null;
}
