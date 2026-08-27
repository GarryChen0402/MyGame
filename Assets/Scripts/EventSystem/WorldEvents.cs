using UnityEngine;

public class BlockChangedEvent
{
    public Vector2Int ChunkCoord;
    public Vector3Int ChunkLocalCoord;
    public ushort NewBlockId;

    public BlockChangedEvent(Vector2Int coord, Vector3Int localPos, ushort id)
    {
        ChunkCoord = coord;
        ChunkLocalCoord = localPos;
        NewBlockId = id;
    }
}