
using UnityEngine;

public static class CoordUtils
{
    public static readonly int BlockSizePerChunk = 16;
    public static readonly int MinSubChunkYIndex = -8;
    public static readonly int MaxSubChunkYIndex = 16;

    public static Vector2Int WorldPosToChunkCoord(Vector3 worldPos)
    {
        return new Vector2Int
        (
            Mathf.FloorToInt(worldPos.x / BlockSizePerChunk),
            Mathf.FloorToInt(worldPos.z / BlockSizePerChunk)
        );
        
    }

    public static Vector3Int WorldPosToBlockCoord(Vector3 worldPos)
    {
        return new Vector3Int
        (
            Mathf.FloorToInt(worldPos.x / BlockSizePerChunk),
            Mathf.FloorToInt(worldPos.y / BlockSizePerChunk),
            Mathf.FloorToInt(worldPos.z / BlockSizePerChunk)
        );
    }

    public static Vector3 BlockCoordToWorldPos(Vector3Int blockCoord)
    {
        return new Vector3
        (
            blockCoord.x * BlockSizePerChunk * 1.0f,
            blockCoord.y * BlockSizePerChunk * 1.0f,
            blockCoord.z * BlockSizePerChunk * 1.0f
        );
    }

    public static Vector3 ChunkCoordToWorldPos(Vector2Int ChunkCoord)
    {
        return new Vector3
        (
            ChunkCoord.x * BlockSizePerChunk * 1.0f,
            0.0f,
            ChunkCoord.y * BlockSizePerChunk * 1.0f
        );
    }

    public static Vector3Int WorldPosToSubChunkLocalCoord(Vector3 worldPos)
    {
        return new Vector3Int
        (
            (Mathf.FloorToInt(worldPos.x) % BlockSizePerChunk + BlockSizePerChunk) % BlockSizePerChunk,
            (Mathf.FloorToInt(worldPos.y) % BlockSizePerChunk + BlockSizePerChunk) % BlockSizePerChunk,
            (Mathf.FloorToInt(worldPos.z) % BlockSizePerChunk + BlockSizePerChunk) % BlockSizePerChunk
        );
    }

    public static Vector3 SubChunkCoordToWorldPos(Vector2Int ChunkCoord, int YCoord)
    {
        return new Vector3
        (
            ChunkCoord.x * BlockSizePerChunk * 1.0f,
            YCoord * BlockSizePerChunk * 1.0f,
            ChunkCoord.y * BlockSizePerChunk * 1.0f
        );
    }

    public static bool IsCorrectSubChunkLocalCoord(int x, int y, int z)
    {
        return x >= 0 && x < BlockSizePerChunk 
            && y >= 0 && y < BlockSizePerChunk
            && z >= 0 && z < BlockSizePerChunk;
    }

    public static bool IsCorrectChunkLocalCoord(int x, int y, int z)
    {
        return x >= 0 && x < BlockSizePerChunk 
            && y >= MinSubChunkYIndex * BlockSizePerChunk && y < MaxSubChunkYIndex * BlockSizePerChunk
            && z >= 0 && z < BlockSizePerChunk;
    }

    public static int WorldYPosToSubChunkYIndex(float y)
    {
        return Mathf.FloorToInt(y / BlockSizePerChunk);
    }

    public static int ChunkLocalYToSubChunkLocalY(int y) => (y % BlockSizePerChunk + BlockSizePerChunk) % BlockSizePerChunk;
}