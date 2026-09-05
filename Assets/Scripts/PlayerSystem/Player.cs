using System.Collections.Generic;
using UnityEngine;

public class Player : Entity, ICraftingGridHost
{
    private static Player instance = new();
    public static Player Instance => instance;

    // MC: 1.8-tall player box, eyes sit at 1.62 above the feet.
    public const float EyeHeight = 1.62f;

    public const float MaxHealthValue = 20f;   // MC player 20 HP; no def resource in v1
    public ValueEntry MaxHealth {get; private set;}
    public float CurrentHealth {get; private set;}

    private const float HurtKnockbackDamping = 3f;   // hurt-window horizontal friction (slide ~ speed/3, mob-scale)

    // Last TickPhysics ground result (Entity.OnGround), exposed to the input
    // layer so jump impulses only fire from the ground.
    public bool IsOnGround => OnGround;

    private float harvestSpeedMultiply = 1.0f;

    // Personal 2x2 crafting (design doc Docs/玩家界面-模型预览组件与2x2个人合成
    // 设计方案.md §5.1.3): containers are constructed without any block
    // entity - Host stays null, so their MarkDirty chain is a no-op and the
    // player save (written whole on a fixed cadence) is the persistence path.
    public InventoryDataContainer CraftingGrid;
    public InventoryDataContainer CraftingResult;
    public CraftingSolver Crafting;

    private Player()
    {
        // Spawn above the tallest biome surface (mountains reach ~106) so the
        // player falls onto the world from above; the box pivot lands on (0, 115, 0).
        AABBs.Add(new AABB()
        {
            MinRange = new Vector3(-0.3f, 114.1f, -0.3f),
            MaxRange = new Vector3( 0.3f, 115.9f, 0.3f)
        });

        MaxHealth = new ValueEntry(MaxHealthValue);
        CurrentHealth = MaxHealth.CurrentValue;

        inventory = new Inventory(36, false);
        InitCrafting();

        AttackPoint = new(1);
        AttackPoint.AddNewPart("critical", new ChancedModifier());
        AttackPoint.TrySetParam("critical", "chance", 0.8f);
        AttackPoint.TrySetParam("critical", "baseRatio", 5f);
    }

    private void InitCrafting()
    {
        CraftingGrid = new InventoryDataContainer(new DataContainerConfig
        {
            Parameters = JsonUtility.ToJson(new InventoryDataContainer.Config { Capacity = 4 })
        });
        // Module-only insert, mirroring the workbench result slot: players can
        // only ever take the preview out, never place into it.
        CraftingResult = new InventoryDataContainer(new DataContainerConfig
        {
            Parameters = JsonUtility.ToJson(new InventoryDataContainer.Config
            {
                Capacity = 1,
                InsertPolicy = ContainerAccess.Module,
                ExtractPolicy = ContainerAccess.Any
            })
        });
        Crafting = new CraftingSolver(CraftingGrid, CraftingResult, 2, 2,
            new List<string> { "universal:shaped", "universal:shapeless" }, null);
    }

    // ---- ICraftingGridHost (UI binds grid/result slot accessors to the player) ----

    public void OnGridChanged() => Crafting?.OnGridChanged();
    public void OnResultTaken() => Crafting?.OnResultTaken();

    // Overwrites state from a save file: AABB (0.6-wide, 1.8-tall player box,
    // position is the feet-center pivot), look direction, dimension, inventory
    // and the 2x2 crafting grid. Unknown string ids are skipped with a warning
    // instead of failing the load.
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

        // Fixed 36 slots first, so every GetItemStackAt(index) stays valid
        // (an out-of-range null slot would make clicks on empty backpack
        // slots silently no-op).
        inventory.itemStacks.Clear();
        for(int i = 0; i < inventory.MaxSlotCount; i++)
            inventory.itemStacks.Add(new ItemStack());
        // Restore each entry into the exact slot it was saved from, keeping
        // the backpack layout intact across save cycles. Entries without a
        // valid slotIndex (v1 saves) and entries whose saved slot is already
        // occupied fall back to the first empty slot in file order.
        foreach(var entry in data.inventory)
        {
            if(entry == null || entry.amount <= 0 || string.IsNullOrEmpty(entry.itemId)) continue;
            if(!ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(entry.itemId, out ushort itemId))
            {
                Debug.LogWarning($"[Player] unknown item '{entry.itemId}' in save; skipped");
                continue;
            }
            int slot = entry.slotIndex >= 0 && entry.slotIndex < inventory.itemStacks.Count
                ? entry.slotIndex : -1;
            if(slot < 0 || !inventory.GetItemStackAt(slot).IsEmpty())
                slot = inventory.itemStacks.FindIndex(s => s.IsEmpty());
            if(slot < 0)continue;   // no free slot left (duplicated/overflowing save)
            inventory.itemStacks[slot] = new ItemStack { itemId = itemId, amount = entry.amount };
        }

        // 2x2 crafting grid (old v1 saves carry no field -> stays empty). The
        // result slot is a runtime preview and is never persisted, same as the
        // workbench work container.
        if(data.craftingGrid != null)
            CraftingGrid.RestoreSave(new InventoryDataContainer.SaveData { slots = data.craftingGrid });
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

    // Hurt-window slide: the input layer stops writing horizontal during the
    // window, so the knockback residual decays here (player normally has no
    // idle friction - without this it would slide forever).
    protected override void TickPhysics(float dt)
    {
        if(InvincibleTimer > 0f)
        {
            Motion.x *= Mathf.Exp(-HurtKnockbackDamping * dt);
            Motion.z *= Mathf.Exp(-HurtKnockbackDamping * dt);
        }
        base.TickPhysics(dt);
    }

    // Mirror of MobEntity.Hurt (design doc §4): invincibility gate -> damage ->
    // window -> horizontal knockback write. Death keeps the entity in place
    // (PlayerDeadEvent, no DeathEntity) - respawn semantics land later.
    public void Hurt(float damage, Vector3 knockbackVelocity = default)
    {
        if(InvincibleTimer > 0f)return;
        CurrentHealth = Mathf.Clamp(CurrentHealth - damage, 0f, MaxHealth.CurrentValue);
        InvincibleTimer = HurtInvincibleSeconds;
        if(knockbackVelocity != Vector3.zero)
            Motion = new Vector3(knockbackVelocity.x, Motion.y, knockbackVelocity.z);

        Debug.Log($"[Player] took {damage:F1} damage -> {CurrentHealth:F1}/{MaxHealth.CurrentValue:F1} health");
        EventBus.Instance.Publish(new HurtEntity(){entity = this, amount = damage});

        if(CurrentHealth <= 0f)
        {
            EventBus.Instance.Publish(new PlayerDeadEvent());   // zombie AI unsubscribes its chase lock
            CurrentHealth = MaxHealth.CurrentValue;             // v1: instant full reset in place
            // TODO full death flow (respawn / scene reset) replaces the instant reset
        }
    }

    public ValueEntry AttackPoint;
}