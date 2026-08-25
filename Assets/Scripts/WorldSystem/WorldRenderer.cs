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

    private Dimension CurrentRenderDimension = null;
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
        if(WorldManager.Instance.TryGetDimension(dimId, out var dim))return;
        CurrentRenderDimension = dim;
        RefreshChunkRenderers();
    }

    private void RefreshChunkRenderers()
    {
        if(CurrentRenderDimension == null)
        {
            foreach(var renderer in chunkRenderers)Destroy(renderer.gameObject);
            chunkRenderers.Clear();
            return;
        }
        foreach(var chunk in CurrentRenderDimension.GetEnableChunks())
        {
            GameObject go = Instantiate(gameObject);
            var renderer = go.AddComponent<ChunkRenderer>();
            renderer.SetChunk(chunk);
            chunkRenderers.Add(renderer);
        }
    }

}