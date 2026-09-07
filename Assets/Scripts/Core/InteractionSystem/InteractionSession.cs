using System;
using UnityEngine;

public enum InteractionSessionTargetType
{
    Block,
    Entity,
    Item
}

public class InteractionSessionContext
{
    // True for sessions an AI started (mob swings): Entity.ProcessInteractionSession
    // skips its operator interruption rules for these, the brain owns teardown.
    public bool IsAIControlled;
    // Action full name whose hold state keeps the session alive (rebind-aware).
    public string bindingFullName;
    public InteractionSessionTargetType TargetType;
    public bool Completed = false;
    public ItemStack itemStack;
    public ItemDefinition ItemDef;
    public Entity entity;
    // public EntityDefinition EntityDef;

    public Vector3Int blockDimCoord;
    // Hit face normal of the raycast the session was started from (block-target
    // sessions only; the completed session re-publishes it with the original
    // interaction events, whose consumers derive facing/placement from it).
    public Vector3 HitNormal;
    public ushort blockId;
    public BlockDefinition BlockDef;
    public BlockEntity blockEntity;
    public BlockEntityDefinition BlockEntityDef;
    public float CompleteTime;
    public float Durantion;

    public Action OnComplete;
}