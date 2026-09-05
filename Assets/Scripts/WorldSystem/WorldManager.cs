
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class WorldManager
{
    public static WorldManager Instance {get;} = new();

    public readonly Dictionary<ushort, Dimension> Dimensions = new();

    // World seed: all deterministic generation (center points, corner noise,
    // density layers) derives from it, so the same seed yields the same world.
    public int Seed = 20260830;

    private Vector2Int lastPlayerChunkCoord = new(int.MaxValue, int.MaxValue);
    private const int ChunkLoadRange = 8;

    // Async chunk generation: workers fill chunks off the main thread (the density
    // field is pure computation); the main thread polls for completion and
    // registers finished chunks, since events must fire on the main thread.
    private readonly Queue<ChunkGenTask> genQueue = new();
    private readonly List<ChunkGenTask> genInflight = new();
    private const int MaxConcurrentChunkGens = 6;

    // Controller: the only place that decides which chunks are loaded. Called by the
    // view (WorldRenderer) with the raw player position; detects chunk crossings here.
    public void OnPlayerMoved(Vector3 worldPos)
    {
        Vector2Int coord = Dimension.WorldPosToChunkCoord(worldPos);
        if(coord == lastPlayerChunkCoord)return;
        lastPlayerChunkCoord = coord;
        foreach(var dim in Dimensions.Values)
            LoadChunksInDimension(dim, coord, ChunkLoadRange);
    }

    // Forces a reload around a position even if the player hasn't crossed a chunk
    // boundary (dimension switch / initial setup).
    public void ForceLoadAround(Vector3 worldPos)
    {
        lastPlayerChunkCoord = Dimension.WorldPosToChunkCoord(worldPos);
        foreach(var dim in Dimensions.Values)
            LoadChunksInDimension(dim, lastPlayerChunkCoord, ChunkLoadRange);
    }

    // Controller entry for player block operations. stateId is a global block
    // state id (see ResourceSystem.BlockStates); returns false when the target position
    // is already occupied or the dimension doesn't exist.
    public bool TryPlaceBlock(ushort dimId, Vector3Int dimensionCoord, ushort stateId, bool fromInteraction = false)
    {
        if(!TryGetOrGenerateDimension(dimId, out var dim))return false;
        return dim.TrySetBlockAt(dimensionCoord, stateId, fromInteraction);
    }

    public bool TryBreakBlockAt(ushort dimId, Vector3Int dimensionCoord, bool fromInteraction = false)
    {
        if(!TryGetOrGenerateDimension(dimId, out var dim))return false;
        return dim.TryBreakBlockAt(dimensionCoord, fromInteraction);
    }

    // Read-only lookup of the block entity at a dimension coord (interaction
    // entry: does not generate or load any chunk).
    public bool TryGetBlockEntity(ushort dimId, Vector3Int dimensionCoord, out BlockEntity blockEntity)
    {
        blockEntity = null;
        if(!TryGetDimension(dimId, out var dim))return false;
        return dim.TryGetBlockEntity(dimensionCoord, out blockEntity);
    }

    public bool IsDimensionExist(ushort dimensionId) => Dimensions.ContainsKey(dimensionId);

    public bool TryGetOrGenerateDimension(string dimensionFullName, out Dimension dimension)
    {
        dimension = null;
        if(!ResourceSystem.Instance.DimensionDefinitions.TryGetResourceWithFullName(dimensionFullName, out var def))return false;
        if(!ResourceSystem.Instance.DimensionDefinitions.TryGetNumberId(dimensionFullName, out var id))return false;
        if (IsDimensionExist(id))
        {
            dimension = Dimensions[id];
            return true;
        }
        dimension = new Dimension(def);
        Dimensions[id] = dimension;
        return true;
    }
    public bool TryGetOrGenerateDimension(ushort dimId, out Dimension dimension)
    {
        dimension = null;
        if(!ResourceSystem.Instance.DimensionDefinitions.TryGetStringId(dimId, out var name))return false;
        return TryGetOrGenerateDimension(name, out dimension);
    }

    public bool TryGetDimension(string dimensionName, out Dimension dim)
    {
        dim = null;
        if(!ResourceSystem.Instance.DimensionDefinitions.TryGetNumberId(dimensionName, out var id))return false;
        return TryGetDimension(id, out dim);
    }

    public bool TryGetDimension(ushort dimId, out Dimension dim) => Dimensions.TryGetValue(dimId, out dim);


    public void LoadChunksInDimension(ushort dimId, Vector2Int centerChunkCoord, int range)
    {
        if(!TryGetOrGenerateDimension(dimId, out var dim))return ;
        LoadChunksInDimension(dim, centerChunkCoord, range);
    }

    public void LoadChunksInDimension(Dimension dim, Vector2Int centerChunkCoord, int range)
    {
        if(dim==null)return;
        // The chunk the player stands in must exist immediately or the player
        // falls through; everything else generates asynchronously on workers.
        dim.GetOrCreateChunk(centerChunkCoord);
        for(int x = -range; x <= range; x++)
            for(int z = -range; z <= range; z++)
            {
                Vector2Int coord = centerChunkCoord + new Vector2Int(x, z);
                if(coord == centerChunkCoord)continue;
                dim.LoadChunk(coord);
            }

        Chunk centerChunk = dim.GetOrCreateChunk(centerChunkCoord);
        List<Vector2Int> unloadPendingChunkCoords = new();
        foreach(var chunk in dim.GetEnableChunks())
        {
            Vector2Int delta = chunk.ChunkCoord - centerChunk.ChunkCoord;
            if(Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) > range)
                unloadPendingChunkCoords.Add(chunk.ChunkCoord);
        }

        foreach(var coord in unloadPendingChunkCoords)dim.UnloadChunk(coord);
    }

    // Main thread: enqueue a chunk for async generation. The biome center map is
    // materialized here — before any worker samples it — so worker threads only
    // ever do read-only lookups on it.
    public void SubmitChunkGeneration(Dimension dim, Chunk chunk)
    {
        BiomeCenterMap.GetOrCreate(dim.DimensionDefinitionInfo.FullName, Seed);
        genQueue.Enqueue(new ChunkGenTask { Dimension = dim, Chunk = chunk });
    }

    // Main thread: enqueue a chunk for async load from the save file. Same
    // lifecycle as generation; on failure the worker falls back to generation.
    public void SubmitChunkLoad(Dimension dim, Chunk chunk)
    {
        genQueue.Enqueue(new ChunkGenTask { Dimension = dim, Chunk = chunk, LoadFromDisk = true });
    }

    // Main thread: blocks until the chunk's worker finished and registered it.
    // The wait pumps the completion queue, since draining it is what clears the
    // generating state (see ProcessChunkGeneration) — a plain sleep would deadlock.
    public void WaitForChunkGenerated(Dimension dim, Vector2Int coord)
    {
        while(dim.IsChunkGenerating(coord))
        {
            ProcessChunkGeneration();
            Thread.Sleep(1);
        }
    }

    // Called every frame by the view: dispatch queued tasks to the thread pool,
    // then register finished chunks (ChunkLoaded must fire on the main thread).
    public void ProcessChunkGeneration()
    {
        while(genQueue.Count > 0 && genInflight.Count < MaxConcurrentChunkGens)
        {
            ChunkGenTask task = genQueue.Dequeue();
            genInflight.Add(task);
            ThreadPool.QueueUserWorkItem(ComputeChunkGeneration, task);
        }

        for(int i = genInflight.Count - 1; i >= 0; i--)
        {
            if(genInflight[i].IsDown)
            {
                CompleteChunkGeneration(genInflight[i]);
                genInflight.RemoveAt(i);
            }
        }
    }

    private static void ComputeChunkGeneration(object state)
    {
        var task = (ChunkGenTask)state;
        try
        {
            // Save-backed chunks load from disk; a missing or corrupt file falls
            // back to deterministic generation.
            if(task.LoadFromDisk)
            {
                if(!WorldSaveManager.Instance.TryLoadChunkFromDisk(task.Dimension, task.Chunk))
                    task.Dimension.FillNewChunk(task.Chunk);
            }
            else
            {
                task.Dimension.FillNewChunk(task.Chunk);
            }
        }
        catch(System.Exception e)
        {
            Debug.LogError($"Chunk generation failed ({task.Chunk.ChunkCoord}): {e}");
            task.Failed = true;
        }
        finally
        {
            task.IsDown = true;   // volatile write: last operation, makes chunk data visible
        }
    }

    private void CompleteChunkGeneration(ChunkGenTask task)
    {
        task.Dimension.MarkGenerationDone(task.Chunk.ChunkCoord);
        if(task.Failed)return;   // left unregistered; a later load regenerates it
        // The player moved out of range while the worker ran: drop the result.
        Vector2Int delta = task.Chunk.ChunkCoord - lastPlayerChunkCoord;
        if(Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) > ChunkLoadRange)return;
        task.Dimension.RegisterGeneratedChunk(task.Chunk);
    }

    // Every-frame world logic step (design doc 逻辑tick驱动与渲染解耦重构方案.md):
    // the sole driver of the logic managers, called by GameLoopDriver. Order
    // matters: chunks finished this frame register first, then the systems that
    // tick over them (autosave, block entities, item entities).
    public void Tick(float dt)
    {
        ProcessChunkGeneration();
        WorldSaveManager.Instance.Tick(dt);
        EntityManager.Instance.Update(dt);
        BlockEntityManager.Instance.Tick(dt);
        ItemEntityManager.Instance.Update();
    }
}

// One async chunk generation unit: a worker fills Chunk, the main thread then
// registers it (see WorldManager.ProcessChunkGeneration). LoadFromDisk tasks
// restore the chunk from the save file instead of generating it.
public class ChunkGenTask
{
    public Dimension Dimension;
    public Chunk Chunk;
    public bool LoadFromDisk;
    public volatile bool IsDown;
    public bool Failed;
}
