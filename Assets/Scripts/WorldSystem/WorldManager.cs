
using System.Collections.Generic;
using UnityEngine;

public class WorldManager 
{
    public static WorldManager Instance {get;} = new();

    public readonly Dictionary<ushort, Dimension> Dimensions = new();

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
        // Load a diamond of chunks within Manhattan distance <= range
        for(int x = -range; x <= range; x++)
        {
            int maxZ = range - Mathf.Abs(x);
            for(int z = -maxZ; z <= maxZ; z++)
                dim.LoadChunk(centerChunkCoord + new Vector2Int(x, z));
        }

        Chunk centerChunk = dim.GetOrCreateChunk(centerChunkCoord);
        List<Vector2Int> unloadPendingChunkCoords = new();
        foreach(var chunk in dim.GetEnableChunks())
        {
            if(chunk.DistanceTo(centerChunk) > range)unloadPendingChunkCoords.Add(chunk.ChunkCoord);
        }

        foreach(var coord in unloadPendingChunkCoords)dim.UnloadChunk(coord);
    }

    // public ushort GetBlockAt(ushort dimId)
}