

using System;
using System.Collections.Generic;
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
    public virtual void FillNewChunk(Chunk chunk)
    {
        //DimensionGenerator, use the Dimension Definition to generate the new chunk of the dimension
        //TODO
        if(Generator == null)return;
        Generator.FillNewChunk(chunk, chunk.ChunkCoord, DimensionDefinitionInfo);
    }

    public bool IsChunkEnabled(Vector2Int ChunkCoord) => EnableChunks.ContainsKey(ChunkCoord);
    public bool IsChunkDisabled(Vector2Int ChunkCoord) => DisableChunks.ContainsKey(ChunkCoord);

    public bool TryGetChunk(Vector2Int chunkCoord, out Chunk chunk)
        => EnableChunks.TryGetValue(chunkCoord, out chunk);

    public void LoadChunk(Vector2Int ChunkCoord)
    {
        if(IsChunkEnabled(ChunkCoord))return;
        if (IsChunkDisabled(ChunkCoord))
        {
            Chunk targetChunk = DisableChunks[ChunkCoord];
            DisableChunks.Remove(ChunkCoord);
            EnableChunks[ChunkCoord] = targetChunk;
            // Renderer meshes don't survive unload; force a rebuild on re-enable.
            targetChunk.MarkRenderMeshDirty();
            MarkNeighborsRenderMeshDirty(ChunkCoord);
            return;
        }
        // Create new Chunk
        GetOrCreateChunk(ChunkCoord);
        MarkNeighborsRenderMeshDirty(ChunkCoord);
    }

    public void UnloadChunk(Vector2Int ChunkCoord)
    {
        if(IsChunkDisabled(ChunkCoord))return;
        if (IsChunkEnabled(ChunkCoord))
        {
            Chunk target = EnableChunks[ChunkCoord];
            EnableChunks.Remove(ChunkCoord);
            DisableChunks[ChunkCoord] = target;
            // Neighbors lose a solid neighbor: their exposed faces must be re-rendered.
            MarkNeighborsRenderMeshDirty(ChunkCoord);
            return;
        }
    }

    private void MarkNeighborsRenderMeshDirty(Vector2Int chunkCoord)
    {
        MarkDirty(chunkCoord + new Vector2Int(1, 0));
        MarkDirty(chunkCoord + new Vector2Int(-1, 0));
        MarkDirty(chunkCoord + new Vector2Int(0, 1));
        MarkDirty(chunkCoord + new Vector2Int(0, -1));
    }

    private void MarkDirty(Vector2Int chunkCoord)
    {
        if (EnableChunks.TryGetValue(chunkCoord, out var chunk))
        {
            chunk.MarkRenderMeshDirty();
            WorldRenderer.Instance.MarkChunkIntoRebuildQueue(chunk);
        }
    }

    public Chunk GetOrCreateChunk(Vector2Int ChunkCoord)
    {
        if(IsChunkEnabled(ChunkCoord))return EnableChunks[ChunkCoord];
        if(IsChunkDisabled(ChunkCoord))return DisableChunks[ChunkCoord];
        Chunk chunk = new(ChunkCoord, DimensionDefinitionInfo.MinSubChunkIndex, DimensionDefinitionInfo.MaxSubChunkIndex);
        FillNewChunk(chunk);
        EnableChunks[ChunkCoord] = chunk;
        return chunk;
    }

    public ushort GetBlockAt(Vector3Int dimensionCoord)
    {
        var chunkCoord = DimensionCoordToChunkCoord(dimensionCoord);
        if(IsChunkDisabled(chunkCoord))return 0;
        if(IsChunkEnabled(chunkCoord))return EnableChunks[chunkCoord].GetBlockAt(Chunk.DimensionCoordToChunkLocalCoord(dimensionCoord));
        return 0;
    }

    private bool TrySetBlockAt(Vector3Int dimensionCoord, ushort blockId)
    {
        var chunk = GetOrCreateChunk(DimensionCoordToChunkCoord(dimensionCoord));
        return chunk.TrySetBlockAt(Chunk.DimensionCoordToChunkLocalCoord(dimensionCoord), blockId);
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