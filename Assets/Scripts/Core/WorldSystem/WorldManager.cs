
using System.Collections.Generic;
using System.Threading;
using Unity.Profiling;
using UnityEngine;

public class WorldManager
{
    public static WorldManager Instance {get;} = new();

    public readonly Dictionary<ushort, Dimension> Dimensions = new();

    // World seed: all deterministic generation (center points, corner noise,
    // density layers) derives from it, so the same seed yields the same world.
    // The default also backs a slot whose world.json is missing (the
    // enter-world worker falls back to it).
    public const int DefaultSeed = 20260830;
    public int Seed = DefaultSeed;

    // Multi-focus load centers (Part B §4.1): v1 registers only the player
    // focus, replacing the retired lastPlayerChunkCoord single-center state.
    private readonly List<LoadFocus> loadFocuses = new();

    public const int ChunkLoadRange = 8;

    // Async chunk generation: workers fill chunks off the main thread (the density
    // field is pure computation); the main thread polls for completion and
    // registers finished chunks, since events must fire on the main thread.
    private readonly Queue<ChunkGenTask> genQueue = new();
    private readonly List<ChunkGenTask> genInflight = new();
    private const int MaxConcurrentChunkGens = 6;

    // Profiler markers (normal Profiler, no Deep Profile needed); the worker
    // marker records per-chunk-task cost on the thread-pool lane.
    private static readonly ProfilerMarker chunkGenPumpMarker = new ProfilerMarker("World.ChunkGenPump");
    private static readonly ProfilerMarker chunkGenWorkerMarker = new ProfilerMarker("World.ChunkGenWorker");
    private static readonly ProfilerMarker saveTickMarker = new ProfilerMarker("World.SaveTick");
    private static readonly ProfilerMarker entitiesMarker = new ProfilerMarker("World.Entities");
    private static readonly ProfilerMarker loadCenterMarker = new ProfilerMarker("World.LoadCenter");
    private static readonly ProfilerMarker blockEntitiesMarker = new ProfilerMarker("World.BlockEntities");
    private static readonly ProfilerMarker randomTickMarker = new ProfilerMarker("World.RandomTick");
    private static readonly ProfilerMarker itemEntitiesMarker = new ProfilerMarker("World.ItemEntities");
    private static readonly ProfilerMarker commandTickMarker = new ProfilerMarker("World.Commands");

    // Multi-focus registration (decision C). The enter-world sequence registers
    // its PlayerLoadFocus before the initial ring submission so completed
    // workers keep their chunks (the drop rule tests every focus).
    public void RegisterLoadFocus(LoadFocus focus) => loadFocuses.Add(focus);
    public void UnregisterLoadFocus(LoadFocus focus) => loadFocuses.Remove(focus);

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

    // Registers a dimension instance built off the main thread (enter-world
    // w4). The session never overwrites an existing entry with it: re-entry
    // keeps the live instance and its enabled chunks.
    public void RegisterDimension(ushort dimId, Dimension dimension) => Dimensions[dimId] = dimension;

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


    public void LoadChunksInDimension(Dimension dim, Vector2Int centerChunkCoord, int range)
    {
        if(dim==null)return;
        // The chunk the player stands in must exist immediately or the player
        // falls through; everything else generates asynchronously on workers.
        // Run-time only (rule L2): the logic tick owns this path, never the
        // frozen enter-world submission (that one is SubmitInventoriedRing).
        dim.GetOrCreateChunk(centerChunkCoord);
        for(int x = -range; x <= range; x++)
            for(int z = -range; z <= range; z++)
            {
                Vector2Int coord = centerChunkCoord + new Vector2Int(x, z);
                if(coord == centerChunkCoord)continue;
                dim.LoadChunk(coord);
            }
    }

    // Enter-world ring submission (Part B §4.3 step m3): queues the whole ring
    // around the landing chunk asynchronously - no synchronous center chunk,
    // safe because the logic is still frozen (no player tick can fall through
    // an unready chunk; readiness is ChunkLoadTracker's job). coords/isLoad
    // come from the server worker's w3 inventory, so this path never probes
    // the disk; already-enabled coords are a no-op inside LoadChunk (the
    // tracker snapshots them as ready).
    public void SubmitInventoriedRing(Dimension dim, Vector2Int[] coords, bool[] isLoad)
    {
        for(int i = 0; i < coords.Length; i++)dim.LoadChunk(coords[i], isLoad[i]);
    }

