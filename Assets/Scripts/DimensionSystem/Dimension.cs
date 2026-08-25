

using System;
using System.Collections.Generic;
using UnityEngine;

public class Dimension
{
    public DimensionDefinition DimensionDefinitionInfo {get; private set;}

    public Dimension(DimensionDefinition def)
    {
        DimensionDefinitionInfo = def;
    }

    private readonly Dictionary<Vector2Int, Chunk> EnableChunks = new();
    private readonly Dictionary<Vector2Int, Chunk> DisableChunks = new();
    public virtual void FillNewChunk(Chunk chunk)
    {
        //DimensionGenerator, use the Dimension Definition to generate the new chunk of the dimension
        //TODO
    }

    public bool IsChunkEnabled(Vector2Int ChunkCoord) => EnableChunks.ContainsKey(ChunkCoord);
    public bool IsChunkDisabled(Vector2Int ChunkCoord) => DisableChunks.ContainsKey(ChunkCoord);

    public void LoadChunk(Vector2Int ChunkCoord)
    {
        if(IsChunkEnabled(ChunkCoord))return;
        if (IsChunkDisabled(ChunkCoord))
        {
            Chunk targetChunk = DisableChunks[ChunkCoord];
            DisableChunks.Remove(ChunkCoord);
            EnableChunks[ChunkCoord] = targetChunk;
            return;
        }


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
}