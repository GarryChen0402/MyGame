using UnityEngine;

public class UseItemOnStaticBlock : GameEvent
{
    public Entity entity;
    public Vector3Int HitBlockCoord; // Dimension Coord;
    public Vector3 HitNormal;

    public ItemStack HoldingItem;
    public ItemDefinition ItemDef;

    public ushort BlockId;
    public BlockDefinition BlockDef;
    public ItemUseResult Result;
}

public class UseItemOnBlockEntity : GameEvent
{
    public Entity entity;
    public Vector3Int HitBlockCoord; // Dimension Coord;
    public Vector3 HitNormal;

    public ItemStack HoldingItem;
    public ItemDefinition ItemDef;

    public ushort BlockId;
    public BlockDefinition BlockDef;
    public BlockEntity blockEntity;
    public BlockEntityDefinition BlockEntityDef;
    public ItemUseResult Result;
}

public class UseItemEvent : GameEvent
{
    public Entity entity;
    public Vector3Int HitBlockCoord; // Dimension Coord;
    public Vector3 HitNormal;

    public ItemStack HoldingItem;
    public ItemDefinition ItemDef;

    public ItemUseResult Result;
}

public class InteractWithStaticBlock : GameEvent
{
    public Entity entity;
    public Vector3Int HitBlockCoord; // Dimension Coord;
    public Vector3 HitNormal;
    public ushort BlockId;
    public BlockDefinition BlockDef;
}

public class InteractWithBlockEntity : GameEvent
{
    public Entity entity;
    public Vector3Int HitBlockCoord; // Dimension Coord;
    public Vector3 HitNormal;
    public ushort BlockId;
    public BlockDefinition BlockDef;

    public BlockEntity blockEntity;
    public BlockEntityDefinition BlockEntityDef;
}

// Session event payload (rule R-C2-3): a pure-data snapshot taken at publish
// time. The former live InteractionSessionContext reference only reached
// render subscribers (CrackBlockRenderer / MobVisualSync), so the load is
// now DTO-only; logic-internal session state stays in InteractionSessionContext.
public class SessionEventData
{
    public int operatorId;                    // publisher's EntityId (shell self-compare)
    public InteractionSessionTargetType targetType;
    public string bindingFullName;
    public bool isAIControlled;
    public Vector3Int blockDimCoord;
    public int blockStateId;                  // mined target state id (filled by the Start publish)
    public float durantion;
    public float completeTime;
}

public class InteractionSessionContextStartEvent : GameEvent
{
    public SessionEventData Data;
}
public class InteractionSessionContextTickEvent : GameEvent
{
    public SessionEventData Data;
}
public class InteractionSessionContextCompletedEvent : GameEvent
{
    public SessionEventData Data;
}
public class InteractionSessionContextInteruptedEvent : GameEvent
{
    public SessionEventData Data;
}