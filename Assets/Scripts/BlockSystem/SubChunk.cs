using UnityEngine;


public class SubChunk
{
    private readonly ushort[] blockData;
    public static readonly int SubChunkBlockSize = 16;
    public Vector2Int ChunkCoord { get; private set; } = Vector2Int.zero;
    public int SubChunkIndexInChunk { get; private set; } = 0;
    public SubChunk(Vector2Int chunkCoord, int subChunkIndex)
    {
        ChunkCoord = chunkCoord;
        SubChunkIndexInChunk = subChunkIndex;
        blockData = new ushort[SubChunkBlockSize *  SubChunkBlockSize * SubChunkBlockSize];
    }


    // Position Utils Func
    public static bool IsCorrectCoord(int localX, int localY, int localZ)
    {
        return localX >= 0 && localX < SubChunkBlockSize
            && localY >= 0 && localY < SubChunkBlockSize
            && localZ >= 0 && localZ < SubChunkBlockSize;
    }
    public static bool IsCorrectCoord(Vector3Int localCoord)
    {
        return localCoord.x >= 0 && localCoord.x< SubChunkBlockSize
            && localCoord.y >= 0 && localCoord.y < SubChunkBlockSize
            && localCoord.z >= 0 && localCoord.z < SubChunkBlockSize;
    }
    /// <summary>
    /// Convert the Block Coord into SubChunk Local Coord, 
    /// The Block Coord come from the Chunk
    /// </summary>
    /// <param name="blockCoord"> the block coord in the dimision(world) </param>
    /// <returns>the coord in current subChunk</returns>
    public static Vector3Int BlockCoordToSubChunkLocalCoord(Vector3Int blockCoord)
    {
        return new Vector3Int
        (
            (blockCoord.x % SubChunkBlockSize + SubChunkBlockSize) * SubChunkBlockSize,
            (blockCoord.y % SubChunkBlockSize + SubChunkBlockSize) * SubChunkBlockSize,
            (blockCoord.z % SubChunkBlockSize + SubChunkBlockSize) * SubChunkBlockSize
        );
    }
    /// <summary>
    /// seem the BlockCoordToSubChunkLocalCoord(Vector3Int blockCoord)
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public static Vector3Int BlockCoordToSubChunkLocalCoord(int x, int y, int z)
    {
        return new Vector3Int
        (
            (x % SubChunkBlockSize + SubChunkBlockSize) * SubChunkBlockSize,
            (y % SubChunkBlockSize + SubChunkBlockSize) * SubChunkBlockSize,
            (z % SubChunkBlockSize + SubChunkBlockSize) * SubChunkBlockSize
        );
    }

    public Vector3Int SubChunkLocalCoordToChunkLocalCoord(int x, int y, int z)
    {
        return new Vector3Int
        (
            x,
            y + SubChunkBlockSize * SubChunkIndexInChunk,
            z
        );
    }

    public Vector3Int SubChunkLocalCoordToDimisionBlockCoord(int x, int y, int z)
    {
        return new Vector3Int
        (
            ChunkCoord.x * SubChunkBlockSize + x,
            SubChunkIndexInChunk * SubChunkBlockSize + y,
            ChunkCoord.y * SubChunkBlockSize + z
        );
    }

    private static int SubChunkLocalCoordToIndex(int x, int y, int z)
    {
        return x * SubChunkBlockSize * SubChunkBlockSize + y * SubChunkBlockSize + z;
    }

    private static int SubChunkLocalCoordToIndex(Vector3Int localCoord)
    {
        return localCoord.x * SubChunkBlockSize * SubChunkBlockSize + localCoord.y * SubChunkBlockSize + localCoord.z;
    }

    public ushort GetBlockAt(Vector3Int subChunkLocalCoord)
    {
        if (!IsCorrectCoord(subChunkLocalCoord)) return 0;
        int index = SubChunkLocalCoordToIndex(subChunkLocalCoord);
        return blockData[index];
    }

    public bool TrySetBlockAt(Vector3Int subChunkLocalCoord, ushort blockId)
    {
        if(!IsCorrectCoord(subChunkLocalCoord))return false;
        int index = SubChunkLocalCoordToIndex(subChunkLocalCoord);
        if(blockData[index] != 0)return false;
        blockData[index] = blockId;
        return true;
    }

    public Vector3 GetSubChunkOrigin()
    {
        return new Vector3
        (
            ChunkCoord.x * SubChunkBlockSize,
            SubChunkIndexInChunk * SubChunkBlockSize,
            ChunkCoord.y * SubChunkBlockSize
        );
    }

    public Mesh RenderMesh {  get; private set; } = new Mesh();
    public bool IsRenderMeshDirty { get; private set; } = true;

    public void RebuildRenderMesh()
    {
        if (!IsRenderMeshDirty) return;
        if(RenderMesh != null)Object.Destroy(RenderMesh);
        RenderMesh = SubChunkRenderMeshRebuilder.RebuildSubChunkRenderMesh(this);
        IsRenderMeshDirty = false;
    }

    
}