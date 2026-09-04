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

    // Break callback fired on session completion: collects the drops before the
    // break removes the block, breaks it, then spawns the drops at the block
    // center +0.5 up with a random horizontal kick (P5 of the item drop dev
    // plan: full break -> drop chain).
    private static void CompleteBreakSession(Entity entity)
    {
        InteractionSessionContext ctx = entity.Session;
        if(!WorldManager.Instance.TryGetDimension(entity.DimensionId, out var dim))return;

        List<ItemStack> drops = DropResolver.Collect(dim, ctx.blockDimCoord);   // read the old block before the break removes it
        if(!WorldManager.Instance.TryBreakBlockAt(entity.DimensionId, ctx.blockDimCoord, fromInteraction: true))return;

        Vector3 spawnPos = new(ctx.blockDimCoord.x + 0.5f, ctx.blockDimCoord.y + 1f, ctx.blockDimCoord.z + 0.5f);
        foreach(ItemStack stack in drops)
            ItemEntityManager.Instance.SpawnItemEntity(entity.DimensionId, spawnPos, stack,
                new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f)), 0.5f);
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