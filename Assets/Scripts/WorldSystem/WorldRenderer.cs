using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

// Rebuild scheduling priority, mirroring Sodium's ChunkUpdateType:
// Important (player interaction) is rebuilt synchronously on the main thread so
// the edit renders within the same frame; Initial covers freshly loaded chunks;
// Normal covers neighbor-driven changes. Initial/Normal are built on the thread
// pool and rate-limited at upload time.
public enum ChunkRebuildType { Normal = 0, Initial = 1, Important = 2 }

public class WorldRenderer : MonoBehaviour
{
    private static WorldRenderer instance = null;
    public static WorldRenderer Instance => instance;

    [SerializeField]
    private Transform playerTransform;

    public Dimension CurrentRenderDimension {get; private set;} = null;

    private void Awake()
    {
        if(instance == null)instance = this;
        else Destroy(gameObject);

        EventBus.Instance.Subscribe<ChunkLoadedEvent>(OnChunkLoaded);
        EventBus.Instance.Subscribe<ChunkUnloadedEvent>(OnChunkUnloaded);
    }

    private void OnDestroy()
    {
        EventBus.Instance.Unsubscribe<ChunkLoadedEvent>(OnChunkLoaded);
        EventBus.Instance.Unsubscribe<ChunkUnloadedEvent>(OnChunkUnloaded);
    }

    private void Update()
    {
        if(playerTransform == null)return;
        // View forwards the player position; chunk load/unload is the Controller's
        // job. World logic ticking lives in WorldManager.Tick (GameLoopDriver).
        WorldManager.Instance.OnPlayerMoved(playerTransform.position);
        ProcessRebuildChunkQueue();
    }

    // Makes the chunk-loading center follow a restored player position (the
    // scene transform keeps its authored position otherwise).
    public void SetPlayerPosition(Vector3 worldPos)
    {
        if(playerTransform != null) playerTransform.position = worldPos;
    }

    public void SetRenderDimension(ushort dimId)
    {
        if(!WorldManager.Instance.TryGetDimension(dimId, out var dim))return;
        CurrentRenderDimension = dim;
        // Old-dimension renderers are no longer valid; the new dimension's chunks
        // (re)load through events once ForceLoadAround generates them.
        foreach(var renderer in chunkRenderers.Values)Destroy(renderer.gameObject);
        chunkRenderers.Clear();
        if(playerTransform != null)
            WorldManager.Instance.ForceLoadAround(playerTransform.position);
        // Chunks enabled before the dimension was set never fired ChunkLoaded
        // (the handler was still ignoring events), so sync them now.
        SyncExistingRenderers();
    }

    // View: renderer lifecycle is driven by ChunkLoaded / ChunkUnloaded events.
    private void OnChunkLoaded(ChunkLoadedEvent evt)
    {
        if(CurrentRenderDimension == null)return;
        if(!CurrentRenderDimension.IsChunkEnabled(evt.Chunk.ChunkCoord))return;   // other dimension
        if(chunkRenderers.ContainsKey(evt.Chunk.ChunkCoord))return;
        CreateRenderer(evt.Chunk);
    }

    // Chunks already enabled (e.g. preloaded before the render dimension was set,
    // or re-enabled from the disabled pool) get a renderer here instead of via event.
    private void SyncExistingRenderers()
    {
        if(CurrentRenderDimension == null)return;
        foreach(var chunk in CurrentRenderDimension.GetEnableChunks())
            if(!chunkRenderers.ContainsKey(chunk.ChunkCoord))
                CreateRenderer(chunk);
    }

    private void CreateRenderer(Chunk chunk)
    {
        var go = new GameObject($"Chunk Coord : {chunk.ChunkCoord.x} : {chunk.ChunkCoord.y}");
        go.transform.SetParent(gameObject.transform);
        go.transform.localPosition = new Vector3(
            chunk.ChunkCoord.x * SubChunk.SubChunkBlockSize,
            0,
            chunk.ChunkCoord.y * SubChunk.SubChunkBlockSize
        );
        var chunkRenderer = go.AddComponent<ChunkRenderer>();
        chunkRenderer.SetChunk(chunk);
        chunkRenderers[chunk.ChunkCoord] = chunkRenderer;
    }

