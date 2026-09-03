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
    public KeyCode bindKey;
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