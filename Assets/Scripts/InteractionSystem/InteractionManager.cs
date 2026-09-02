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

    // P2 main: resolve the held item and delegate judgment to its behavior.
    // private static void OnItemUseOnBlock(ItemUseOnBlockEvent evt)
    // {
    //     var entity = evt.Operator;
    //     ItemStack stack = entity.inventory.GetItemStackAt(entity.SelectedSlotIndex);
    //     if(stack == null || stack.IsEmpty())return;
    //     if(!ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(stack.itemId, out var itemDef))return;
    //     if(!ResourceSystem.Instance.ItemBehaviors.TryGetResourceWithFullName(itemDef.ItemBehaivorId, out var behavior))return;
    //     evt.Result = behavior.OnRightUseToBlock(entity, stack);
    // }

    // private static void OnItemUse(ItemUseEvent evt)
    // {
    //     var entity = evt.Operator;
    //     ItemStack stack = entity.inventory.GetItemStackAt(entity.SelectedSlotIndex);
    //     if(stack == null || stack.IsEmpty())return;
    //     if(!ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(stack.itemId, out var itemDef))return;
    //     if(!ResourceSystem.Instance.ItemBehaviors.TryGetResourceWithFullName(itemDef.ItemBehaivorId, out var behavior))return;
    //     evt.Result = behavior.OnRightUse(entity, stack);
    // }

    

    public void HandleLeftClick(Entity entity, Vector3Int dimCoord)
    {
        if(!WorldManager.Instance.TryGetDimension(entity.DimensionId, out var dim))return;
        List<ItemStack> drops = DropResolver.Collect(dim, dimCoord);   // read the old block before the break removes it
        if(!WorldManager.Instance.TryBreakBlockAt(entity.DimensionId, dimCoord, fromInteraction: true))return;
        // Spawn every drop at the block center +0.5 up with a random horizontal
        // kick (P5 of the item drop dev plan: full break -> drop chain).
        Vector3 spawnPos = new(dimCoord.x + 0.5f, dimCoord.y + 1f, dimCoord.z + 0.5f);
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

}