using System.Collections.Generic;
using UnityEngine;

// Loot facade: registers the mob-death hook once in the ctor (GameBootstrap's
// pre-core line touches Instance, so registration precedes any death) and owns
// the shared stack-splitting spawn helper used by both drop paths.
public class LootManager
{
    private static LootManager instance = new();
    public static LootManager Instance => instance;

    private LootManager()
    {
        EventBus.Instance.Subscribe<MobEntityDeathEvent>(OnMobDeath);
    }

    // Mob killed: rolls every table the definition references and spawns the
    // products near the corpse. Operator = the killer carried by the death
    // event (hurt-event chain); environmental deaths publish attacker null and
    // conditions decide (rule §2.1).
    private void OnMobDeath(MobEntityDeathEvent evt)
    {
        MobDefinition def = evt.entity.Definition;
        if(def == null || def.LootTables == null || def.LootTables.Count == 0)return;

        var drops = new List<ItemStack>();
        var lootCtx = new LootContext
        {
            Operator = evt.attacker,
            heldItem = evt.attacker != null ? evt.attacker.GetCurrentHoldingItemStack() : null
        };
        foreach(var tableName in def.LootTables)
            if(ResourceSystem.Instance.LootTables.TryGetResourceWithFullName(tableName, out var table))
                LootRoller.Roll(table, lootCtx, drops);
        if(drops.Count == 0)return;

        SpawnDrops(evt.entity.DimensionId, evt.entity.Position + new Vector3(0f, 0.5f, 0f), drops, 1.5f);
    }

    // Spawns the stacks near pos, splitting any stack past its item's MaxStack
    // into multiple item entities (loot amounts can exceed a stack).
    public void SpawnDrops(ushort dimensionId, Vector3 pos, List<ItemStack> drops, float scatterRange)
    {
        foreach(var stack in drops)
        {
            int max = 64;
            if(ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(stack.itemId, out var def))
                max = Mathf.Max(1, def.MaxStack);
            int left = stack.amount;
            while(left > 0)
            {
                int take = Mathf.Min(left, max);
                ItemEntityManager.Instance.SpawnItemEntity(dimensionId, pos,
                    new ItemStack { itemId = stack.itemId, amount = take },
                    new Vector3(Random.Range(-scatterRange, scatterRange), 0f, Random.Range(-scatterRange, scatterRange)),
                    0.5f);
                left -= take;
            }
        }
    }
}
