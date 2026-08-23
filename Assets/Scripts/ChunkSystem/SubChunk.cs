using UnityEngine;

public class SubChunk
{
    public bool IsDirty {get; private set;} = true;
    public Vector2Int ChunkCoord {get; private set;}

    public int YCoord {get; private set;}

    private readonly ushort[,,] blocksData = new ushort[CoordUtils.BlockSizePerChunk, CoordUtils.BlockSizePerChunk, CoordUtils.BlockSizePerChunk];
    private Mesh RenderMesh = null;

    public SubChunk(Vector2Int ChunkCoord, int YCoord)
    {
        this.ChunkCoord = ChunkCoord;
        this.YCoord = YCoord;
    }

    public ushort GetBlockAt(int x, int y, int z)
    {
        if(!CoordUtils.IsCorrectSubChunkLocalCoord(x, y, z))return 0;
        return blocksData[x, y, z];
    }

    public void MarkDirty() => IsDirty = true;

    public bool TrySetBlockAt(int x, int y, int z, ushort blockId)
    {
        ushort curBlockId = GetBlockAt(x, y, z);
        if(curBlockId != 0)return false;
        blocksData[x, y, z] = blockId;
        MarkDirty();
        return true;
    }

    public Mesh GetRenderMesh()
    {
        return RenderMesh;
    }

    public void SetRenderMesh(Mesh RenderMesh)
    {
        this.RenderMesh = RenderMesh;
    }

    // Rebuilds the render mesh from the current block data and clears the dirty flag.
    public void RebuildRenderMesh()
    {
        RenderMesh = RenderMeshBuilder.ReBuildSubChunkRenderMesh(this);
        IsDirty = false;
    }

}