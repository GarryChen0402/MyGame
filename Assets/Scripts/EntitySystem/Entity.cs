using System;
using System.Collections.Generic;
using UnityEngine;

public class Entity // Data Class
{
    public List<AABB> AABBs = new();
    public AABB MainBox => AABBs[0];
    
    public Vector3 Position => MainBox.Pivot;
    public Inventory inventory;
    public float pitch = 0;
    public float yaw = 0;
    public ushort DimensionId;
    public RaycastHit CurrentRaycastHitResult;
    // Inventory slot this entity currently holds (drives IsHoldingItem and
    // item use; updated by the input layer).
    public int SelectedSlotIndex;

    public Entity()
    {
        EventBus.Instance.Publish(new SummonEntity(){entity = this});
    }

    public void OnDestroy() => EventBus.Instance.Publish(new DestroyEntity(){entity = this});

    public void Move(Vector3 motion)
    {
        //TODO Use the Move logic like mc, get the MoveResult from PhysicsManager
        PhysicsManager.Instance.MoveEntity(this, motion);
    }

    public virtual void OnUpdate(float deltaTime)
    {
        ProcessInteractionSession(deltaTime);
    }
    public virtual void ProcessInteractionSession(float dt)
    {
        if (Session.Completed)return;
        if(!Input.GetKey(Session.bindKey))Session.Completed = true;//Interupt
        if(Session.TargetType == InteractionSessionTargetType.Block && (!CurrentRaycastHitResult.IsHit || CurrentRaycastHitResult.BlockDimensionCoord != Session.blockDimCoord))Session.Completed = true;
        // else if(Session.TargetType == InteractionSessionTargetType.Entity && ())// TODO: add the raycast to raycast the entity
        else if(Session.TargetType == InteractionSessionTargetType.Item && GetCurrentHoldingItemStack() != Session.itemStack)Session.Completed = true;
        if(Session.Completed)return;
        Session.Durantion += GetSessionUpdateTime(Session.TargetType, dt);
        if(Session.Durantion >= Session.CompleteTime)
        {
            Session.OnComplete.Invoke();
            Session.Completed = true;
        }
    }
    public virtual float GetSessionUpdateTime(InteractionSessionTargetType targetType, float dt) => dt;

    public virtual bool IsHoldingItem() => false;
    public virtual ItemStack GetCurrentHoldingItemStack() => null;
    
    public InteractionSessionContext Session {get; private set;} = new();
    public void SetSession(KeyCode key, InteractionSessionTargetType type, Action OnComplete, float CompleteTime, Entity entity = null, Vector3Int blockCoord = default, ItemStack itemStack = null)
    {
        Session.bindKey = key;
        Session.CompleteTime = CompleteTime;
        Session.Durantion = 0;
        Session.TargetType = type;
        Session.Completed = false;
        Session.blockDimCoord = blockCoord;
        Session.entity = entity;
        Session.itemStack = itemStack;
        Session.OnComplete = OnComplete;
        if(itemStack != null && ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(itemStack.itemId, out var itemDefinition))
            Session.ItemDef = itemDefinition;
        if(!WorldManager.Instance.TryGetDimension(DimensionId, out var dim))return;
        ushort stateId = dim.GetBlockAt(blockCoord);
        if(!ResourceSystem.Instance.BlockStates.TryGetResourceWithNumberId(stateId, out var blockState))return;
        Session.blockId = blockState.BlockId;
        Session.BlockDef = blockState.Block;
        if (!Session.BlockDef.HasBlockEntity)
        {
            Session.blockEntity = null;
            Session.BlockEntityDef = null;
        }
        else
        {
            if(!WorldManager.Instance.TryGetBlockEntity(DimensionId, blockCoord, out var be))return;
            Session.blockEntity = be;
            Session.BlockEntityDef = be.Definition;
        }

    }

    public virtual void ConsumeItemUseResult(ItemUseResult result){}
}