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

    // Returns the global block state id (0 = air); see ResourceSystem.BlockStates.
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

    // Replace-style write: overwrites whatever occupies the cell (random-tick
    // conversions like grass spreading onto dirt). Same event + dirty-mark
    // behavior as a normal edit; NOT the break-then-place sequence, so no drop
    // path and no double BlockChangedEvent ever fires.
    public bool ForceSetBlockAt(Vector3Int chunkLocalCoord, ushort stateId)
    {
        if(!IsCorrectChunkLocalCoord(chunkLocalCoord))return false;
        int subChunkIndex = DimensionYCoordToSubChunkYIndex(chunkLocalCoord.y);
        if(subChunks[subChunkIndex] == null)subChunks[subChunkIndex] = CreateNewSubChunk(subChunkIndex);
        bool ok = subChunks[subChunkIndex].SetBlockAt(SubChunk.BlockCoordToSubChunkLocalCoord(chunkLocalCoord), stateId);
        if(ok && !SilentMode)
        {
            MarkModified();
            EventBus.Instance.Publish(new BlockChangedEvent(this, chunkLocalCoord, stateId));
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

    
    // BE save data parsed by the worker during load; consumed (and cleared) by
    // BlockEntityManager on ChunkLoadedEvent. Null when not loaded from disk.
    public List<BlockEntitySaveData> PendingBlockEntities { get; set; } = null;

    // Block entities hosted by this chunk, keyed by chunk-local coords. The key
    // is Vector3Int on purpose: two BEs can share an x/z column (stacked), so a
    // Vector2Int key would overwrite one of them.
    public readonly Dictionary<Vector3Int, BlockEntity> BlockEntities = new();

    // Stores a freshly created BE (see BlockEntityDefinition.CreateNewBlockEntity)
    // at its chunk-local coord and hands it to the BlockEntityManager so it gets
    // ticked. A stale BE at the same spot (replaced block) is unregistered first.
    public void RegisterBlockEntity(BlockEntity be, Vector3Int chunkLocalCoord)
    {
        if(be == null || !IsCorrectChunkLocalCoord(chunkLocalCoord))return;
        if(BlockEntities.TryGetValue(chunkLocalCoord, out var previous))
            BlockEntityManager.Instance.Unregister(previous);
        be.Position = ChunkLocalCoordToDimensionCoord(ChunkCoord, chunkLocalCoord);   // BE stores dimension coords
        be.OwnerChunk = this;   // MarkDirty now reaches this chunk's save flag
        BlockEntities[chunkLocalCoord] = be;
        BlockEntityManager.Instance.Register(be);
    }

    // Drops the BE at the given coord (block broken or replaced by a non-BE
    // block). No-op when the spot hosts no BE.
    public void RemoveBlockEntity(Vector3Int chunkLocalCoord)
    {
        if(!BlockEntities.Remove(chunkLocalCoord, out var be))return;
        BlockEntityManager.Instance.Unregister(be);
    }

    // Chunk unloaded / disabled: every BE stops ticking and is dropped with the
    // chunk. Their serialized state is cached in PendingBlockEntities first so a
    // later re-enable (and the unload save, which runs after the unload event)
    // can restore them; ChunkLoaded consumers clear the cache once restored.
    public void UnregisterAllBlockEntities()
    {
        if(BlockEntities.Count > 0)
        {
            PendingBlockEntities = BuildBlockEntitySaveData();
            foreach(var be in BlockEntities.Values)
                BlockEntityManager.Instance.Unregister(be);
            BlockEntities.Clear();
        }
    }

    // Item drops hosted by this chunk. Data ownership lives here (design doc
    // §3.1) - the manager only references living drops; unload destroys them
    // (v1) and a future v2 snapshot restores them like PendingBlockEntities.
    public readonly List<ItemEntity> ItemEntities = new();

    // Stores a freshly spawned drop in this chunk and hands it to the manager
    // so it gets updated each frame.
    public void RegisterItemEntity(ItemEntity entity)
    {
        if(entity == null)return;
        entity.OwnerChunk = this;
        ItemEntities.Add(entity);
        ItemEntityManager.Instance.Register(entity);
    }

    // Removes the entity from this chunk's list (cross-chunk migration and
    // despawn both route through here) and drops the manager reference.
    public void RemoveItemEntity(ItemEntity entity)
    {
        if(entity == null || !ItemEntities.Remove(entity))return;
        entity.OwnerChunk = null;
        ItemEntityManager.Instance.Unregister(entity);
    }

    // Chunk unloaded/disabled: every drop is destroyed with the chunk (v1 does
    // not persist drops - design doc §8).
    public void UnregisterAllItemEntities()
    {
        if(ItemEntities.Count == 0)return;
        foreach(var entity in new List<ItemEntity>(ItemEntities))
            ItemEntityManager.Instance.DespawnItemEntity(entity);
    }

    // Main thread only: serializes every live BE into the save list. Null when
    // the chunk hosts none; pass the result to ChunkSerializer.Serialize, which
    // runs on a worker (BE state is main-thread owned, so snapshot it here).
    public List<BlockEntitySaveData> BuildBlockEntitySaveData()
    {
        if(BlockEntities.Count == 0)return null;
        var list = new List<BlockEntitySaveData>(BlockEntities.Count);
        foreach(var kv in BlockEntities)
            list.Add(ChunkSerializer.SerializeBlockEntity(kv.Value, kv.Key));
        return list;
    }

    // Inverse of DimensionCoordToChunkLocalCoord.
    public static Vector3Int ChunkLocalCoordToDimensionCoord(Vector2Int chunkCoord, Vector3Int chunkLocalCoord)
        => new(
            chunkCoord.x * SubChunk.SubChunkBlockSize + chunkLocalCoord.x,
            chunkLocalCoord.y,
            chunkCoord.y * SubChunk.SubChunkBlockSize + chunkLocalCoord.z
        );


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