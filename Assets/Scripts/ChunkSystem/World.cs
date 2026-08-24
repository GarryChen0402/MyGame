

using System.Collections.Generic;
using UnityEngine;

public class World
{
    public readonly Dictionary<Vector2Int, Chunk> Chunks = new();
    public IWorldGenerator WorldGenerator {get; private set;} = null;
    public Chunk GetOrCreateChunk(float x, float y, float z)
    {
        if(!CoordUtils.IsCorrectWorldPos(y))return null;
        Vector2Int chunkCoord = CoordUtils.WorldPosToChunkCoord(new Vector3(x, y, z));
        if(!Chunks.TryGetValue(chunkCoord, out Chunk chunk))
        {
            if(WorldGenerator == null)return null;
            GameObject go = new();
            go.transform.position = CoordUtils.ChunkCoordToWorldPos(chunkCoord);
            Chunk c = go.AddComponent<Chunk>();
            WorldGenerator.GenerateChunk(c);
            Chunks[chunkCoord] = c;
            c.SetChunkCoord(chunkCoord);
            return Chunks[chunkCoord];
        }else
        {
            return chunk;
        }
    }

    public Chunk GetChunk(float x, float y, float z)
    {
        if(!CoordUtils.IsCorrectWorldPos(y))return null;
        Vector2Int chunkCoord = CoordUtils.WorldPosToChunkCoord(new Vector3(x, y, z));
        if(!Chunks.TryGetValue(chunkCoord, out Chunk chunk))return null;
        else return chunk;
    }

    public ushort GetBlock(float x, float y, float z)
    {
        var chunk = GetChunk(x, y, z);
        Vector3Int chunkLocalCoord = CoordUtils.WorldPosToChunkLocalPos(x, y, z);
        if(chunk == null)return 0;
        else return chunk.GetBlockAt(chunkLocalCoord.x, chunkLocalCoord.y, chunkLocalCoord.z);
    }

    public bool SetBlockAt(float x, float y, float z, ushort blockId)
    {
        var chunk =GetOrCreateChunk(x, y, z);
        if(chunk == null)return false;
        Vector3Int chunklc = CoordUtils.WorldPosToChunkLocalPos(x, y, z);
        return chunk.TrySetBlockAt(chunklc.x, chunklc.y, chunklc.z, blockId);
    }

    public void SetWorldGenerator(IWorldGenerator generator)
    {
        WorldGenerator = generator;
    }


    public void UnloadChunk(Vector2Int chunkCoord)
    {
        if(Chunks.TryGetValue(chunkCoord, out var c))
        {
            Chunks.Remove(chunkCoord);
            GameObject.Destroy(c.gameObject);
        }
    }

    public void LoadChunk(Vector2Int chunkCoord)
    {
        if(!Chunks.TryGetValue(chunkCoord, out var c))
        {
            Vector3 worldPos = CoordUtils.ChunkCoordToWorldPos(chunkCoord);
            GetOrCreateChunk(worldPos.x, worldPos.y, worldPos.z);
        }
    }

}