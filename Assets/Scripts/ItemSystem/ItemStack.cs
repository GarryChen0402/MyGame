public class ItemStack
{
    public ushort itemId;
    public int amount;

    public void Clear()
    {
        itemId = 0;
        amount = 0;
    }

    public bool IsEmpty()
    {
        return amount == 0;
    }
    public bool Equals(ItemStack other)
    {
        return !IsEmpty() && !other.IsEmpty() && itemId == other.itemId ;
    }

    public override bool Equals(object obj)
        => Equals(obj as ItemStack);

    public override int GetHashCode()
        =>  itemId.GetHashCode();
    /// <summary>
    /// This function will try to add the other item into this itemstack.
    /// 
    /// </summary>
    /// <param name="other">The ItemStack will be add to this</param>
    /// <returns>
    /// mean is all item in other be add into this ItemStack
    /// </returns>
    public bool TryAddItemAsMax(ItemStack other)
    {
        if(!Equals(other))return false;
        if(!ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(itemId, out var def))return false;
        if(other.amount + amount <= def.MaxStack)
        {
            amount += other.amount;
            other.Clear();
            return true;
        }
        else
        {
            other.amount -= def.MaxStack - amount;
            amount = def.MaxStack;
            return false;
        }
    }

    public void SwapWith(ItemStack other)
    {
        // Tuple swap: right-hand side is evaluated before assignment.
        (itemId, other.itemId) = (other.itemId, itemId);
        (amount, other.amount) = (other.amount, amount);
    }

    public bool TryConsumeItem(int amount)
    {
        if(IsEmpty())return false;
        if(this.amount < amount)return false;
        this.amount -= amount;
        if(this.amount == 0)Clear();
        return true;
    }
}