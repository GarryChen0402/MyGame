using UnityEngine;

// Deterministic address of a block entity instance (design C1): the type full
// name plus the owning chunk coordinate and the chunk-local block coordinate
// identify one instance for its whole lifetime.
public static class BlockEntityId
{
    public static string Of(BlockEntity be)
    {
        if(be == null)return string.Empty;
        var chunk = be.OwnerChunk != null ? be.OwnerChunk.ChunkCoord : Vector2Int.zero;
        var local = Chunk.DimensionCoordToChunkLocalCoord(be.Position);
        return $"{be.Definition?.FullName}#{chunk.x}:{chunk.y}:{local.x}:{local.y}:{local.z}";
    }
}
