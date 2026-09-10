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

        // ---- tree loop (design doc 树木系统 §7) ----
        ResourceSystem.Instance.LootTables.Register(new LootTableDefinition
        {
            modId = ModId, name = "oak_log",
            Groups = new()
            {
                new LootGroup { Condition = null, Entries = new()
                {
                    new LootEntry { ItemInfo = new ItemLootInfo { ItemFullName = $"{ModId}:oak_log", Amount = 1 }, Weight = 1f }
                } }
            }
        });

        ResourceSystem.Instance.LootTables.Register(new LootTableDefinition
        {
            modId = ModId, name = "oak_sapling",
            Groups = new()
            {
                // Misplaced saplings come back on break, so an accidental plant
                // never consumes the drop.
                new LootGroup { Condition = null, Entries = new()
                {
                    new LootEntry { ItemInfo = new ItemLootInfo { ItemFullName = $"{ModId}:oak_sapling", Amount = 1 }, Weight = 1f }
                } }
            }
        });

        ResourceSystem.Instance.LootTables.Register(new LootTableDefinition
        {
            modId = ModId, name = "oak_leaves",
            Groups = new()
            {
                // ~46 leaves per tree at 5% = ~2.3 saplings, so one grown tree
                // pays for the next one.
                new LootGroup { Condition = null, Entries = new()
                {
                    new LootEntry { ItemInfo = new ItemLootInfo { ItemFullName = $"{ModId}:oak_sapling", Amount = 1 }, Weight = 0.05f }
                } }
            }
        });

        ResourceSystem.Instance.LootTables.Register(new LootTableDefinition
        {
            modId = ModId, name = "grass",
            Groups = new()
            {
                // Interim bootstrap: the first sapling has to come from somewhere
                // while natural tree generation is postponed (design doc §6).
                // 50%: the entry point has to be reachable by hand-testing a
                // handful of grass blocks.
                new LootGroup { Condition = null, Entries = new()
                {
                    new LootEntry { ItemInfo = new ItemLootInfo { ItemFullName = $"{ModId}:oak_sapling", Amount = 1 }, Weight = 0.5f }
                } }
            }
        });
    }
}
