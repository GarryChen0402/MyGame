using System.Collections.Generic;
using UnityEngine;

// Click resolution for inventory slots - the single implementation of the
// pickup/place/merge/swap decision chain (Docs/物品流转交互实现方案.md §4).
// Pure static functions: no MonoBehaviour / UIManager dependency, so the same
// code can be replayed by an authoritative server later (调研 §8/§9).
public static class SlotClickProcessor
{
    // PICKUP: one left/right click on a slot.
    // rightClick: right = 1 item / half-stack semantics, left = whole stack.
    // carried: the cursor-held stack (UIManager.HeldItemStack); mutated in place.
    public static void Click(ISlotAccess slot, bool rightClick, ItemStack carried)
    {
        if(slot == null)return;
        var slotStack = slot.Get();
        bool slotEmpty = slotStack == null || slotStack.IsEmpty();
        bool carriedEmpty = carried == null || carried.IsEmpty();

        // 1. Empty slot, cursor has items -> place (right: 1, left: whole).
        if(slotEmpty)
        {
            if(carriedEmpty)return;
            if(!slot.CanPlace(carried))return;
            if(rightClick)slot.PlaceOne(carried);
            else slot.Set(carried);
            slot.MarkChanged();
            return;
        }

        // 2. Occupied slot, cursor empty -> pick up (right: half, odd rounds up;
        //    left: whole).
        if(carriedEmpty)
        {
            if(!slot.CanTake())return;
            if(rightClick)
            {
                slot.TakeHalf(out var taken);
                if(taken == null)return;
                carried.itemId = taken.itemId;
                carried.amount = taken.amount;
            }
            else
            {
                carried.itemId = slotStack.itemId;
                carried.amount = slotStack.amount;
                slotStack.Clear();
            }
            slot.MarkChanged();
            return;
        }

        // 3. Both occupied, same stackable item -> merge cursor into slot
        //    (never swap); unstackable items (MaxStack == 1) fall through to
        //    swap below, matching vanilla.
        bool sameStackable = slotStack.itemId == carried.itemId && slot.MaxStackFor(slotStack) > 1;
        if(sameStackable)
        {
            if(!slot.CanPlace(carried))
            {
                // Output slots (Module-only insert policy) reject placements;
                // vanilla feel pulls the whole slot stack into the cursor
                // instead, consuming the recipe on MarkChanged. A cursor that
                // cannot hold the whole stack makes the click a no-op.
                if(!slot.CanTake())return;
                int cursorSpace = slot.MaxStackFor(carried) - carried.amount;
                if(cursorSpace < slotStack.amount)return;
                carried.amount += slotStack.amount;
                slotStack.Clear();
                slot.MarkChanged();
                return;
            }
            int space = slot.MaxStackFor(slotStack) - slotStack.amount;
            if(space <= 0)return;                // full stack: no effect
            if(rightClick)
            {
                slot.PlaceOne(carried);
            }
            else
            {
                int move = Mathf.Min(space, carried.amount);
                slotStack.amount += move;
                carried.amount -= move;
                if(carried.amount == 0)carried.Clear();
            }
            slot.MarkChanged();
            return;
        }

        // 4. Different items (or unstackable) -> swap whole stacks. Both gates
        //    must pass: the slot item may leave AND the carried item may enter
        //    the container, otherwise the whole swap is rejected (atomic).
        if(!slot.CanTake())return;
        if(!slot.CanPlace(carried))return;
        slotStack.SwapWith(carried);
        slot.MarkChanged();
    }

