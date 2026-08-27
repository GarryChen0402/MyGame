using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly Queue<Chunk> rebuildQueue = new();
    private const float MaxRebuildChunkCountPerFrameMs = 2f;

    private void RefreshChunkRenderers()
    {
        if(CurrentRenderDimension == null)return;
        WorldManager.Instance.LoadChunksInDimension(CurrentRenderDimension, 
            Dimension.WorldPosToChunkCoord(playerTransform.position) , 2);

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
        rebuildQueue.Enqueue(chunk);
    }

    private void ProcessRebuildChunkQueue()
    {
        int budget = 2;
        // float deadLine = Time.realtimeSinceStartup + MaxRebuildChunkCountPerFrameMs / 1000f;
        while(rebuildQueue.Count > 0 && budget-- > 0)
        {
            var chunk = rebuildQueue.Dequeue();
            if(!chunkRenderers.ContainsKey(chunk.ChunkCoord))continue;
            chunk.RebulidCombinedRenderMesh();
            if(chunkRenderers.TryGetValue(chunk.ChunkCoord, out var renderer))
                renderer.RebuildCombinedRenderMesh();
        }
    }
}