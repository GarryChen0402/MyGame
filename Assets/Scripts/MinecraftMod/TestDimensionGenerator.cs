
using System.Collections.Generic;
using UnityEngine;

public class TestDimensionGenerator : DimensionGenerator
{
    public override void FillNewChunk(Chunk chunk, Vector2Int ChunkCoord, DimensionDefinition dimDefinition)
    {
        string[] blockNames = new string[]
        {
            "minecraft:stone",
            "minecraft:dirt",
            "minecraft:grass"
        };
        List<ushort> blockId = new();
        foreach(var name in blockNames)
        {
            if(!ResourceSystem.Instance.BlockDefinitions.TryGetNumberId(name, out var id))continue;
            blockId.Add(id);
        }

        for(int ycoord = dimDefinition.MinSubChunkIndex; ycoord <= dimDefinition.MaxSubChunkIndex; ycoord++)
        {
            FillAll(
                chunk,
                blockId[(ycoord - dimDefinition.MinSubChunkIndex + blockId.Count) % blockId.Count] ,
                ycoord
            );
        }
    }

    private void FillAll(Chunk sub, ushort blockId, int ycoord)
    {
        int size = SubChunk.SubChunkBlockSize;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        for (int z = 0; z < size; z++)
            sub.TrySetBlockAt(new Vector3Int(x, y + ycoord * size, z), blockId);
    }
}