    // QUICK_MOVE: Shift+click moves the source slot's whole stack toward the
    // target zone. A container slot targets the player backpack; a backpack
    // slot targets the open container's slots (containerSlots), each placed
    // only where its policies allow (fuel into the fuel slot, never into the
    // Module-only output). Two passes mirror vanilla moveItemStackTo: fill
    // existing same-item stacks first, then empty slots.
    public static void QuickMove(ISlotAccess source, IReadOnlyList<ISlotAccess> containerSlots)
    {
        if(source == null)return;
        var src = source.Get();
        if(src == null || src.IsEmpty())return;

        // Container slot -> player backpack (no policy on the backpack side).
        if(source is ContainerSlotAccess)
        {
            if(!source.CanTake())return;
            var playerInv = Player.Instance?.inventory;
            if(playerInv == null)return;
            if(playerInv.TryAddItemStack(src))   // whole stack or nothing
                source.MarkChanged();
            return;
        }

        // Backpack slot -> open container. containerSlots is the container's
        // reachable slots (input/fuel/output of the furnace BE).
        if(containerSlots == null || containerSlots.Count == 0)return;

        // Pass 1: merge into existing same-item stacks until full.
        foreach(var target in containerSlots)
        {
            if(src.IsEmpty())break;
            var t = target.Get();
            if(t == null || t.IsEmpty())continue;
            if(t.itemId != src.itemId || t.amount >= target.MaxStackFor(t))continue;
            if(!target.CanPlace(src))continue;
            t.TryAddItemAsMax(src);              // slot absorbs src up to its cap
            target.MarkChanged();
        }

        // Pass 2: drop the remainder into the first empty accepting slots.
        foreach(var target in containerSlots)
        {
            if(src.IsEmpty())break;
            var t = target.Get();
            if(t != null && !t.IsEmpty())continue;
            if(!target.CanPlace(src))continue;
            target.Set(src);
            target.MarkChanged();
        }
    }

    // Whether a slot can receive one drag distribution (Docs/物品拖拽分配交互
    // 实现方案.md §5.1): place into an empty slot or merge into a same-item
    // stack that is not full, policy gates included. Shared by the drag
    // session collector and the settlement below - slot contents never change
    // mid-drag, so the collector's check stays valid at settlement.
    public static bool CanReceive(ISlotAccess slot, ItemStack carried)
    {
        if(slot == null || carried == null || carried.IsEmpty())return false;
        var t = slot.Get();
        if(t == null || t.IsEmpty())return slot.CanPlace(carried);       // empty slot
        if(t.itemId != carried.itemId || slot.MaxStackFor(t) <= 1)return false;
        if(t.amount >= slot.MaxStackFor(t))return false;                  // full stack
        return slot.CanPlace(carried);                                    // same stack, merge
    }

    // DRAG_END (vanilla QUICK_CRAFT release): one settlement for every slot
    // the cursor dragged over. targets are collected (deduped, pre-filtered
    // by CanReceive) by the UIManager drag session. Left drag spreads carried
    // as evenly as possible - remainder items go to the earliest targets;
    // right drag places one item per target until carried runs out. Amounts
    // truncated by a slot's capacity stay on the cursor; conservation holds.
    public static void DragEnd(IReadOnlyList<ISlotAccess> targets, bool rightDrag, ItemStack carried)
    {
        if(targets == null || targets.Count == 0)return;
        if(carried == null || carried.IsEmpty())return;
        int n = targets.Count;

        // Left drag: even spread. When carried.amount < n, per == 0 degrades
        // naturally to "place 1 into the first carried.amount slots".
        if(!rightDrag)
        {
            int per = carried.amount / n;
            int rem = carried.amount % n;          // first `rem` targets get one extra
            for(int i = 0; i < n && !carried.IsEmpty(); i++)
            {
                var target = targets[i];
                var t = target.Get();
                if(t == null)continue;
                bool empty = t.IsEmpty();
                int remain = target.MaxStackFor(empty ? carried : t) - (empty ? 0 : t.amount);
                int put = Mathf.Min(per + (i < rem ? 1 : 0), remain);
                if(put <= 0)continue;
                if(empty)
                {
                    t.itemId = carried.itemId;
                    t.amount = put;
                }
                else t.amount += put;              // same item checked by CanReceive
                carried.amount -= put;
                if(carried.amount == 0)carried.Clear();
                target.MarkChanged();
            }
            return;
        }

        // Right drag: one per target in collection order.
        foreach(var target in targets)
        {
            if(carried.IsEmpty())break;
            target.PlaceOne(carried);
            if(carried.IsEmpty())carried.Clear();
            target.MarkChanged();
        }
    }
}
