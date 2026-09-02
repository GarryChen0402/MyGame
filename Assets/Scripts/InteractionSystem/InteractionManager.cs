using UnityEditor.PackageManager;
using UnityEngine;

public class InteractionManager
{
    private static InteractionManager instance = new();
    public static InteractionManager Intance => instance;

    private InteractionManager()
    {
        // Stage main handlers: the chain has no effect until these are
        // registered (P0/P1 light up when their behaviors are added).
        EventBus.Instance.Subscribe<UseItemOnStaticBlock>(OnUseItemOnStaticBlock);
        EventBus.Instance.Subscribe<InteractWithStaticBlock>(OnInteractWithStaticBlock);
        EventBus.Instance.Subscribe<InteractWithBlockEntity>(OnInteractWithBlockEntity);
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

    

    // // Left-click main: break the targeted block (Before hooks can cancel).
    // private static void OnBlockBreak(BlockBreakEvent evt)
    // {
    //     // WorldManager.Instance.TryBreakBlockAt(evt.Operator.DimensionId, evt.DimensionBlockCoord, fromInteraction: true);
    // }

    public void HandleLeftClick(Entity entity, Vector3Int dimCoord)
    {
        // EventBus.Instance.Publish(new BlockBreakEvent()
        // {
        //     Operator = entity,
        //     DimensionBlockCoord = dimCoord
        // });
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

}