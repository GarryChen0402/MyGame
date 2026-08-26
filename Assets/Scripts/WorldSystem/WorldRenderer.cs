using System;
using System.Collections.Generic;
using UnityEngine;

public class WorldRenderer : MonoBehaviour
{
    private static WorldRenderer instance = null;
    public static WorldRenderer Instance => instance;

    [SerializeField]
    private List<ChunkRenderer> chunkRenderers = new();

    [SerializeField]
    private Transform playerTransform;
    private Vector2Int playerLastChunkCoord = new(int.MaxValue, int.MaxValue);

    public Dimension CurrentRenderDimension {get; private set;} = null;
    private void Awake()
    {
        if(instance == null)instance = this;
        else GameObject.Destroy(gameObject);
    }


    private void Update()
    {
        if(playerTransform == null)return;
        var currentPlayerChunkCoord = Dimension.WorldPosToChunkCoord(playerTransform.position);
        if(currentPlayerChunkCoord == playerLastChunkCoord)return;
        playerLastChunkCoord = currentPlayerChunkCoord;
        RefreshChunkRenderers();

    }

    public void SetRenderDimension(ushort dimId)
    {
        if(!WorldManager.Instance.TryGetDimension(dimId, out var dim))return;
        CurrentRenderDimension = dim;
        RefreshChunkRenderers();
    }

    private void RefreshChunkRenderers()
    {
        if(CurrentRenderDimension == null)return;
        WorldManager.Instance.LoadChunksInDimension(CurrentRenderDimension, 
            Dimension.WorldPosToChunkCoord(playerTransform.position) , 2);

        foreach(var renderer in chunkRenderers)Destroy(renderer.gameObject);
        chunkRenderers.Clear();

        foreach(var chunk in CurrentRenderDimension.GetEnableChunks())
        {
            GameObject go = new GameObject();
            go.name = $"Chunk Coord : {chunk.ChunkCoord.x} : {chunk.ChunkCoord.y}";
            go.transform.SetParent(gameObject.transform);
            go.transform.localPosition = new Vector3(
                chunk.ChunkCoord.x * SubChunk.SubChunkBlockSize,
                0,
                chunk.ChunkCoord.y * SubChunk.SubChunkBlockSize
            );
            var renderer = go.AddComponent<ChunkRenderer>();
            renderer.SetChunk(chunk);
            chunkRenderers.Add(renderer);
        }
    }

}