    private void OnChunkUnloaded(ChunkUnloadedEvent evt)
    {
        if(chunkRenderers.Remove(evt.ChunkCoord, out var renderer))
            Destroy(renderer.gameObject);
        // Drop any pending rebuild entry for the unloaded chunk (the renderer is
        // gone, so its dirty subchunk set can never be dispatched).
        Chunk removed = null;
        foreach(var key in rebuildEntries.Keys)
            if(key.ChunkCoord == evt.ChunkCoord) { removed = key; break; }
        if(removed != null)rebuildEntries.Remove(removed);
    }

    private readonly Dictionary<Vector2Int, ChunkRenderer> chunkRenderers = new();
    private readonly Dictionary<Chunk, RebuildEntry> rebuildEntries = new();
    private readonly List<RebuildEntry> dispatchCandidates = new();   // reused sort buffer
    private readonly List<ChunkMeshBuildTask> inflight = new();
    private readonly List<ChunkMeshBuildTask> ready = new();

    private class RebuildEntry
    {
        public Chunk Chunk;
        public ChunkRebuildType Type;
        // Subchunk indices (relative to Chunk.MinSubChunkIndex) to recompute.
        public readonly HashSet<int> DirtySubs = new();
    }

    // Full rebuild of every subchunk (fresh chunk, neighbor loaded/unloaded).
    public void MarkChunkRebuildFull(Chunk chunk, ChunkRebuildType type)
        => MarkChunkRebuild(chunk, null, type);

    // Partial rebuild of the given subchunk indices; important marks a player edit,
    // which upgrades the pending task to blocking (same-frame) priority.
    public void MarkChunkRebuildSubs(Chunk chunk, IEnumerable<int> subIdxs, bool important)
        => MarkChunkRebuild(chunk, subIdxs, important ? ChunkRebuildType.Important : ChunkRebuildType.Normal);

    private void MarkChunkRebuild(Chunk chunk, IEnumerable<int> subIdxs, ChunkRebuildType type)
    {
        if(!rebuildEntries.TryGetValue(chunk, out var entry))
        {
            entry = new RebuildEntry { Chunk = chunk, Type = type };
            rebuildEntries[chunk] = entry;
        }
        if(type > entry.Type)entry.Type = type;   // upgrade, never downgrade
        if(subIdxs == null)
        {
            for(int i = 0; i <= chunk.MaxSubChunkIndex - chunk.MinSubChunkIndex; i++)
                entry.DirtySubs.Add(i);
        }
        else
        {
            foreach(int i in subIdxs)entry.DirtySubs.Add(i);
        }
    }

    public const int MaxConcurrentBuilds = 4;

    private void ProcessRebuildChunkQueue()
    {
        if(CurrentRenderDimension == null)return;
        // Move completed worker tasks to the upload list.
        for(int i = inflight.Count - 1; i >= 0; i--)
            if(inflight[i].IsDown) { ready.Add(inflight[i]); inflight.RemoveAt(i); }

        ApplyReadyTasks();
        DispatchRebuildTasks();
    }

    // Uploads completed worker tasks on a small per-frame budget (uploads touch
    // the GPU, so they are rate-limited). Important tasks never reach this list:
    // they are built and applied synchronously in DispatchRebuildTasks.
    private void ApplyReadyTasks()
    {
        int initialBudget = 2, normalBudget = 2;
        for(int i = 0; i < ready.Count; i++)
        {
            var task = ready[i];
            if(task.Type == ChunkRebuildType.Initial && initialBudget > 0)initialBudget--;
            else if(task.Type == ChunkRebuildType.Normal && normalBudget > 0)normalBudget--;
            else continue;
            ready.RemoveAt(i);
            i--;
            ApplyTask(task);
        }
    }

    private void ApplyTask(ChunkMeshBuildTask task)
    {
        if(!chunkRenderers.TryGetValue(task.chunk.ChunkCoord, out var renderer))return;
        renderer.ApplyMeshData(task);
        // A rebuild marked while this task was in flight can now snapshot the
        // current block data; dispatch it right away instead of waiting for the
        // next frame's queue pass, so edits during a rebuild render same-frame.
        DispatchPendingEntry(task.chunk);
    }

    // The chunk just applied a new mesh, so a pending rebuild entry for it can
    // finally snapshot the latest block data. Important entries build and apply
    // synchronously here (same frame); Initial/Normal go to the thread pool.
    private void DispatchPendingEntry(Chunk chunk)
    {
        if(inflight.Count >= MaxConcurrentBuilds)return;
        if(!rebuildEntries.Remove(chunk, out var entry))return;
        DispatchEntry(chunk, entry);
    }

