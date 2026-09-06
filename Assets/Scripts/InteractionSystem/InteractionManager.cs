using System.Collections.Generic;
using UnityEngine;

public class InteractionManager
{
    private static InteractionManager instance = new();
    public static InteractionManager Instance => instance;

    private InteractionManager()
    {
        // Stage main handlers: the chain has no effect until these are
        // registered (P0/P1 light up when their behaviors are added).
        EventBus.Instance.Subscribe<UseItemOnStaticBlock>(OnUseItemOnStaticBlock);
        EventBus.Instance.Subscribe<InteractWithStaticBlock>(OnInteractWithStaticBlock);
        EventBus.Instance.Subscribe<InteractWithBlockEntity>(OnInteractWithBlockEntity);
        EventBus.Instance.Subscribe<UseItemOnBlockEntity>(OnUseItemOnBlockEntity);
    }


    public void HandleLeftClick(Entity entity, Vector3Int dimCoord)
    {
        if(!WorldManager.Instance.TryGetDimension(entity.DimensionId, out var dim))return;
        ushort stateId = dim.GetBlockAt(dimCoord);
        if(stateId == 0)return;   // air: nothing to break
        if(!ResourceSystem.Instance.BlockStates.TryGetResourceWithNumberId(stateId, out var blockState))return;
        float completeTime = Mathf.Max(0.1f, blockState.Block.Hardness);
        // The session driver (Entity.ProcessInteractionSession) accumulates the
        // hold time and fires OnComplete once it reaches CompleteTime; the
        // callback consumes entity.Session as its context (single source).
        entity.SetSession("minecraft:attack", InteractionSessionTargetType.Block,
            () => CompleteBreakSession(entity), completeTime, entity, dimCoord, null);
    }

    // Left-click on a mob (crosshair-hit entity): instant hit - CompleteTime 0
    // settles the session on the very next process tick, so a click lands the
    // damage immediately (no swing animation/cool-down in v1). The session
    // target goes into Session.entity; the interaction layer stays as the
    // trigger hook for future attack events (swing animation, cool-down).
    public void HandleAttackEntity(Entity attacker, MobEntity target)
    {
        attacker.SetSession("minecraft:attack", InteractionSessionTargetType.Entity,
            () => CompleteAttackSession(attacker), 0f, target, default, null);
    }

    // Knockback impulse handed to Hurt on a landed hit (horizontal m/s;
    // one shared value in v1 - no weapon/modifier scaling yet).
    private const float PlayerKnockbackSpeed = 5f;

    // Swing callback fired on session completion: resolves the hit damage from
    // the attacker's AttackPoint (a ValueEntry - crit modifiers roll here) and
    // hurts the session target. Non-player attackers fall back to 1 (mob-side
    // melee will feed MobDefinition.BaseDamage through the AI Attack state).
    private static void CompleteAttackSession(Entity attacker)
    {
        InteractionSessionContext ctx = attacker.Session;
        if(!(ctx.entity is MobEntity mob) || mob.IsDead)return;   // died mid-swing
        float damage = attacker is Player player ? player.AttackPoint.CurrentValue : 1f;
        Vector3 dir = mob.Position - attacker.Position;
        dir.y = 0f;
        if(dir.sqrMagnitude > 1e-6f)dir.Normalize();
        else dir = Quaternion.Euler(0, attacker.yaw, 0) * Vector3.forward;   // overlapping: use the attacker's facing
        // The attacker enters the damage channel through the hurt event;
        // MobEntity's main handler applies damage/knockback synchronously from
        // this publish (Publish dispatches inline, so the blow still lands
        // within this callback).
        EventBus.Instance.Publish(new MobEntityHurtEvent
        {
            entity = mob, attacker = attacker,
            amount = damage, knockbackVelocity = dir * PlayerKnockbackSpeed
        });
    }

    // Break callback fired on session completion: collects the drops before the
    // break removes the block, breaks it, then spawns the drops at the block
    // center +0.5 up with a random horizontal kick. Drops = the definition's
    // loot tables + the block entity's inventory dump (a DataContainer removal
    // mechanism, not loot's business - kept here because the removal path has
    // no dimension/spawn context yet).
    private static void CompleteBreakSession(Entity entity)
    {
        InteractionSessionContext ctx = entity.Session;
        if(!WorldManager.Instance.TryGetDimension(entity.DimensionId, out var dim))return;

        var drops = new List<ItemStack>();

        // (1) Definition loot: rolls every table referenced by the block def.
        ushort stateId = dim.GetBlockAt(ctx.blockDimCoord);
        BlockDefinition blockDef = stateId != 0 ? ResourceSystem.Instance.GetState(stateId)?.Block : null;
        if(blockDef != null && blockDef.LootTables != null)
        {
            var lootCtx = new LootContext
            {
                Operator = entity,
                heldItem = entity.GetCurrentHoldingItemStack()   // Player override; null elsewhere
            };
            foreach(var tableName in blockDef.LootTables)
                if(ResourceSystem.Instance.LootTables.TryGetResourceWithFullName(tableName, out var table))
                    LootRoller.Roll(table, lootCtx, drops);
        }

        // (2) Block-entity inventory dump on break (read before the break).
        if(dim.TryGetBlockEntity(ctx.blockDimCoord, out BlockEntity be))
            foreach(var container in be.DataContainers.Values)
                if(container is InventoryDataContainer inv)
                    foreach(var stack in inv.Inv.itemStacks)
                        if(stack != null && !stack.IsEmpty())
                            drops.Add(new ItemStack { itemId = stack.itemId, amount = stack.amount });

        if(!WorldManager.Instance.TryBreakBlockAt(entity.DimensionId, ctx.blockDimCoord, fromInteraction: true))return;
        if(drops.Count == 0)return;
        Vector3 spawnPos = new(ctx.blockDimCoord.x + 0.5f, ctx.blockDimCoord.y + 1f, ctx.blockDimCoord.z + 0.5f);
        LootManager.Instance.SpawnDrops(entity.DimensionId, spawnPos, drops, 3f);
    }


    public void OnUseItemOnStaticBlock(UseItemOnStaticBlock evt)
    {
        Debug.Log("Use item on static Block");
        if(!ResourceSystem.Instance.ItemBehaviors.TryGetResourceWithFullName(evt.ItemDef.ItemBehaivorId, out var behavior))return;
        evt.Result = behavior.OnRightUseToBlock(evt.entity, evt.HoldingItem);
        Debug.Log("Use item on static Block done");
    }

    public void OnInteractWithStaticBlock(InteractWithStaticBlock evt)
    {
        Debug.Log($"Current interact with the Block :{evt.BlockDef.FullName}");
    }

    public void OnInteractWithBlockEntity(InteractWithBlockEntity evt)
    {
        evt.blockEntity.OnInteract(evt.entity, evt.BlockEntityDef);
    }

    public void OnUseItemOnBlockEntity(UseItemOnBlockEntity evt)
    {
        evt.blockEntity.OnInteract(evt.entity, evt.BlockEntityDef);
    }

    public void OnUseItemEvent(UseItemEvent evt)
    {
        Debug.Log("Use item no block");
        if(!ResourceSystem.Instance.ItemBehaviors.TryGetResourceWithFullName(evt.ItemDef.ItemBehaivorId, out var behavior))return;
        evt.Result = behavior.OnRightUse(evt.entity, evt.HoldingItem);
    }

}