using UnityEngine;

public class BlockChangedEvent : GameEvent
{
    public Chunk Chunk;
    public Vector2Int ChunkCoord;
    public Vector3Int ChunkLocalCoord;
    public ushort NewStateId;   // global block state id (0 = air, i.e. broken)
    public bool FromInteraction;
    public BlockChangedEvent(Chunk chunk, Vector3Int localPos, ushort stateId)
    {
        Chunk = chunk;
        ChunkCoord = chunk.ChunkCoord;
        ChunkLocalCoord = localPos;
        NewStateId = stateId;
    }
}

// Published when a chunk's data becomes available (created or re-enabled).
// View subscribes to create a renderer; neighboring renderers rebuild exposed faces.
public class ChunkLoadedEvent : GameEvent
{
    public Chunk Chunk;
    public ChunkLoadedEvent(Chunk chunk) { Chunk = chunk; }
}

// Published when a chunk moves into the disabled pool. Its renderer must be
// destroyed; neighbors rebuild exposed faces.
public class ChunkUnloadedEvent : GameEvent
{
    public Chunk Chunk;
    public Vector2Int ChunkCoord;
    public ChunkUnloadedEvent(Chunk chunk)
    {
        Chunk = chunk;
        ChunkCoord = chunk.ChunkCoord;
    }
}