    private void DispatchRebuildTasks()
    {
        Vector2Int playerChunkCoord = Dimension.WorldPosToChunkCoord(playerTransform.position);
        // Candidates in dispatch priority order: Important, Initial, then Normal,
        // nearest chunks first within a type. Chunks with a task in flight or ready
        // are skipped here; the entry stays queued and DispatchPendingEntry
        // re-dispatches it (fresh snapshot) the moment the task applies, so changes
        // arriving mid-flight render without waiting for the next frame.
        dispatchCandidates.Clear();
        foreach(var entry in rebuildEntries.Values)
        {
            if(inflight.Exists(t => t.chunk == entry.Chunk) || ready.Exists(t => t.chunk == entry.Chunk))continue;
            if(!chunkRenderers.ContainsKey(entry.Chunk.ChunkCoord))continue;
            dispatchCandidates.Add(entry);
        }
        dispatchCandidates.Sort((a, b) =>
        {
            if(a.Type != b.Type)return b.Type.CompareTo(a.Type);
            int da = (a.Chunk.ChunkCoord - playerChunkCoord).sqrMagnitude;
            int db = (b.Chunk.ChunkCoord - playerChunkCoord).sqrMagnitude;
            return da.CompareTo(db);
        });

        foreach(var entry in dispatchCandidates)
        {
            if(inflight.Count >= MaxConcurrentBuilds)break;
            rebuildEntries.Remove(entry.Chunk);
            DispatchEntry(entry.Chunk, entry);
        }
    }

    // Builds one rebuild entry and, for Important, applies it immediately. The
    // entry must already be removed from rebuildEntries so re-entrant dispatches
    // (DispatchPendingEntry) can never run it twice.
    private void DispatchEntry(Chunk chunk, RebuildEntry entry)
    {
        var renderer = chunkRenderers[chunk.ChunkCoord];
        // Partial rebuilds copy untouched subchunks from the last applied result;
        // without a base there is nothing to copy from, so rebuild everything.
        var task = new ChunkMeshBuildTask(chunk)
        {
            Type = entry.Type,
            PreviousTask = renderer.AppliedTask
        };
        task.SnapShot(renderer.AppliedTask == null ? null : entry.DirtySubs);

        if(entry.Type == ChunkRebuildType.Important)
        {
            // Player edits build synchronously on the main thread (only the
            // touched subchunks, ~1-3ms) so the change renders within the same
            // frame - no thread-pool contention with chunk generation, no
            // timeout fallback. A full rebuild (no applied base yet) is rare
            // and still correct; it only costs one longer frame.
            try
            {
                BuildMeshData(task);
            }
            catch(System.Exception e)
            {
                Debug.LogError($"Chunk mesh build failed ({task.chunk.ChunkCoord}): {e}");
            }
            ApplyTask(task);
        }
        else
        {
            ThreadPool.QueueUserWorkItem(_ => Compute(task));
            inflight.Add(task);
        }
    }

    // Worker thread: reads only the task snapshot and read-only resource data.
    private static void Compute(object state)
    {
        var task = (ChunkMeshBuildTask)state;
        try
        {
            BuildMeshData(task);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Chunk mesh build failed ({task.chunk.ChunkCoord}): {e}");
        }
        finally
        {
            task.IsDown = true;   // volatile write: last operation, makes all list writes visible
        }
    }

