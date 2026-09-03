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
}