    // Chebyshev-ascending ring around center: pure math, worker-safe (the
    // enter-world w3 inventory runs it off the main thread).
    public static Vector2Int[] RingCoords(Vector2Int center, int range)
    {
        var ring = new List<Vector2Int>();
        for(int x = -range; x <= range; x++)
            for(int z = -range; z <= range; z++)
                ring.Add(center + new Vector2Int(x, z));
        ring.Sort((a, b) => Mathf.Max(Mathf.Abs(a.x - center.x), Mathf.Abs(a.y - center.y))
                          .CompareTo(Mathf.Max(Mathf.Abs(b.x - center.x), Mathf.Abs(b.y - center.y))));
        return ring.ToArray();
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
    // generating state (see PumpChunkGeneration) — a plain sleep would deadlock.
    public void WaitForChunkGenerated(Dimension dim, Vector2Int coord)
    {
        while(dim.IsChunkGenerating(coord))
        {
            PumpChunkGeneration();
            Thread.Sleep(1);
        }
    }

    // Pumps the async chunk pipeline: dispatch queued tasks to the thread pool,
    // then register finished chunks (ChunkLoaded must fire on the main thread).
    // Called once per logic tick in the game state and once per render frame
    // while the logic is frozen (enter-world readiness, Part B §4.4) - never
    // both, so the game state's pump cadence is unchanged.
    public void PumpChunkGeneration()
    {
        using (chunkGenPumpMarker.Auto())
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
    }

    private static void ComputeChunkGeneration(object state)
    {
        var task = (ChunkGenTask)state;
        try
        {
            using (chunkGenWorkerMarker.Auto())
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
        // Every focus moved away while the worker ran: drop the result. With no
        // focus at all nothing is loading, so nothing is dropped (matches the
        // retired single-center semantics, Part B §4.2).
        if(IsBeyondAllFocuses(task.Chunk.ChunkCoord, forSweep: false))return;
        task.Dimension.RegisterGeneratedChunk(task.Chunk);
    }

    // One fixed game tick (20Hz GameClock step, design doc 固定Tick时钟与渲染
    // 插值改造-代码设计.md §5): the sole driver of the logic managers, called
    // by GameLoopDriver per caught-up tick. Order matters: chunks finished this
    // tick register first, then the systems that tick over them (autosave,
    // entities, block entities, random ticks, item entities).
    public void Tick(float dt)
    {
        PumpChunkGeneration();
        using (saveTickMarker.Auto()) WorldSaveManager.Instance.Tick(dt);
        using (entitiesMarker.Auto()) EntityManager.Instance.Update(dt);
        using (loadCenterMarker.Auto()) UpdateLoadCenter();   // after the entity batch: the player moved this tick already (rules L1/L2)
        using (blockEntitiesMarker.Auto()) BlockEntityManager.Instance.Tick(dt);
        using (randomTickMarker.Auto()) RandomTickSystem.Instance.Tick(dt);   // MC random ticks: 20Hz block-domain step (design doc 随机刻系统-代码设计 §3)
        using (itemEntitiesMarker.Auto()) ItemEntityManager.Instance.Update();
        // External input last: queued commands settle after the world stepped
        // this tick (D2 - parse + settlement never run on the input frame).
        using (commandTickMarker.Auto()) CommandDispatcher.Instance.Tick();
    }

    // Load-center driver on the game tick (rule L2): every dynamic focus
    // refreshes against the authoritative player position, dirty focuses submit
    // their load ring (run-time only - the enter-world ring was already
    // submitted), then enabled chunks beyond every focus's unload radius are
    // swept on the same dirty tick (the legacy unload cadence).
    private void UpdateLoadCenter()
    {
        // Menu / pre-enter-world defense (trap T1): with no dimension there is
        // nothing to load and no player position to center on - never read the
        // singleton player while the world is absent. No focus = nothing to
        // center on either.
        if(Dimensions.Count == 0 || loadFocuses.Count == 0)return;
        bool anyDirty = false;
        foreach(var focus in loadFocuses)anyDirty |= focus.RefreshFocus();
        if(!anyDirty)return;
        foreach(var dim in Dimensions.Values)
        {
            foreach(var focus in loadFocuses)
                if(focus.Dirty)
                    LoadChunksInDimension(dim, focus.FocusChunk, focus.LoadRadius);
            UnloadChunksOutsideFocuses(dim);
        }
        foreach(var focus in loadFocuses)focus.Dirty = false;
    }

    // Multi-focus unload sweep (Part B §4.2): enabled chunks beyond EVERY
    // focus's unload radius leave the enabled set (collect-then-remove, since
    // UnloadChunk mutates the dictionary). Runs on dirty ticks only.
    private void UnloadChunksOutsideFocuses(Dimension dim)
    {
        List<Vector2Int> unloadPending = new();
        foreach(var chunk in dim.GetEnableChunks())
            if(IsBeyondAllFocuses(chunk.ChunkCoord, forSweep: true))
                unloadPending.Add(chunk.ChunkCoord);
        foreach(Vector2Int coord in unloadPending)dim.UnloadChunk(coord);
    }

    // Is the chunk beyond every focus's radius of the given kind? LoadRadius
    // gates finished-generation drops, UnloadRadius gates the sweep. An empty
    // focus list drops nothing and sweeps nothing.
    private bool IsBeyondAllFocuses(Vector2Int coord, bool forSweep)
    {
        if(loadFocuses.Count == 0)return false;
        foreach(var focus in loadFocuses)
        {
            int radius = forSweep ? focus.UnloadRadius : focus.LoadRadius;
            if(Mathf.Max(Mathf.Abs(coord.x - focus.FocusChunk.x),
                         Mathf.Abs(coord.y - focus.FocusChunk.y)) <= radius)return false;
        }
        return true;
    }
}

// One async chunk generation unit: a worker fills Chunk, the main thread then
// registers it (see WorldManager.PumpChunkGeneration). LoadFromDisk tasks
// restore the chunk from the save file instead of generating it.
public class ChunkGenTask
{
    public Dimension Dimension;
    public Chunk Chunk;
    public bool LoadFromDisk;
    public volatile bool IsDown;
    public bool Failed;
}
