

using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class Dimension
{
    public DimensionDefinition DimensionDefinitionInfo {get; private set;}

    public DimensionGenerator Generator {get; private set;} = null;
    public Dimension(DimensionDefinition def)
    {
        DimensionDefinitionInfo = def;
        if(!ResourceSystem.Instance.DimensionGenerator.TryGetResourceWithFullName(def.DimensionGeneratorName, out var generator))return;
        Generator = generator.GetNewGenerator();
    }

    private readonly Dictionary<Vector2Int, Chunk> EnableChunks = new();
    private readonly Dictionary<Vector2Int, Chunk> DisableChunks = new();

    // Chunks whose generation is running on a worker thread: present but not yet
    // registered in EnableChunks, so queries treat them as not loaded.
    private readonly Dictionary<Vector2Int, Chunk> GeneratingChunks = new();

    public virtual void FillNewChunk(Chunk chunk)
    {
        //DimensionGenerator, use the Dimension Definition to generate the new chunk of the dimension
        //TODO
        if(Generator == null)return;
        Generator.FillNewChunk(chunk, chunk.ChunkCoord, DimensionDefinitionInfo);
    }

    public bool IsChunkEnabled(Vector2Int ChunkCoord) => EnableChunks.ContainsKey(ChunkCoord);
    public bool IsChunkDisabled(Vector2Int ChunkCoord) => DisableChunks.ContainsKey(ChunkCoord);
    public bool IsChunkGenerating(Vector2Int ChunkCoord) => GeneratingChunks.ContainsKey(ChunkCoord);

    public bool TryGetChunk(Vector2Int chunkCoord, out Chunk chunk)
        => EnableChunks.TryGetValue(chunkCoord, out chunk);

    public void LoadChunk(Vector2Int ChunkCoord)
    {
        if(IsChunkEnabled(ChunkCoord))return;
        Chunk chunk;
        if (IsChunkDisabled(ChunkCoord))
        {
            chunk = DisableChunks[ChunkCoord];
            DisableChunks.Remove(ChunkCoord);
            EnableChunks[ChunkCoord] = chunk;
        }
        else
        {
            if(IsChunkGenerating(ChunkCoord))return;   // already queued / running
            chunk = new Chunk(ChunkCoord, DimensionDefinitionInfo.MinSubChunkIndex, DimensionDefinitionInfo.MaxSubChunkIndex);
            chunk.SilentMode = true;   // suppress block events during bulk fill
            GeneratingChunks[ChunkCoord] = chunk;
            // Async: the worker fills the chunk from the save file when one
            // exists, otherwise generates it; ChunkLoaded fires on the main
            // thread once WorldManager registers it (see ProcessChunkGeneration).
            if(WorldSaveManager.Instance.HasChunkSave(DimensionDefinitionInfo.FullName, ChunkCoord))
                WorldManager.Instance.SubmitChunkLoad(this, chunk);
            else
                WorldManager.Instance.SubmitChunkGeneration(this, chunk);
            return;
        }
        EventBus.Instance.Publish(new ChunkLoadedEvent(chunk));
    }

    public void UnloadChunk(Vector2Int ChunkCoord)
    {
        if(IsChunkDisabled(ChunkCoord))return;
        if (IsChunkEnabled(ChunkCoord))
        {
            Chunk target = EnableChunks[ChunkCoord];
            EnableChunks.Remove(ChunkCoord);
            DisableChunks[ChunkCoord] = target;
            EventBus.Instance.Publish(new ChunkUnloadedEvent(ChunkCoord));
            // Player-modified chunks must persist: queue the save now so the
            // data is on disk even if the app quits without an autosave tick.
            if(target.IsModified && !target.IsSavedToDisk)
                WorldSaveManager.Instance.EnqueueChunkSave(this, target);
            return;
        }
    }

    public Chunk GetOrCreateChunk(Vector2Int ChunkCoord)
    {
        if(IsChunkEnabled(ChunkCoord))return EnableChunks[ChunkCoord];
        if(IsChunkDisabled(ChunkCoord))return DisableChunks[ChunkCoord];
        if(IsChunkGenerating(ChunkCoord))
        {
            // The worker fills the chunk, but registration happens on the main
            // thread, so the wait must keep pumping the completion queue or it
            // deadlocks (the queue is drained in WorldManager.ProcessChunkGeneration).
            WorldManager.Instance.WaitForChunkGenerated(this, ChunkCoord);
            if(IsChunkEnabled(ChunkCoord))return EnableChunks[ChunkCoord];
            // Generation failed: fall through to a fresh synchronous fill.
        }
        Chunk chunk = new(ChunkCoord, DimensionDefinitionInfo.MinSubChunkIndex, DimensionDefinitionInfo.MaxSubChunkIndex);
        chunk.SilentMode = true;   // bulk generation: one ChunkLoadedEvent after, not 24k block events
        // Synchronous load path (player's own chunk): restore from the save
        // file when one exists, otherwise generate in place.
        if(!WorldSaveManager.Instance.TryLoadChunkFromDisk(this, chunk))
            FillNewChunk(chunk);
        chunk.SilentMode = false;
        EnableChunks[ChunkCoord] = chunk;
        return chunk;
    }

    // Main thread only (from WorldManager.ProcessChunkGeneration): moves a
    // finished worker chunk into the enabled set and announces it.
    public void RegisterGeneratedChunk(Chunk chunk)
    {
        chunk.SilentMode = false;
        EnableChunks[chunk.ChunkCoord] = chunk;
        EventBus.Instance.Publish(new ChunkLoadedEvent(chunk));
    }

    // Main thread only: forgets a chunk whose generation finished (success or
    // failure) so LoadChunk can re-enqueue it and GetOrCreateChunk stops waiting.
    public void MarkGenerationDone(Vector2Int ChunkCoord) => GeneratingChunks.Remove(ChunkCoord);

    public ushort GetBlockAt(Vector3Int dimensionCoord)
    {
        var chunkCoord = DimensionCoordToChunkCoord(dimensionCoord);
        if(IsChunkDisabled(chunkCoord))return 0;
        if(IsChunkEnabled(chunkCoord))return EnableChunks[chunkCoord].GetBlockAt(Chunk.DimensionCoordToChunkLocalCoord(dimensionCoord));
        return 0;
    }

    public bool TrySetBlockAt(Vector3Int dimensionCoord, ushort blockId, bool fromInteraction = false)
    {
        var chunk = GetOrCreateChunk(DimensionCoordToChunkCoord(dimensionCoord));
        return chunk.TrySetBlockAt(Chunk.DimensionCoordToChunkLocalCoord(dimensionCoord), blockId, fromInteraction);
    }

    public bool TryBreakBlockAt(Vector3Int dimensionCoord, bool fromInteraction = false)
    {
        var chunk = GetOrCreateChunk(DimensionCoordToChunkCoord(dimensionCoord));
        return chunk.TryBreakBlockAt(Chunk.DimensionCoordToChunkLocalCoord(dimensionCoord), fromInteraction);
    }

    public static Vector3Int WorldPosToDimensionCoord(Vector3 worldPos)
        => new (
            Mathf.FloorToInt(worldPos.x),
            Mathf.FloorToInt(worldPos.y),
            Mathf.FloorToInt(worldPos.z)
        );

    public static Vector2Int DimensionCoordToChunkCoord(Vector3Int dimensionCoord)
        => new(
            Mathf.FloorToInt(dimensionCoord.x * 1.0f / SubChunk.SubChunkBlockSize), 
            Mathf.FloorToInt(dimensionCoord.z * 1.0f / SubChunk.SubChunkBlockSize) 
        );
    
    public static Vector2Int WorldPosToChunkCoord(Vector3 worldPos)
        => new(
            Mathf.FloorToInt(worldPos.x * 1.0f / SubChunk.SubChunkBlockSize), 
            Mathf.FloorToInt(worldPos.z * 1.0f / SubChunk.SubChunkBlockSize) 
        );

    public IEnumerable<Chunk> GetEnableChunks() => EnableChunks.Values;
    public IEnumerable<Chunk> GetDisableChunks() => DisableChunks.Values;
}