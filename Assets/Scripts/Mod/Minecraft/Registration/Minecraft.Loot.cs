public partial class Minecraft
{
    // ---- S: loot tables ----
    private void RegisterLootTables()
    {
        // ---- loot tables: stone keeps dropping itself, zombie drops coal ----
        ResourceSystem.Instance.LootTables.Register(new LootTableDefinition
        {
            modId = ModId, name = "stone",
            Groups = new()
            {
                new LootGroup { Condition = null, Entries = new()
                {
                    new LootEntry { ItemInfo = new ItemLootInfo { ItemFullName = $"{ModId}:stone", Amount = 1 }, Weight = 1f }
                } }
            }
        });

        ResourceSystem.Instance.LootTables.Register(new LootTableDefinition
        {
            modId = ModId, name = "zombie",
            Groups = new()
            {
                // 50%: one coal, any killer (conditions gate the groups).
                new LootGroup { Condition = null, Entries = new()
                {
                    new LootEntry { ItemInfo = new ItemLootInfo { ItemFullName = $"{ModId}:coal", Amount = 1 }, Weight = 0.5f }
                } },
                // +20% when a player wielding a sword killed it (demo condition
                // group: killer = death-event attacker since the hurt eventification).
                new LootGroup
                {
                    Condition = ConditionGroup.And(new OperatorIsPlayerCondition(),
                                                   new OperatorHoldsTagCondition("sword")),
                    Entries = new()
                    {
                        new LootEntry { ItemInfo = new ItemLootInfo { ItemFullName = $"{ModId}:coal", Amount = 1 }, Weight = 0.2f }
                    }
                }
            }
        });
    }
}
