using System.Collections.Generic;
using UnityEngine;

public class Chunk 
{
    public Vector2Int ChunkCoord {get; private set;}
    public int MinSubChunkIndex {get; private set;}
    public int MaxSubChunkIndex {get; private set;}

    private readonly SubChunk[] subChunks;

    public Chunk(Vector2Int chunkCoord, int minSubChunkIndex, int maxSubChunkIndex)
    {
        ChunkCoord = chunkCoord;
        MinSubChunkIndex = minSubChunkIndex;
        MaxSubChunkIndex = maxSubChunkIndex;
        // For example : min = -4 max = 16 ==> block range : [-4 * 16 = -64 ,  16 * 16 = 256) , full subChunk count : 16 - ( -4 ) + 1 = 21 
        subChunks = new SubChunk[maxSubChunkIndex - minSubChunkIndex +1];
    }

    public bool IsCorrectChunkLocalCoord(int x, int y, int z)
    {
        return x >= 0 && x < SubChunk.SubChunkBlockSize
            && z >= 0 && z < SubChunk.SubChunkBlockSize
            && y >= MinSubChunkIndex * SubChunk.SubChunkBlockSize
            && y < (MaxSubChunkIndex + 1) * SubChunk.SubChunkBlockSize;
    }

    public bool IsCorrectChunkLocalCoord(Vector3Int ChunkLocalCoord)
    {
        return ChunkLocalCoord.x >= 0 && ChunkLocalCoord.x < SubChunk.SubChunkBlockSize
            && ChunkLocalCoord.z >= 0 && ChunkLocalCoord.z < SubChunk.SubChunkBlockSize
            && ChunkLocalCoord.y >= MinSubChunkIndex * SubChunk.SubChunkBlockSize
            && ChunkLocalCoord.y < (MaxSubChunkIndex + 1) * SubChunk.SubChunkBlockSize;
    }

    public static Vector3Int DimensionCoordToChunkLocalCoord(Vector3Int dimensionCoord)
    {
        return new Vector3Int
        (
            (dimensionCoord.x % SubChunk.SubChunkBlockSize + SubChunk.SubChunkBlockSize) % SubChunk.SubChunkBlockSize,
            dimensionCoord.y,
            (dimensionCoord.z % SubChunk.SubChunkBlockSize + SubChunk.SubChunkBlockSize) % SubChunk.SubChunkBlockSize
        );
    }

    public int DimensionYCoordToSubChunkYIndex(int yCoord)
    {
        return Mathf.FloorToInt(yCoord * 1.0f / SubChunk.SubChunkBlockSize) - MinSubChunkIndex;
    }

    public ushort GetBlockAt(Vector3Int chunkLocalCoord)
    {
        if(!IsCorrectChunkLocalCoord(chunkLocalCoord))return 0;
        int subChunkIndex = DimensionYCoordToSubChunkYIndex(chunkLocalCoord.y);
        if(subChunks[subChunkIndex] == null)return 0;
        else return subChunks[subChunkIndex].GetBlockAt(SubChunk.BlockCoordToSubChunkLocalCoord(chunkLocalCoord));
    }

    public bool TrySetBlockAt(Vector3Int chunkLocalCoord, ushort blockId)
    {
        if(!IsCorrectChunkLocalCoord(chunkLocalCoord))return false;
        int subChunkIndex = DimensionYCoordToSubChunkYIndex(chunkLocalCoord.y);
        if(subChunks[subChunkIndex] == null)subChunks[subChunkIndex] = CreateNewSubChunk(subChunkIndex);
        return subChunks[subChunkIndex].TrySetBlockAt(SubChunk.BlockCoordToSubChunkLocalCoord(chunkLocalCoord), blockId);
    }

    private SubChunk CreateNewSubChunk(int subChunkIndex)
    {
        return new SubChunk(ChunkCoord, subChunkIndex + MinSubChunkIndex);
    }

    public Mesh CombinedRenderMesh {get; private set;} = new();
    public bool IsRenderMeshDirty{get; private set; } = true;

    public void MarkRenderMeshDirty()
    {
        IsRenderMeshDirty = true;
        // Sub meshes are cached independently; the combined mesh rebuild reuses them,
        // so the dirty flag must cascade down or stale culling stays baked in.
        foreach (var sub in subChunks)
            if (sub != null) sub.MarkRenderMeshDirty();
    }

    public void RebulidCombinedRenderMesh()
    {
        if(!IsRenderMeshDirty)return;
        if(CombinedRenderMesh != null)Object.Destroy(CombinedRenderMesh);
        List<Vector3> verts = new();
        List<Vector2> uv = new();
        List<Color> colors = new();
        List<Vector3> normals = new();
        List<int> triangles = new();
        
        foreach(var sub in subChunks)
        {
            if(sub == null)continue;
            if(sub.IsRenderMeshDirty)sub.RebuildRenderMesh();
            Mesh subMesh = sub.RenderMesh;
            if(subMesh == null)continue;
            int vStart = verts.Count;
            
            foreach(var v in subMesh.vertices)verts.Add(v);
            foreach(var u in subMesh.uv)uv.Add(u);
            foreach(var t in subMesh.triangles)triangles.Add(vStart + t);
            foreach(var c in subMesh.colors)colors.Add(c);
            foreach(var n in subMesh.normals)normals.Add(n);
        }

        Mesh mesh = new();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uv);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        CombinedRenderMesh = mesh;
        IsRenderMeshDirty = false;
    }


    public int DistanceTo(Chunk other)
    {
        if(other == null)return int.MaxValue;
        return Mathf.Abs(ChunkCoord.x - other.ChunkCoord.x) + Mathf.Abs(ChunkCoord.y - other.ChunkCoord.y);
    }
}