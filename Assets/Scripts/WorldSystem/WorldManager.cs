
using System.Collections.Generic;
using UnityEngine;

public class WorldManager
{
    public static WorldManager Instance {get;} = new();

    public readonly Dictionary<ushort, Dimension> Dimensions = new();

    private Vector2Int lastPlayerChunkCoord = new(int.MaxValue, int.MaxValue);
    private const int ChunkLoadRange = 8;

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

    // Controller entry for player block operations. Returns false when the target
    // position is already occupied or the dimension doesn't exist.
    public bool TryPlaceBlock(ushort dimId, Vector3Int dimensionCoord, ushort blockId)
    {
        if(!TryGetOrGenerateDimension(dimId, out var dim))return false;
        return dim.TrySetBlockAt(dimensionCoord, blockId);
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
        // Load a square of chunks with |x|, |z| <= range (Chebyshev distance)
        for(int x = -range; x <= range; x++)
            for(int z = -range; z <= range; z++)
                dim.LoadChunk(centerChunkCoord + new Vector2Int(x, z));

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

    // public ushort GetBlockAt(ushort dimId)
}
