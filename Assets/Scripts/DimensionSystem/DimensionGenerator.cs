

using System;
using UnityEngine;

public abstract class DimensionGenerator
{
    public abstract void FillNewChunk(Chunk chunk, Vector2Int ChunkCoord, DimensionDefinition dimDefinition);
}

public class DimensionGeneratorResource : ResourceType
{
    public Func<DimensionGenerator> GetNewGenerator;
}