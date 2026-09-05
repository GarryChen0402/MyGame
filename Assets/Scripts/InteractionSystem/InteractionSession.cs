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
    public ushort blockId;
    public BlockDefinition BlockDef;
    public BlockEntity blockEntity;
    public BlockEntityDefinition BlockEntityDef;
    public float CompleteTime;
    public float Durantion;

    public Action OnComplete;
}