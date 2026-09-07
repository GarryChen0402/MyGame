using System;
using System.Collections.Generic;
using UnityEngine;

public class Player : LivingEntity, ICraftingGridHost
{
    private static Player instance = new();
    public static Player Instance => instance;

    // Player hurt channel main handler (mob-symmetric, design doc 规则 §4 切分):
    // the attacker publishes PlayerHurtEvent and this static ctor registration
    // applies the shared Hurt flow synchronously. Death stays broadcast-only
    // (Dead publishes PlayerDeadEvent; no self-subscription, it would re-enter
    // Dead on its own broadcast).
    private static readonly Action<PlayerHurtEvent> hurtHandler = OnHurtEvent;

    static Player()
    {
        EventBus.Instance.Subscribe(hurtHandler);
        // Resident mirror sources bind here (Phase C rule R-C1-0): the static
        // instance above is fully constructed by this point, and the call
        // rides on the first static access of Player - exactly the current
        // creation point, never earlier (a premature Player would fire
        // SummonEntityEvent before the logic managers subscribe).
        MirrorSync.Instance.RegisterPlayerSources();
    }

    private static void OnHurtEvent(PlayerHurtEvent evt)
        => evt.player.Hurt(evt.attacker, evt.amount, evt.knockbackVelocity);

    // MC: 1.8-tall player box, eyes sit at 1.62 above the feet.
    public const float EyeHeight = 1.62f;

    public const float MaxHealthValue = 20f;   // MC player 20 HP; no def resource in v1

    // Speed constants moved in from the input layer (rules M2, design doc 玩家
    // 权威化与输入命令化-代码设计 §4.1): target horizontal speed and jump
    // velocity now apply on the logic side, where the intent is consumed.
    public const float HorizontalMoveSpeed = 5f;    // m/s target horizontal speed
    public const float JumpSpeed = 6.4f;            // m/s jump velocity (jump height ~1.28m, MC ~1.25 blocks)

    private const float HurtKnockbackDamping = 3f;   // hurt-window horizontal friction (slide ~ speed/3, mob-scale)

    // Input intent slot (input frames write, the player tick consumes - the
    // same-process stand-in for a network report packet; rule B1: the slot is
    // exactly where a Phase D network input would land).
    public PlayerIntent Intent { get; } = new();

    // Cursor-held stack (Phase C rule R-C1-6): the former UIManager.
    // HeldItemStack moved into logic state. ContainerCommandProcessor is its
    // only writer (slot click/drag settlements); the UI sees it through the
    // held mirror. Never persisted, and it intentionally keeps the legacy
    // linger-across-close semantics (design doc §9).
    public ItemStack CursorStack = new();

    // Tick-start Motion snapshot (design doc §4.1/§4.4): the validation
    // envelope's baseline, alongside the PrevPosition/PrevYaw/PrevPitch
    // render snapshot taken in the same pass.
    public Vector3 PrevMotion;

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

    // The rule §3 three-step tick chain (design doc §4.2): intents become
    // authoritative physics input / action dispatches (1), the shared chain
    // runs - invincible timer, TickPhysics with the hurt damping above,
    // interaction session progress, buffs (2) - then the tick's move is
    // validated against the reachable envelope, rolling back on violation (3).
    public override void OnUpdate(float dt)
    {
        ConsumeInputIntent();
        base.OnUpdate(dt);
        ResolveMoveValidation();
    }

    public override void SnapshotTickStart()
    {
        base.SnapshotTickStart();   // PrevPosition/PrevYaw/PrevPitch for render interpolation
        PrevMotion = Motion;        // validation baseline (design doc §4.4)
    }

