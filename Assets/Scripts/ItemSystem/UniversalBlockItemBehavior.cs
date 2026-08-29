using UnityEngine;

public class UniversalBlockItemBehavior : ItemBehaivor
{
    public UniversalBlockItemBehavior()
    {
        
    }
    public override ItemUseResult OnRightUseToBlock(Entity owner, ItemStack itemStack) 
    {
        var raycastRes = owner.CurrentRaycastHitResult;
        if(!raycastRes.IsHit)return ItemUseResult.Fail;
        if(!WorldManager.Instance.TryGetDimension(owner.DimensionId, out var dim))return ItemUseResult.Fail;
        if(!ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(itemStack.itemId, out var itemDef))return ItemUseResult.Fail;
        if(!ResourceSystem.Instance.BlockDefinitions.TryGetNumberId(itemDef.BlockFullName, out var blockId))return ItemUseResult.Fail;
        Vector3Int normal = new (
            Mathf.FloorToInt(raycastRes.Normal.x),
            Mathf.FloorToInt(raycastRes.Normal.y),
            Mathf.FloorToInt(raycastRes.Normal.z)
        );
        return new ItemUseResult()
        {
            UseSuccess = dim.TrySetBlockAt(raycastRes.BlockDimensionCoord + normal, blockId, fromInteraction: true),
            ConsumeAmount = 1,
            ReplaceWith = null
        };
    }
}