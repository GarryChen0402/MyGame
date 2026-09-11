using UnityEngine;

// A single inventory slot reachable by click logic. Policy gates (CanPlace /
// CanTake) delegate to the owning container (or always allow for the player
// backpack); the slot's own state (empty / same item / full) is read through
// Get() by the click processor before mutating. Mutators never mark dirty -
// the caller calls MarkChanged once after a successful click resolution.
public interface ISlotAccess
{
    bool CanPlace(ItemStack stack);      // stack 能否进入该容器(策略/白名单闸门)
    bool CanTake();                      // 该请求者能否从容器取出
    int MaxStackFor(ItemStack stack);    // 槽对该物品的堆叠上限(item.MaxStack)
    ItemStack Get();                     // 槽内物品(引用,可变)
    void Set(ItemStack stack);           // 槽 ← stack(消费 stack)
    void TakeHalf(out ItemStack taken);  // 取走一半,奇数取大(7 → 拿 4 留 3)
    void PlaceOne(ItemStack from);       // 从 from 放 1 个进槽(空槽建堆/同 id 加 1/满不动)
    void AddToSlot(int amount);          // 槽堆增加(截断到上限)
    void RemoveFromSlot(int amount);     // 槽堆减少(不足则不变)
    void MarkChanged();                  // 成功结算后落脏(容器 → MarkDirty 存档)
    ContainerAccess Requester { get; }   // 本次操作者(玩家点击恒为 Player)
}

// Shared slot-level read/write over an Inventory slot. Policy gates are left
// to subclasses: PlayerSlotAccess (backpack, no policy) vs ContainerSlotAccess
// (block-entity container, gates delegate to InventoryDataContainer).
public abstract class InventorySlotAccess : ISlotAccess
{
    protected readonly Inventory inv;
    protected readonly int index;

    protected InventorySlotAccess(Inventory inv, int index)
    {
        this.inv = inv;
        this.index = index;
    }

    public ContainerAccess Requester => ContainerAccess.Player;   // player-driven clicks

    protected ItemStack Slot => inv?.GetItemStackAt(index);

    public abstract bool CanPlace(ItemStack stack);
    public abstract bool CanTake();

    public ItemStack Get() => Slot;

    public int MaxStackFor(ItemStack stack)
        => stack != null && !stack.IsEmpty()
           && ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(stack.itemId, out var def)
               ? def.MaxStack : 0;

    public void Set(ItemStack stack)
    {
        var slot = Slot;
        if(slot == null || stack == null || stack.IsEmpty())return;
        slot.itemId = stack.itemId;
        slot.amount = stack.amount;
        stack.Clear();
    }

    public void TakeHalf(out ItemStack taken)
    {
        var slot = Slot;
        if(slot == null || slot.IsEmpty())
        {
            taken = null;
            return;
        }
        taken = new ItemStack { itemId = slot.itemId, amount = (slot.amount + 1) / 2 };
        slot.amount /= 2;
        if(slot.amount == 0)slot.Clear();
    }

    public void PlaceOne(ItemStack from)
    {
        var slot = Slot;
        if(slot == null || from == null || from.IsEmpty())return;
        if(slot.IsEmpty())
        {
            slot.itemId = from.itemId;
            slot.amount = 1;
        }
        else if(slot.itemId == from.itemId && slot.amount < MaxStackFor(slot))
        {
            slot.amount++;
        }
        else return;
        from.amount--;
        if(from.amount == 0)from.Clear();
    }

    public void AddToSlot(int amount)
    {
        var slot = Slot;
        if(slot == null || slot.IsEmpty() || amount <= 0)return;
        slot.amount = Mathf.Min(slot.amount + amount, MaxStackFor(slot));
    }

    public void RemoveFromSlot(int amount)
    {
        var slot = Slot;
        if(slot == null || slot.IsEmpty() || amount <= 0)return;
        slot.TryConsumeItem(amount);
    }

    public abstract void MarkChanged();
}

// Player backpack slot: no container policy - any valid stack may enter/leave.
public class PlayerSlotAccess : InventorySlotAccess
{
    public PlayerSlotAccess(Inventory inv, int index) : base(inv, index) {}

    public override bool CanPlace(ItemStack stack) => stack != null && !stack.IsEmpty();
    public override bool CanTake() => true;
    public override void MarkChanged() { }   // player inventory persists via its own save path
}

// Block-entity container slot: gates delegate to the owning container's
// per-slot rules (SlotRule: requesters + tag whitelists per direction), and
// successful writes mark the owning chunk dirty for autosave.
public class ContainerSlotAccess : InventorySlotAccess
{
    private readonly InventoryDataContainer container;

    public ContainerSlotAccess(InventoryDataContainer container, int index)
        : base(container?.Inv, index)
    {
        this.container = container;
    }

    public override bool CanPlace(ItemStack stack)
        => container != null && container.CanInsert(index, stack, ContainerAccess.Player);

    public override bool CanTake() => container != null && container.CanExtract(index, ContainerAccess.Player);

    // DataContainer.MarkDirty is protected; reach the same dirty chain through
    // its public Host (BlockEntity.MarkDirty → OwnerChunk).
    public override void MarkChanged() => container?.Host?.MarkDirty();
}
