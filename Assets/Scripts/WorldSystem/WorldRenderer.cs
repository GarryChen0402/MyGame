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

    // [SerializeField]
    // private List<ChunkRenderer> chunkRenderers = new();

    [SerializeField]
    private Transform playerTransform;
    private Vector2Int playerLastChunkCoord = new(int.MaxValue, int.MaxValue);

    public Dimension CurrentRenderDimension {get; private set;} = null;
    private void Awake()
    {
        if(instance == null)instance = this;
        else Destroy(gameObject);
    }
    

    private void Update()
    {
        if(playerTransform == null)return;
        var currentPlayerChunkCoord = Dimension.WorldPosToChunkCoord(playerTransform.position);
        if(currentPlayerChunkCoord != playerLastChunkCoord)
        {
            playerLastChunkCoord = currentPlayerChunkCoord;
            RefreshChunkRenderers();
        }
        // playerLastChunkCoord = currentPlayerChunkCoord;
        // RefreshChunkRenderers();

        ProcessRebuildChunkQueue();
    }

    public void SetRenderDimension(ushort dimId)
    {
        if(!WorldManager.Instance.TryGetDimension(dimId, out var dim))return;
        CurrentRenderDimension = dim;
        RefreshChunkRenderers();
    }
    private readonly Dictionary<Vector2Int, ChunkRenderer> chunkRenderers = new();
    private readonly List<Chunk> rebuildQueue = new();   // re-sorted by player distance on each dispatch
    private readonly List<ChunkMeshBuildTask> inflight = new();
    private readonly List<ChunkMeshBuildTask> ready = new();
    private const float MaxRebuildChunkCountPerFrameMs = 2f;

    private void RefreshChunkRenderers()
    {
        if(CurrentRenderDimension == null)return;
        WorldManager.Instance.LoadChunksInDimension(CurrentRenderDimension, 
            Dimension.WorldPosToChunkCoord(playerTransform.position) , 8);

        foreach(var coord in chunkRenderers.Keys.ToList())
        {
            if (!CurrentRenderDimension.IsChunkEnabled(coord))
            {
                Destroy(chunkRenderers[coord].gameObject);
                chunkRenderers.Remove(coord);
            }
        }

        foreach(var chunk in CurrentRenderDimension.GetEnableChunks())
        {
            if (!chunkRenderers.ContainsKey(chunk.ChunkCoord))
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
        }
    }
    public void MarkChunkIntoRebuildQueue(Chunk chunk)
    {
        if(rebuildQueue.Contains(chunk))return;
        rebuildQueue.Add(chunk);
    }

    public const int MaxConcurrentBuilds = 4;
    private void ProcessRebuildChunkQueue()
    {
        // int budget = 2;
        // // float deadLine = Time.realtimeSinceStartup + MaxRebuildChunkCountPerFrameMs / 1000f;
        // while(rebuildQueue.Count > 0 && budget-- > 0)
        // {
        //     var chunk = rebuildQueue.Dequeue();
        //     if(!chunkRenderers.ContainsKey(chunk.ChunkCoord))continue;
        //     chunk.RebulidCombinedRenderMesh();
        //     if(chunkRenderers.TryGetValue(chunk.ChunkCoord, out var renderer))
        //         renderer.RebuildCombinedRenderMesh();
        // }
        // Dispatch nearest chunks first. The list is small (<= (2r+1)^2) and the player
        // keeps moving, so re-sorting every frame is cheap and stays correct.
        Vector2Int playerChunkCoord = Dimension.WorldPosToChunkCoord(playerTransform.position);
        rebuildQueue.Sort((a, b) =>
        {
            int da = (a.ChunkCoord - playerChunkCoord).sqrMagnitude;
            int db = (b.ChunkCoord - playerChunkCoord).sqrMagnitude;
            return da.CompareTo(db);
        });

        while(rebuildQueue.Count > 0 && inflight.Count < MaxConcurrentBuilds)
        {
            var chunk = rebuildQueue[0];
            rebuildQueue.RemoveAt(0);
            if(!chunkRenderers.ContainsKey(chunk.ChunkCoord))continue;
            // One task per chunk at a time; a re-dirty during flight re-enqueues after apply.
            if(inflight.Exists(t => t.chunk == chunk) || ready.Exists(t => t.chunk == chunk))continue;
            var task = new ChunkMeshBuildTask(chunk);
            task.SnapShot();
            chunk.MarkRenderMeshClean();   // renderer.Update won't re-enqueue while the task is in flight
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

        int budget = 2;
        while(ready.Count > 0 && budget-- > 0)
        {
            var task = ready[0];
            ready.RemoveAt(0);
            if(!chunkRenderers.ContainsKey(task.chunk.ChunkCoord))
            {
                task.chunk.MarkRenderMeshDirty();   // restore pending state for a later re-enable
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