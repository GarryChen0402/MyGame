using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Video;

public class Entity // Data Class
{
    // Stable instance id (Phase C R-C2-2): keys the render shell table and
    // identifies publishers in view-event DTOs. Logic never consumes it.
    public readonly int EntityId;
    private static int nextEntityId = 1;

    public List<AABB> AABBs = new();
    public AABB MainBox => AABBs[0];
    
    public Vector3 Position => MainBox.Pivot;

    // State at the start of the current game tick (rendered via partial-tick
    // interpolation - design doc 固定Tick时钟与渲染插值改造-代码设计.md §3):
    // EntityManager snapshots every entity before any OnUpdate runs, so these
    // always equal "last tick's end state" (MC lastTickPos alike).
    public Vector3 PrevPosition;
    public float PrevYaw;
    public float PrevPitch;
    public InventoryDataContainer inventory;
    public float pitch = 0;
    public float yaw = 0;
    public ushort DimensionId;
    public RaycastHit CurrentRaycastHitResult;
    // Inventory slot this entity currently holds (drives IsHoldingItem and
    // item use; updated by the input layer).
    public int SelectedSlotIndex;

    public const float Gravity = 16f;
    public const float HurtInvincibleSeconds = 0.5f;

    public Vector3 Motion;
    public float InvincibleTimer;
    protected bool OnGround;
    // True on frames TickPhysics truncated horizontal motion against a wall
    // (x/z axis zeroed). Reset every tick; wander uses it to stop bumping
    // (design doc §3.1.2).
    public bool HorizontalBlocked {get; private set;}

    public Entity()
    {
        EntityId = nextEntityId++;
        // Session slot starts idle: Completed true = "no session in flight"
        // (the SetSession gate rejects new requests while one is active).
        Session.Completed = true;
        EventBus.Instance.Publish(new SummonEntityEvent(){entity = this});
    }

    public void OnDestroy()
    {
        EventBus.Instance.Publish(new DestroyEntity(){entity = this});   // logic dereg chain (PhysicsManager AABB)
        // Render-side teardown (rule R-C2-3): the shell manager subscribed to
        // the DTO event pair, so its table can key on EntityId alone.
        MirrorSync.Instance.UnregisterEntityMirror(EntityId);
        EventBus.Instance.Publish(new EntityShellDespawnEvent(){entityId = EntityId});
    }

    public void Move(Vector3 motion)
    {
        //TODO Use the Move logic like mc, get the MoveResult from PhysicsManager
        PhysicsManager.Instance.MoveEntity(this, motion);
    }

    // Tick-boundary snapshot, called by EntityManager.Update for every entity
    // before any of them runs OnUpdate. Position only changes in TickPhysics
    // and EntityPushResolver, both of which run after this - so prev fields
    // hold the tick-start state for render interpolation.
    public virtual void SnapshotTickStart()
    {
        PrevPosition = Position;
        PrevYaw = yaw;
        PrevPitch = pitch;
    }

    public virtual void OnUpdate(float deltaTime)
    {
        TickInvincible(deltaTime);
        TickPhysics(deltaTime);
        ProcessInteractionSession(deltaTime);
    }

    protected void TickInvincible(float deltaTime)
        => InvincibleTimer = Mathf.Max(0f, InvincibleTimer - deltaTime);
    
    protected virtual void TickPhysics(float dt)
    {
        HorizontalBlocked = false;
        Motion.y -= Gravity * dt;
        Vector3 desired = Motion * dt;
        MoveResult r = PhysicsManager.Instance.MoveEntity(this, desired);
        OnGround = r.OnGround;
        if(r.OnGround)Motion.y = 0f;
        if(Mathf.Abs(r.Motion.x) < Mathf.Abs(desired.x) - 1e-4f)
        {
            Motion.x = 0f;
            HorizontalBlocked = true;
        }
        if(Mathf.Abs(r.Motion.y) < Mathf.Abs(desired.y) - 1e-4f)Motion.y = 0f;
        if(Mathf.Abs(r.Motion.z) < Mathf.Abs(desired.z) - 1e-4f)
        {
            Motion.z = 0f;
            HorizontalBlocked = true;
        }
    }
    

    public virtual void ProcessInteractionSession(float dt)
    {
        if (Session.Completed)return;
        // AI-driven sessions (e.g. a mob's attack swing) bypass the operator
        // rules below: there is no input held, no crosshair, and the target
        // lock lives in the brain. The owning state aborts them instead.
        if(!Session.IsAIControlled)
        {
            // Interrupt when the action is no longer held; IsDown also gates on the
            // active input context, so opening a panel stops an in-progress break.
            // Instant sessions (CompleteTime <= 0, e.g. the click attack) settle on
            // the next tick without requiring the key to stay held - one click edge
            // creates one session, which completes exactly once.
            if(Session.CompleteTime > 0 && !KeyBindingManager.Instance.IsDown(Session.bindingFullName))Session.Completed = true;
            if(Session.TargetType == InteractionSessionTargetType.Block && (!CurrentRaycastHitResult.IsHit || CurrentRaycastHitResult.BlockDimensionCoord != Session.blockDimCoord))Session.Completed = true;
            // Entity swing: keep going only while the crosshair stays on the session
            // target (the raycast result refreshes every frame) and it is alive.
            // (Players never set IsDead - the v1 reset keeps them alive - so the
            // LivingEntity check behaves exactly like the former MobEntity one.)
            else if(Session.TargetType == InteractionSessionTargetType.Entity && (CurrentRaycastHitResult.HitEntity != Session.entity
                    || (Session.entity is LivingEntity living && living.IsDead)))Session.Completed = true;
            else if(Session.TargetType == InteractionSessionTargetType.Item && GetCurrentHoldingItemStack() != Session.itemStack)Session.Completed = true;
        }
        if (Session.Completed)
        {
            EventBus.Instance.Publish(new InteractionSessionContextInteruptedEvent(){Data = SessionSnapshot()});
            return;
        }
        Session.Durantion += GetSessionUpdateTime(Session.TargetType, dt);
        EventBus.Instance.Publish(new InteractionSessionContextTickEvent(){Data = SessionSnapshot()});

        if(Session.Durantion >= Session.CompleteTime)
        {
            SettleCompletedSession();
        }

    }

