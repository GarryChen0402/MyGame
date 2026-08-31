using System.Collections.Generic;
using UnityEngine;

public class Chunk
{
    public Vector2Int ChunkCoord {get; private set;}
    public int MinSubChunkIndex {get; private set;}
    public int MaxSubChunkIndex {get; private set;}

    // Save tracking: IsModified marks player edits (generation fills run in
    // SilentMode and never set it); IsSavedToDisk tracks whether the current
    // content is already on disk, so a dirty chunk is only saved once per edit.
    public bool IsModified {get; private set;}
    public bool IsSavedToDisk {get; private set;}

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

    // Returns the global block state id (0 = air); see BlockStateRegistry.
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

    public bool TrySetBlockAt(Vector3Int chunkLocalCoord, ushort stateId, bool fromInteraction = false)
    {
        if(!IsCorrectChunkLocalCoord(chunkLocalCoord))return false;
        int subChunkIndex = DimensionYCoordToSubChunkYIndex(chunkLocalCoord.y);
        if(subChunks[subChunkIndex] == null)subChunks[subChunkIndex] = CreateNewSubChunk(subChunkIndex);
        bool ok = subChunks[subChunkIndex].TrySetBlockAt(SubChunk.BlockCoordToSubChunkLocalCoord(chunkLocalCoord), stateId);
        if(ok && !SilentMode)
        {
            MarkModified();
            EventBus.Instance.Publish(new BlockChangedEvent(this, chunkLocalCoord, stateId){FromInteraction = fromInteraction});
        }
        return ok;
    }

    public bool TryBreakBlockAt(Vector3Int chunkLocalCoord, bool fromInteraction = false)
    {
        if(!IsCorrectChunkLocalCoord(chunkLocalCoord))return false;
        int subChunkIndex = DimensionYCoordToSubChunkYIndex(chunkLocalCoord.y);
        if(subChunks[subChunkIndex] == null)return false;
        bool ok = subChunks[subChunkIndex].TryBreakBlockAt(SubChunk.BlockCoordToSubChunkLocalCoord(chunkLocalCoord));
        if(ok && !SilentMode)
        {
            MarkModified();
            // NewStateId = 0 (air): consumers (block entities) rely on 0 meaning "broken".
            EventBus.Instance.Publish(new BlockChangedEvent(this, chunkLocalCoord, 0){FromInteraction = fromInteraction});
        }
        return ok;
    }

    // Player edit: the chunk content now differs from disk and needs a save.
    private void MarkModified()
    {
        IsModified = true;
        IsSavedToDisk = false;
    }

    // Block entity data changed outside TrySetBlockAt (no block edit happened):
    // the chunk content still differs from disk, so expose the same dirty mark.
    public void MarkModifiedByBlockEntity() => MarkModified();

    // Block entities bound to this chunk, keyed by chunk-local coord. Kept in
    // memory across unload (chunks are pooled, not destroyed), so re-enabled
    // chunks restore their BEs without a disk round-trip.
    public Dictionary<Vector3Int, BlockEntity> BlockEntities { get; } = new();

    // BE save data parsed by the worker during load; consumed (and cleared) by
    // BlockEntityManager on ChunkLoadedEvent. Null when not loaded from disk.
    public List<BlockEntitySaveData> PendingBlockEntities { get; set; } = null;

    // Main thread only: snapshots every BE's module data (BE data is
    // main-thread-owned; the worker only assembles these strings into JSON).
    public List<BlockEntitySaveData> BuildBlockEntitySaveSnapshot()
    {
        if(BlockEntities.Count == 0)return null;
        var list = new List<BlockEntitySaveData>(BlockEntities.Count);
        foreach(var kv in BlockEntities)
        {
            var be = kv.Value;
            if(be.Removed)continue;
            list.Add(new BlockEntitySaveData
            {
                x = kv.Key.x,
                y = kv.Key.y,
                z = kv.Key.z,
                type = be.Definition.FullName,
                modules = be.BuildModuleSaveData()
            });
        }
        return list.Count == 0 ? null : list;
    }

    // Set by the save manager once the current content was written to disk.
    public void MarkSavedToDisk() => IsSavedToDisk = true;

    // Restored from a save file: content matches disk, no rewrite needed unless
    // the player edits it again.
    public void MarkLoadedFromDisk()
    {
        IsModified = true;
        IsSavedToDisk = true;
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

    // Creates the subchunk if missing (used when loading a save file).
    public SubChunk GetOrCreateSubChunk(int subChunkIndexInChunk)
    {
        int idx = subChunkIndexInChunk - MinSubChunkIndex;
        if (idx < 0 || idx >= subChunks.Length) return null;
        if (subChunks[idx] == null) subChunks[idx] = CreateNewSubChunk(idx);
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