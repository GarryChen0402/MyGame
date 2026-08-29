using UnityEngine;

public class BlockChangedEvent : GameEvent
{
    public Vector2Int ChunkCoord;
    public Vector3Int ChunkLocalCoord;
    public ushort NewBlockId;
    public bool FromInteraction;
    public BlockChangedEvent(Vector2Int coord, Vector3Int localPos, ushort id)
    {
        ChunkCoord = coord;
        ChunkLocalCoord = localPos;
        NewBlockId = id;
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
    public Vector2Int ChunkCoord;
    public ChunkUnloadedEvent(Vector2Int coord) { ChunkCoord = coord; }
}