    // Pure-data snapshot of the running session for the render-facing events
    // (rule R-C2-3): values are read at publish time, so a subscriber always
    // sees the settled state of the tick that fired the event.
    private SessionEventData SessionSnapshot()
    {
        var s = Session;
        return new SessionEventData
        {
            operatorId = EntityId,
            targetType = s.TargetType,
            bindingFullName = s.bindingFullName,
            isAIControlled = s.IsAIControlled,
            blockDimCoord = s.blockDimCoord,
            durantion = s.Durantion,
            completeTime = s.CompleteTime
        };
    }

    // Settles the running session: the Completed flag flips before the success
    // callback runs - an exception thrown inside a callback (or one of its
    // event subscribers) must not wedge the session slot open, or the frame
    // loop would re-settle it forever. Shared by the frame accumulation path
    // above and the instant path inside SetSession.
    private void SettleCompletedSession()
    {
        Session.Completed = true;
        Session.OnComplete.Invoke();
        EventBus.Instance.Publish(new InteractionSessionContextCompletedEvent(){Data = SessionSnapshot()});
    }

    public virtual float GetSessionUpdateTime(InteractionSessionTargetType targetType, float dt) => dt;

    public virtual bool IsHoldingItem() => false;
    public virtual ItemStack GetCurrentHoldingItemStack() => null;
    
    public InteractionSessionContext Session {get; private set;} = new();
    // One session slot per entity: an in-flight session (not yet Completed)
    // rejects a new request (returns false) so the operator keeps mining/
    // eating undisturbed - the requester just drops its click. Settles
    // instantly (CompleteTime <= 0) inside this call: the operator's frame
    // state (raycast, held stack) is still the one the click saw, so the use
    // lands against its original target - equivalent to the pre-session
    // publish-and-settle flow the interaction events used to run on.
    public bool SetSession(string bindingFullName, InteractionSessionTargetType type, Action OnComplete, float CompleteTime, Entity entity = null, Vector3Int blockCoord = default, ItemStack itemStack = null, Vector3 hitNormal = default)
    {
        if(!Session.Completed)return false;
        Session.bindingFullName = bindingFullName;
        Session.CompleteTime = CompleteTime;
        Session.Durantion = 0;
        Session.TargetType = type;
        Session.Completed = false;
        Session.blockDimCoord = blockCoord;
        Session.entity = entity;
        Session.itemStack = itemStack;
        Session.HitNormal = hitNormal;
        Session.OnComplete = OnComplete;
        ushort stateId = 0;
        if(itemStack != null && ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(itemStack.itemId, out var itemDefinition))
            Session.ItemDef = itemDefinition;
        if(WorldManager.Instance.TryGetDimension(DimensionId, out var dim))
        {
            stateId = dim.GetBlockAt(blockCoord);
            if(ResourceSystem.Instance.BlockStates.TryGetResourceWithNumberId(stateId, out var blockState))
            {
                Session.blockId = blockState.BlockId;
                Session.BlockDef = blockState.Block;
                if (!Session.BlockDef.HasBlockEntity)
                {
                    Session.blockEntity = null;
                    Session.BlockEntityDef = null;
                }
                else
                {
                    if(WorldManager.Instance.TryGetBlockEntity(DimensionId, blockCoord, out var be))
                    {
                        Session.blockEntity = be;
                        Session.BlockEntityDef = be.Definition;
                    }
                }
            }
        }
        // The mined target's state id rides the Start payload so the crack
        // overlay can build its geometry without a render-side block query
        // (design C-2 §8.1); the block cannot change mid-session without the
        // frame interruption rule above firing first.
        var startData = SessionSnapshot();
        startData.blockStateId = stateId;
        EventBus.Instance.Publish(new InteractionSessionContextStartEvent()
        {
            Data = startData
        });

        if(CompleteTime <= 0f)SettleCompletedSession();   // instant sessions settle on the click frame
        return true;
    }

    // Abort an in-progress session without completing it (used by AI states
    // leaving an attack early, and by the owner's death): same Interrupted
    // event flow as the frame rule above, just operator-driven.
    public void AbortInteractionSession()
    {
        if(Session.Completed)return;
        Session.Completed = true;
        EventBus.Instance.Publish(new InteractionSessionContextInteruptedEvent(){Data = SessionSnapshot()});
    }

    public virtual void ConsumeItemUseResult(ItemUseResult result){}
}