    // (1) Intent consumption (design doc §4.3): input axes rotate by the
    // authoritative yaw into the target horizontal speed - the former
    // input-frame write, now on the tick boundary (rule B5). The jump edge
    // fires only grounded; the grounded gate moved from the input frame to
    // the logic side (rule M2). The hurt window applies neither: the
    // knockback residual slides out through the TickPhysics damping above
    // instead (the input layer stops producing intents too - double lock).
    private void ConsumeInputIntent()
    {
        if(InvincibleTimer <= 0f)
        {
            float yawRad = yaw * Mathf.Deg2Rad;
            Vector3 forward = new(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
            Vector3 right = new(Mathf.Cos(yawRad), 0f, -Mathf.Sin(yawRad));
            Vector3 target = (forward * Intent.move.x + right * Intent.move.y) * HorizontalMoveSpeed;
            Motion.x = target.x;
            Motion.z = target.z;
            if(Intent.jumpRequested && IsOnGround)Motion.y = JumpSpeed;
            Intent.jumpRequested = false;
        }
        if(Intent.action != null)
        {
            PlayerActionRequest req = Intent.action;
            Intent.action = null;   // single-slot edge: cleared before dispatch (no re-entry)
            // Interactions stay live in the hurt window (clicks land while
            // staggered, as before); only a fresh mining start waits - the
            // stagger flings the player off the clicked block anyway.
            if(InvincibleTimer <= 0f || req.kind != PlayerActionKind.AttackBlock)
                InteractionManager.Instance.ProcessPlayerActionRequest(this, req);
        }
        Intent.move = Vector2.zero;   // frame-written state, cleared per consume: a tick without a fresh input frame (multi-tick catch-up) must not repeat the last frame's direction
    }

    // (3) Move validation against this tick's reachable envelope (rules M3/M4,
    // design doc §4.4): the limits derive from this domain's own physics - the
    // target speed and the tick-start Motion - so every legitimate physics step
    // fits inside and only an out-of-domain position change (a future networked
    // injector) trips it. Truncations (landing/walls) only shrink the moved
    // distance; the entity-push pass runs after this method in
    // EntityManager.Update, so pushed displacement is exempt by construction
    // (rule M5).
    private void ResolveMoveValidation()
    {
        Vector3 moved = Position - PrevPosition;
        float dt = GameClock.TickInterval;

        float prevHoriz = Mathf.Sqrt(PrevMotion.x * PrevMotion.x + PrevMotion.z * PrevMotion.z);
        float hLimit = (Mathf.Max(HorizontalMoveSpeed, prevHoriz) * 1.5f + 0.5f) * dt + 0.1f;
        float hMoved = Mathf.Sqrt(moved.x * moved.x + moved.z * moved.z);
        if(hMoved > hLimit)RejectMovement(hMoved, hLimit, "horizontal");

        // Vertical envelope: falls stay within one gravity step of the
        // tick-start speed (landing only truncates); rises are bounded by the
        // tick-start upward motion (jump/knockback keep y).
        float vy = PrevMotion.y;
        float fallMax = vy <= 0f ? -vy * dt + Gravity * dt * dt * 2f : 0.15f;
        float riseMax = vy > 0f ? vy * dt + 0.25f : 0.35f;
        if(moved.y < -fallMax || moved.y > riseMax)RejectMovement(moved.y, 0f, "vertical");
    }

    // Authority rollback (rule M4): rewind to the tick-start state - the only
    // server-known position in the same-process model - and kill the drift
    // source. Local play should never reach this branch (design doc §4.4
    // implementation note: full rewind to PrevPosition); it exists to bound
    // future networked input.
    private void RejectMovement(float got, float limit, string axis)
    {
        Debug.LogWarning($"[Player] move rejected on {axis} axis: {got:F3} > {limit:F3}; rewound to tick start (moved too quickly)");
        AABBs[0] = new AABB(
            PrevPosition - new Vector3(0.3f, 0f, 0.3f),
            PrevPosition + new Vector3(0.3f, 1.8f, 0.3f));   // 0.6-wide, 1.8-tall player box (feet-center pivot)
        Motion.x = Motion.z = 0f;
        if(Motion.y > 0f)Motion.y = 0f;   // an airborne reject must not re-apply itself next tick
    }

    // Player death channel keeps its own shape (design doc §4 separation):
    // hurt enters eventified - the attacker publishes PlayerHurtEvent and the
    // static handler above runs the shared virtual LivingEntity.Hurt. At zero
    // health Dead broadcasts PlayerDeadEvent with the killer, then runs v1's
    // instant full-HP reset in place. No corpse state - IsDead never turns
    // true, the player keeps playing.
    protected override void Dead(Entity attacker)
    {
        EventBus.Instance.Publish(new PlayerDeadEvent { attacker = attacker });   // zombie AI releases its chase lock
        CurrentHealth = MaxHealth.CurrentValue;             // v1: instant full reset in place
        // TODO full death flow (respawn / scene reset) replaces the instant reset
    }

    public ValueEntry AttackPoint;
}