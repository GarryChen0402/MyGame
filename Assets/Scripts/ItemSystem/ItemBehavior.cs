public class ItemBehaivor : ResourceType
{
    // Reture the use result.
    public virtual ItemUseResult OnLeftUse(Entity owner, ItemStack itemStack){return ItemUseResult.Fail;} // left use the item , when raycast not hit a block
    public virtual ItemUseResult OnRightUse(Entity owner, ItemStack itemStack){return ItemUseResult.Fail;} // righht use the item, when raycast not hit a block 
    public virtual ItemUseResult OnLeftUseToBlock(Entity owner, ItemStack itemStack){return ItemUseResult.Fail;}// left use the item , when raycast hit a block
    public virtual ItemUseResult OnRightUseToBlock(Entity owner, ItemStack itemStack){return ItemUseResult.Fail;}// righht use the item, when raycast hit a block 
}

public struct ItemUseResult
{
    public bool UseSuccess;
    public int ConsumeAmount;
    public ItemStack ReplaceWith;

    public static ItemUseResult Fail => new()
    {
        UseSuccess = false,
        ConsumeAmount = 0,
        ReplaceWith = null
    };
}