    private static void BuildMeshData(ChunkMeshBuildTask task)
    {
        var models = ResourceSystem.Instance.CustomModels;
        var textures = ResourceSystem.Instance.Textures;
        ChunkMeshBuildTask prev = task.PreviousTask;

        for (int s = 0; s < task.RebuildFlags.Length; s++)
        {
            int startV = task.Vertices.Count;
            int startT = task.Triangles.Count;
            if (task.RebuildFlags[s])
            {
                ushort[] data = task.SubChunkBlockData[s];
                if (data != null)
                {
                    float originY = task.SubOriginY[s];

                    for (int y = 0; y < SubChunk.SubChunkBlockSize; y++)
                        for (int x = 0; x < SubChunk.SubChunkBlockSize; x++)
                            for (int z = 0; z < SubChunk.SubChunkBlockSize; z++)
                            {
                                ushort stateId = data[x * 256 + y * 16 + z];
                                if (stateId == 0) continue;
                                var state = ResourceSystem.Instance.GetState(stateId);
                                if (state == null) continue;
                                BlockDefinition def = state.Block;
                                if (!models.TryGetResourceWithFullName(state.ModelId, out var model) || model == null) continue;

                                task.Mask.Clear();
                                task.FaceRects.Clear();
                                foreach (var kv in model.GetFaceDirections())
                                {
                                    Vector3Int dir = state.RotationX != 0 || state.RotationY != 0 || state.RotationZ != 0
                                        ? CustomModel.RotateDirection(kv.Value, state.RotationX, state.RotationY, state.RotationZ)
                                        : kv.Value;
                                    ushort neighborId = QueryNeighbor(task, s, x + dir.x, y + dir.y, z + dir.z);
                                    // neighborId is a global state id; resolve it to the block's
                                    // flags via the state registry (block ids only work for
                                    // single-state blocks, which is why the old lookup misfired).
                                    // IsFullCube: non-full shapes (stairs) leave part of a
                                    // neighbor's face visible, so they never hide it.
                                    task.Mask[kv.Key] = neighborId != 0
                                        && ResourceSystem.Instance.GetState(neighborId)?.Block is { IsOpaque: true, IsFullCube: true };

                                    if (def.TextureIds != null && def.TextureIds.TryGetValue(kv.Key, out string texId)
                                        && textures.TryGetResourceWithFullName(texId, out var rect))
                                        task.FaceRects[kv.Key] = rect.AtlasUVRect;
                                }
                                model.ExtendModelMesh(
                                    new Vector3(x, y + originY, z),
                                    task.Vertices, task.Uvs, task.Colors, task.Normals, task.Triangles,
                                    task.Mask, task.FaceRects, state.RotationX, state.RotationY, state.RotationZ);
                            }
                }
            }
            else if (prev != null && prev.SubVertexCount[s] > 0)
            {
                // Untouched subchunk: carry its geometry over from the last applied
                // result verbatim instead of re-running face culling.
                CopyRange(task.Vertices, prev.Vertices, prev.SubStartVertex[s], prev.SubVertexCount[s]);
                CopyRange(task.Uvs, prev.Uvs, prev.SubStartVertex[s], prev.SubVertexCount[s]);
                CopyRange(task.Colors, prev.Colors, prev.SubStartVertex[s], prev.SubVertexCount[s]);
                CopyRange(task.Normals, prev.Normals, prev.SubStartVertex[s], prev.SubVertexCount[s]);
                // Triangle indices are absolute in the previous mesh; rebase them
                // onto this task's copy (the subchunk now starts at startV).
                for (int i = 0; i < prev.SubTriCount[s]; i++)
                    task.Triangles.Add(prev.Triangles[prev.SubStartTri[s] + i] - prev.SubStartVertex[s] + startV);
            }
            task.SubStartVertex[s] = startV;
            task.SubVertexCount[s] = task.Vertices.Count - startV;
            task.SubStartTri[s] = startT;
            task.SubTriCount[s] = task.Triangles.Count - startT;
        }
    }

    private static void CopyRange<T>(List<T> dst, List<T> src, int start, int count)
    {
        for (int i = 0; i < count; i++) dst.Add(src[start + i]);
    }

    // Interior neighbors read the subchunk copy; exterior ones read the prefetched planes.
    private static ushort QueryNeighbor(ChunkMeshBuildTask task, int s, int nx, int ny, int nz)
    {
        int ox = nx < 0 || nx > 15 ? 1 : 0;
        int oy = ny < 0 || ny > 15 ? 1 : 0;
        int oz = nz < 0 || nz > 15 ? 1 : 0;
        if (ox + oy + oz == 0) return task.SubChunkBlockData[s][nx * 256 + ny * 16 + nz];
        if (ox + oy + oz > 1) return 1;   // diagonal neighbor: treat as solid (conservative)
        if (ox == 1) return task.BoundaryPlances[s, nx < 0 ? 0 : 1, nz, ny];
        if (oy == 1) return task.BoundaryPlances[s, ny < 0 ? 2 : 3, nx, nz];
        return task.BoundaryPlances[s, nz < 0 ? 4 : 5, nx, ny];
    }
}
