using UnityEngine;

public class Player : Entity
{
    private static Player instance = new();
    public static Player Instance => instance;

    // MC: 1.8-tall player box, eyes sit at 1.62 above the feet.
    public const float EyeHeight = 1.62f;

    private float harvestSpeedMultiply = 1.0f;

    private Player()
    {
        // Spawn above the tallest biome surface (mountains reach ~106) so the
        // player falls onto the world from above; the box pivot lands on (0, 115, 0).
        AABBs.Add(new AABB()
        {
            MinRange = new Vector3(-0.3f, 114.1f, -0.3f),
            MaxRange = new Vector3( 0.3f, 115.9f, 0.3f)
        });

        inventory = new Inventory(36, false);
    }

    // Overwrites state from a save file: AABB (0.6-wide, 1.8-tall player box,
    // position is the feet-center pivot), look direction, dimension, inventory.
    // Unknown string ids are skipped with a warning instead of failing the load.
    public void RestoreFromSave(PlayerSaveData data)
    {
        AABBs[0] = new AABB(
            data.position - new Vector3(0.3f, 0f, 0.3f),
            data.position + new Vector3(0.3f, 1.8f, 0.3f)
        );
        pitch = data.pitch;
        yaw = data.yaw;

        if(!string.IsNullOrEmpty(data.dimensionId))
        {
            if(ResourceSystem.Instance.DimensionDefinitions.TryGetNumberId(data.dimensionId, out ushort dimId))
                DimensionId = dimId;
            else
                Debug.LogWarning($"[Player] unknown dimension '{data.dimensionId}' in save; keeping current");
        }

        inventory.itemStacks.Clear();
        foreach(var entry in data.inventory)
        {
            if(entry == null || entry.amount <= 0 || string.IsNullOrEmpty(entry.itemId)) continue;
            if(!ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(entry.itemId, out ushort itemId))
            {
                Debug.LogWarning($"[Player] unknown item '{entry.itemId}' in save; skipped");
                continue;
            }
            inventory.itemStacks.Add(new ItemStack { itemId = itemId, amount = entry.amount });
        }
        // Save files list non-empty entries only; refill the fixed 36 slots so
        // every GetItemStackAt(index) stays valid (an out-of-range null slot
        // would make clicks on empty backpack slots silently no-op).
        while(inventory.itemStacks.Count < inventory.MaxSlotCount)
            inventory.itemStacks.Add(new ItemStack());
    }

    public override bool IsHoldingItem()
    {
        var stack = GetCurrentHoldingItemStack();
        return stack != null && !stack.IsEmpty();
    }

    public override ItemStack GetCurrentHoldingItemStack()
    {
        return inventory.GetItemStackAt(SelectedSlotIndex);
    }

    public override void ConsumeItemUseResult(ItemUseResult result)
    {
        if(!IsHoldingItem())return;
        if(result.UseSuccess)GetCurrentHoldingItemStack().TryConsumeItem(result.ConsumeAmount);
    }

    public override float GetSessionUpdateTime(InteractionSessionTargetType targetType, float dt)
    {
        //TODo : Current logic is just a demo for test.
        if(targetType == InteractionSessionTargetType.Block)
        {
            float progress = harvestSpeedMultiply * dt;
            return progress;
        }
        return dt;
    }
}