using System.Collections.Generic;
using UnityEngine;

public class Chunk 
{
    public Vector2Int ChunkCoord {get; private set;}
    public int MinSubChunkIndex {get; private set;}
    public int MaxSubChunkIndex {get; private set;}

    private readonly SubChunk[] subChunks;

    public Chunk(Vector2Int chunkCoord, int minSubChunkIndex, int maxSubChunkIndex)
    {
        ChunkCoord = chunkCoord;
        MinSubChunkIndex = minSubChunkIndex;
        MaxSubChunkIndex = maxSubChunkIndex;
        // For example : min = -4 max = 16 ==> block range : [-4 * 16 = -64 ,  16 * 16 = 256) , full subChunk count : 16 - ( -4 ) + 1 = 21 
        subChunks = new SubChunk[maxSubChunkIndex - minSubChunkIndex +1];
    }

    public bool IsCorrectChunkLocalCoord(int x, int y, int z)
    {
        return x >= 0 && x < SubChunk.SubChunkBlockSize
            && z >= 0 && z < SubChunk.SubChunkBlockSize
            && y >= MinSubChunkIndex * SubChunk.SubChunkBlockSize
            && y < (MaxSubChunkIndex + 1) * SubChunk.SubChunkBlockSize;
    }

    public bool IsCorrectChunkLocalCoord(Vector3Int ChunkLocalCoord)
    {
        return ChunkLocalCoord.x >= 0 && ChunkLocalCoord.x < SubChunk.SubChunkBlockSize
            && ChunkLocalCoord.z >= 0 && ChunkLocalCoord.z < SubChunk.SubChunkBlockSize
            && ChunkLocalCoord.y >= MinSubChunkIndex * SubChunk.SubChunkBlockSize
            && ChunkLocalCoord.y < (MaxSubChunkIndex + 1) * SubChunk.SubChunkBlockSize;
    }

    public static Vector3Int DimensionCoordToChunkLocalCoord(Vector3Int dimensionCoord)
    {
        return new Vector3Int
        (
            (dimensionCoord.x % SubChunk.SubChunkBlockSize + SubChunk.SubChunkBlockSize) % SubChunk.SubChunkBlockSize,
            dimensionCoord.y,
            (dimensionCoord.z % SubChunk.SubChunkBlockSize + SubChunk.SubChunkBlockSize) % SubChunk.SubChunkBlockSize
        );
    }

    public int DimensionYCoordToSubChunkYIndex(int yCoord)
    {
        return Mathf.FloorToInt(yCoord * 1.0f / SubChunk.SubChunkBlockSize) - MinSubChunkIndex;
    }

    public ushort GetBlockAt(Vector3Int chunkLocalCoord)
    {
        if(!IsCorrectChunkLocalCoord(chunkLocalCoord))return 0;
        int subChunkIndex = DimensionYCoordToSubChunkYIndex(chunkLocalCoord.y);
        if(subChunks[subChunkIndex] == null)return 0;
        else return subChunks[subChunkIndex].GetBlockAt(SubChunk.BlockCoordToSubChunkLocalCoord(chunkLocalCoord));
    }

    // Suppresses BlockChangedEvent publishing (chunk generation fills thousands of
    // blocks at once; the renderer rebuilds once on ChunkLoaded instead).
    public bool SilentMode { get; set; }

    public bool TrySetBlockAt(Vector3Int chunkLocalCoord, ushort blockId, bool fromInteraction = false)
    {
        if(!IsCorrectChunkLocalCoord(chunkLocalCoord))return false;
        int subChunkIndex = DimensionYCoordToSubChunkYIndex(chunkLocalCoord.y);
        if(subChunks[subChunkIndex] == null)subChunks[subChunkIndex] = CreateNewSubChunk(subChunkIndex);
        bool ok = subChunks[subChunkIndex].TrySetBlockAt(SubChunk.BlockCoordToSubChunkLocalCoord(chunkLocalCoord), blockId);
        if(ok && !SilentMode)
            EventBus.Instance.Publish(new BlockChangedEvent(ChunkCoord, chunkLocalCoord, blockId){FromInteraction = fromInteraction});
        return ok;
    }

    public bool TryBreakBlockAt(Vector3Int chunkLocalCoord, bool fromInteraction = false)
    {
        if(!IsCorrectChunkLocalCoord(chunkLocalCoord))return false;
        int subChunkIndex = DimensionYCoordToSubChunkYIndex(chunkLocalCoord.y);
        if(subChunks[subChunkIndex] == null)return false;
        ushort oldBlockId = subChunks[subChunkIndex].GetBlockAt(SubChunk.BlockCoordToSubChunkLocalCoord(chunkLocalCoord));
        bool ok = subChunks[subChunkIndex].TryBreakBlockAt(SubChunk.BlockCoordToSubChunkLocalCoord(chunkLocalCoord));
        if(ok && !SilentMode)
            EventBus.Instance.Publish(new BlockChangedEvent(ChunkCoord, chunkLocalCoord, oldBlockId){FromInteraction = fromInteraction});
        return ok;
    }

    private SubChunk CreateNewSubChunk(int subChunkIndex)
    {
        return new SubChunk(ChunkCoord, subChunkIndex + MinSubChunkIndex);
    }

    // Returns the subchunk at the given dimension subchunk index, or null when out of range.
    public SubChunk GetSubChunk(int subChunkIndexInChunk)
    {
        int idx = subChunkIndexInChunk - MinSubChunkIndex;
        if (idx < 0 || idx >= subChunks.Length) return null;
        return subChunks[idx];
    }

    public int DistanceTo(Chunk other)
    {
        if(other == null)return int.MaxValue;
        return Mathf.Abs(ChunkCoord.x - other.ChunkCoord.x) + Mathf.Abs(ChunkCoord.y - other.ChunkCoord.y);
    }

    public int DistanceTo(Vector2Int other)
        => Mathf.Abs(ChunkCoord.x - other.x) + Mathf.Abs(ChunkCoord.y - other.y);
}