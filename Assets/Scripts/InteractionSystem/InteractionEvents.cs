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

// public class UseItemOnBlockEntity : GameEvent
// {
//     public Entity entity;
//     public Vector3Int HitBlockCoord; // Dimension Coord;
//     public Vector3 HitNormal;

//     public ItemStack HoldingItem;
//     public ItemDefinition ItemDef;

//     public ushort BlockId;
//     public BlockDefinition BlockDef;
//     public BlockEntity blockEntity;
//     public BlockEntityDefinition BlockEntityDef;
//     public ItemUseResult Result;
// }

public class UseItem : GameEvent
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
