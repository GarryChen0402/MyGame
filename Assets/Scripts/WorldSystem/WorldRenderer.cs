using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

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
        // View forwards the player position; chunk load/unload is the Controller's job.
        WorldManager.Instance.OnPlayerMoved(playerTransform.position);
        ProcessRebuildChunkQueue();
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
    }

    private readonly Dictionary<Vector2Int, ChunkRenderer> chunkRenderers = new();
    private readonly List<Chunk> rebuildQueue = new();   // re-sorted by player distance on each dispatch
    private readonly List<ChunkMeshBuildTask> inflight = new();
    private readonly List<ChunkMeshBuildTask> ready = new();
    private const float MaxRebuildChunkCountPerFrameMs = 2f;

    private readonly HashSet<Chunk> importantChunks = new();
    public void MarkChunkIntoRebuildQueue(Chunk chunk, bool important = false)
    {
        if(!rebuildQueue.Contains(chunk))rebuildQueue.Add(chunk);
        if(important)importantChunks.Add(chunk);
    }

    public const int MaxConcurrentBuilds = 4;
    private void ProcessRebuildChunkQueue()
    {
        if(CurrentRenderDimension == null)return;
        // Dispatch nearest chunks first. The list is small (<= (2r+1)^2) and the player
        // keeps moving, so re-sorting every frame is cheap and stays correct.
        Vector2Int playerChunkCoord = Dimension.WorldPosToChunkCoord(playerTransform.position);
        rebuildQueue.Sort((a, b) =>
        {
            bool ia = importantChunks.Contains(a), ib = importantChunks.Contains(b);
            if(ia != ib)return ia ? -1 : 1;
            int da = (a.ChunkCoord - playerChunkCoord).sqrMagnitude;
            int db = (b.ChunkCoord - playerChunkCoord).sqrMagnitude;
            return da.CompareTo(db);
        });


        while(rebuildQueue.Count > 0 && inflight.Count < MaxConcurrentBuilds)
        {
            // Skip entries whose task is already in flight or awaiting upload: their
            // result is still to be applied, so keep them queued for a re-snapshot
            // afterwards (a block change or neighbor event can arrive mid-flight).
            int i = 0;
            while(i < rebuildQueue.Count &&
                (inflight.Exists(t => t.chunk == rebuildQueue[i]) || ready.Exists(t => t.chunk == rebuildQueue[i])))
                i++;
            if(i >= rebuildQueue.Count)break;

            var chunk = rebuildQueue[i];
            rebuildQueue.RemoveAt(i);
            if(!chunkRenderers.ContainsKey(chunk.ChunkCoord))continue;
            var task = new ChunkMeshBuildTask(chunk);
            task.SnapShot();
            ThreadPool.QueueUserWorkItem(_ => Compute(task));
            inflight.Add(task);
        }

        for(int i=inflight.Count -1 ; i >= 0; i--)
        {
            if (inflight[i].IsDown)
            {
                ready.Add(inflight[i]);
                inflight.RemoveAt(i);
            }
        }

        for(int i=ready.Count - 1;i >= 0; i--)
        {
            if (importantChunks.Contains(ready[i].chunk))
            {
                var task = ready[i];
                ready.RemoveAt(i);
                importantChunks.Remove(task.chunk);
                if(!chunkRenderers.ContainsKey(task.chunk.ChunkCoord))
                {
                    // Renderer was destroyed (chunk unloaded); a later ChunkLoaded re-enqueues.
                    continue;
                }
                chunkRenderers[task.chunk.ChunkCoord].ApplyMeshData(task);
            }
        }

        int budget = 2;
        while(ready.Count > 0 && budget-- > 0)
        {
            var task = ready[0];
            ready.RemoveAt(0);
            if(!chunkRenderers.ContainsKey(task.chunk.ChunkCoord))
            {
                // Renderer was destroyed (chunk unloaded); a later ChunkLoaded re-enqueues.
                continue;
            }
            chunkRenderers[task.chunk.ChunkCoord].ApplyMeshData(task);
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
        var blockDefs = ResourceSystem.Instance.BlockDefinitions;
        var models = ResourceSystem.Instance.CustomModels;
        var textures = ResourceSystem.Instance.Textures;

        for (int s = 0; s < task.SubChunkBlockData.Length; s++)
        {
            ushort[] data = task.SubChunkBlockData[s];
            if (data == null) continue;
            float originY = task.SubOriginY[s];

            for (int y = 0; y < SubChunk.SubChunkBlockSize; y++)
                for (int x = 0; x < SubChunk.SubChunkBlockSize; x++)
                    for (int z = 0; z < SubChunk.SubChunkBlockSize; z++)
                    {
                        ushort blockId = data[x * 256 + y * 16 + z];
                        if (blockId == 0) continue;
                        if (!blockDefs.TryGetResourceWithNumberId(blockId, out var def) || def == null) continue;
                        if (!models.TryGetResourceWithFullName(def.ModelId, out var model) || model == null) continue;

                        Dictionary<string, bool> mask = new();
                        Dictionary<string, Rect> faceRects = new();
                        foreach (var kv in model.GetFaceDirections())
                        {
                            Vector3Int dir = kv.Value;
                            ushort neighborId = QueryNeighbor(task, s, x + dir.x, y + dir.y, z + dir.z);
                            mask[kv.Key] = neighborId != 0
                                && blockDefs.TryGetResourceWithNumberId(neighborId, out var neighborDef)
                                && neighborDef != null && neighborDef.IsOpaque;

                            if (textures.TryGetResourceWithFullName(def.TextureIds[kv.Key], out var rect))
                                faceRects[kv.Key] = rect.AtlasUVRect;
                        }
                        model.ExtendModelMesh(
                            new Vector3(x, y + originY, z),
                            task.Vertices, task.Uvs, task.Colors, task.Normals, task.Triangles, mask, faceRects);
                    }
        }
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
