using System.Collections.Generic;
using UnityEngine;

public class InteractionManager
{
    // Binding names the session pipeline gates on (KeyBinding ids; IsDown also
    // consults the active input context). Mining/attacking share the attack
    // binding, the right-click use pipeline has its own.
    public const string AttackBindingName = "minecraft:attack";
    public const string UseBindingName = "minecraft:use_item";

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
        // Right-click on air (no block hit): the settlement of an Item-target
        // session publishes this. (Formerly unsubscribed - the input layer
        // published it into the void.)
        EventBus.Instance.Subscribe<UseItemEvent>(OnUseItemEvent);
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
        entity.SetSession(AttackBindingName, InteractionSessionTargetType.Block,
            () => CompleteBreakSession(entity), completeTime, entity, dimCoord, null);
    }

    // Left-click on a mob (crosshair-hit entity): instant hit - CompleteTime 0
    // settles the session on the very next process tick, so a click lands the
    // damage immediately (no swing animation/cool-down in v1). The session
    // target goes into Session.entity; the interaction layer stays as the
    // trigger hook for future attack events (swing animation, cool-down).
    public void HandleAttackEntity(Entity attacker, MobEntity target)
    {
        attacker.SetSession(AttackBindingName, InteractionSessionTargetType.Entity,
            () => CompleteAttackSession(attacker), 0f, target, default, null);
    }

    // Right-click entry: every use/interact intent funnels into the session
    // pipeline (rule doc §3.1). Resolution splits on what the raycast hit and
    // what the operator holds: block-entity targets run on block-owned timing
    // (0 for now), static-block and air targets run on the held item's UseTime
    // (0 = instant click). Settlement re-publishes the pre-session interaction
    // events, so the ItemBehavior/block-entity consumers stay untouched. An
    // in-flight session (mining, eating) silently rejects the request.
    public void HandleUseItem(Entity entity)
    {
        ItemStack held = entity.IsHoldingItem() ? entity.GetCurrentHoldingItemStack() : null;
        ItemDefinition heldDef = null;
        if(held != null && !ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(held.itemId, out heldDef))return;
        var hit = entity.CurrentRaycastHitResult;
        if(!hit.IsHit)
        {
            // Item used on air: only a held item acts - its UseTime gates the
            // hold (eating/drinking). Empty-handed on air is nothing.
            if(heldDef == null)return;
            entity.SetSession(UseBindingName, InteractionSessionTargetType.Item,
                () => CompleteUseSession(entity), heldDef.UseTime, null, default, held);
            return;
        }
        if(!WorldManager.Instance.TryGetDimension(entity.DimensionId, out var dim))return;
        ushort stateId = dim.GetBlockAt(hit.BlockDimensionCoord);
        if(stateId == 0)return;   // air under the crosshair: nothing to interact with
        if(!ResourceSystem.Instance.BlockStates.TryGetResourceWithNumberId(stateId, out var state))return;
        BlockDefinition blockDef = state.Block;
        // A block-entity host chunk is always enabled (the ray just hit it),
        // so a missing BE means the target shifted between the raycast and the
        // session start - drop the click (same guard the input layer ran).
        if(blockDef.HasBlockEntity
            && !WorldManager.Instance.TryGetBlockEntity(entity.DimensionId, hit.BlockDimensionCoord, out _))return;
        bool hasBE = blockDef.HasBlockEntity;
        // §3.1 timing split: BE targets use block-owned timing (0 until block
        // entities define one); static-block targets use the held item's.
        float completeTime = hasBE || heldDef == null ? 0f : heldDef.UseTime;
        entity.SetSession(UseBindingName, InteractionSessionTargetType.Block,
            () => CompleteUseSession(entity), completeTime, null, hit.BlockDimensionCoord, held, hit.Normal);
    }

    // Settled use/interact session (rule doc §3.3): re-publishes the
    // interaction event the intent mapped to - payload rebuilt from the locked
    // session context - then feeds the use result into the operator's consume
    // path. Runs inside the settlement (same frame for instant sessions).
    private static void CompleteUseSession(Entity entity)
    {
        InteractionSessionContext ctx = entity.Session;
        bool holding = ctx.itemStack != null && !ctx.itemStack.IsEmpty();
        Vector3Int coord = ctx.blockDimCoord;
        if(ctx.TargetType == InteractionSessionTargetType.Item)
        {
            var evt = new UseItemEvent
            {
                entity = entity,
                HoldingItem = ctx.itemStack,
                ItemDef = ctx.ItemDef,
                HitBlockCoord = coord,
                HitNormal = ctx.HitNormal,
                Result = new ItemUseResult()
            };
            EventBus.Instance.Publish(evt);
            entity.ConsumeItemUseResult(evt.Result);
            return;
        }
        // Block target: whether the target hosted a block entity at session
        // start picks the BE variant (resolved into ctx by SetSession).
        bool withBE = ctx.blockEntity != null;
        if(holding)
        {
            ItemUseResult result = new();
            if(withBE)
            {
                var evt = new UseItemOnBlockEntity
                {
                    entity = entity,
                    HoldingItem = ctx.itemStack,
                    ItemDef = ctx.ItemDef,
                    HitBlockCoord = coord,
                    HitNormal = ctx.HitNormal,
                    BlockId = ctx.blockId,
                    BlockDef = ctx.BlockDef,
                    blockEntity = ctx.blockEntity,
                    BlockEntityDef = ctx.BlockEntityDef,
                    Result = result
                };
                EventBus.Instance.Publish(evt);
                result = evt.Result;
            }
            else
            {
                var evt = new UseItemOnStaticBlock
                {
                    entity = entity,
                    HoldingItem = ctx.itemStack,
                    ItemDef = ctx.ItemDef,
                    HitBlockCoord = coord,
                    HitNormal = ctx.HitNormal,
                    BlockId = ctx.blockId,
                    BlockDef = ctx.BlockDef,
                    Result = result
                };
                EventBus.Instance.Publish(evt);
                result = evt.Result;
            }
            entity.ConsumeItemUseResult(result);
            return;
        }
        // Empty-handed interaction: open/activate the target (BE) or the plain
        // notification (static block).
        if(withBE)
        {
            var evt = new InteractWithBlockEntity
            {
                entity = entity,
                HitBlockCoord = coord,
                HitNormal = ctx.HitNormal,
                BlockId = ctx.blockId,
                BlockDef = ctx.BlockDef,
                blockEntity = ctx.blockEntity,
                BlockEntityDef = ctx.BlockEntityDef
            };
            EventBus.Instance.Publish(evt);
        }
        else
        {
            var evt = new InteractWithStaticBlock
            {
                entity = entity,
                HitBlockCoord = coord,
                HitNormal = ctx.HitNormal,
                BlockId = ctx.blockId,
                BlockDef = ctx.BlockDef
            };
            EventBus.Instance.Publish(evt);
        }
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
        // Items without a behavior id (plain tools etc.) use nothing - skip
        // before the registry lookup, a null key would throw.
        if(string.IsNullOrEmpty(evt.ItemDef.ItemBehaivorId))return;
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
        if(string.IsNullOrEmpty(evt.ItemDef.ItemBehaivorId))return;   // no behavior: nothing to use (null-key guard, see OnUseItemOnStaticBlock)
        if(!ResourceSystem.Instance.ItemBehaviors.TryGetResourceWithFullName(evt.ItemDef.ItemBehaivorId, out var behavior))return;
        evt.Result = behavior.OnRightUse(evt.entity, evt.HoldingItem);
    }

}