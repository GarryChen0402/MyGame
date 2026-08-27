using System.Collections.Generic;
using UnityEngine;

// Worker-thread chunk mesh build task.
// Input data is snapshotted on the main thread (SnapShot); the worker only reads
// the snapshot plus read-only resource data, so no shared state is mutated off-thread.
public class ChunkMeshBuildTask
{
    public Chunk chunk;
    public ushort[][] SubChunkBlockData;
    // [subIdx, face, u, v] neighbor block ids just outside the subchunk.
    // face 0/1 (-X/+X): u=z, v=y | face 2/3 (-Y/+Y): u=x, v=z | face 4/5 (-Z/+Z): u=x, v=y
    public ushort[,,,] BoundaryPlances;
    public float[] SubOriginY;

    public List<Vector3> Vertices = new();
    public List<Vector2> Uvs = new();
    public List<Color> Colors = new();
    public List<Vector3> Normals = new();
    public List<int> Triangles = new();

    // Memory barrier: worker writes last, main thread reads.
    public volatile bool IsDown;

    public ChunkMeshBuildTask(Chunk chunk) => this.chunk = chunk;

    // Main thread only. Copies block data and pre-fetches the 6 boundary planes
    // so the worker never touches Dimension / Chunk / SubChunk live state.
    public void SnapShot()
    {
        Dimension dim = WorldRenderer.Instance.CurrentRenderDimension;
        if (dim == null) return;

        int subCount = chunk.MaxSubChunkIndex - chunk.MinSubChunkIndex + 1;
        SubChunkBlockData = new ushort[subCount][];
        SubOriginY = new float[subCount];
        for (int i = 0; i < subCount; i++)
        {
            SubChunk sub = chunk.GetSubChunk(chunk.MinSubChunkIndex + i);
            if (sub == null) continue;
            SubChunkBlockData[i] = sub.CopyBlockData();
            SubOriginY[i] = (chunk.MinSubChunkIndex + i) * SubChunk.SubChunkBlockSize;
        }

        BoundaryPlances = new ushort[subCount, 6, 16, 16];
        for (int i = 0; i < subCount; i++)
            FillBoundaryPlances(dim, i, chunk.MinSubChunkIndex + i);
    }

    // Resolve the owner subchunk once per face, then read its array directly (O(1)).
    private void FillBoundaryPlances(Dimension dim, int subIdx, int sy)
    {
        int cx = chunk.ChunkCoord.x, cz = chunk.ChunkCoord.y;

        // -X / +X: owner chunk on either side, same subchunk index; local (x fixed, y=v, z=u)
        SubChunk subNX = ResolveNeighborSub(dim, cx - 1, cz, sy);
        SubChunk subPX = ResolveNeighborSub(dim, cx + 1, cz, sy);
        for (int u = 0; u < 16; u++)
            for (int v = 0; v < 16; v++)
            {
                BoundaryPlances[subIdx, 0, u, v] = subNX == null ? (ushort)0 : subNX.GetBlockAtRaw(15, v, u);
                BoundaryPlances[subIdx, 1, u, v] = subPX == null ? (ushort)0 : subPX.GetBlockAtRaw(0, v, u);
            }

        // -Y / +Y: sibling subchunk in the same chunk; local (x=u, y fixed, z=v)
        SubChunk subNY = chunk.GetSubChunk(sy - 1);
        SubChunk subPY = chunk.GetSubChunk(sy + 1);
        for (int u = 0; u < 16; u++)
            for (int v = 0; v < 16; v++)
            {
                BoundaryPlances[subIdx, 2, u, v] = subNY == null ? (ushort)0 : subNY.GetBlockAtRaw(u, 15, v);
                BoundaryPlances[subIdx, 3, u, v] = subPY == null ? (ushort)0 : subPY.GetBlockAtRaw(u, 0, v);
            }

        // -Z / +Z: owner chunk before/after along z, same subchunk index; local (x=u, y=v, z fixed)
        SubChunk subNZ = ResolveNeighborSub(dim, cx, cz - 1, sy);
        SubChunk subPZ = ResolveNeighborSub(dim, cx, cz + 1, sy);
        for (int u = 0; u < 16; u++)
            for (int v = 0; v < 16; v++)
            {
                BoundaryPlances[subIdx, 4, u, v] = subNZ == null ? (ushort)0 : subNZ.GetBlockAtRaw(u, v, 15);
                BoundaryPlances[subIdx, 5, u, v] = subPZ == null ? (ushort)0 : subPZ.GetBlockAtRaw(u, v, 0);
            }
    }

    private static SubChunk ResolveNeighborSub(Dimension dim, int cx, int cz, int sy)
        => dim.TryGetChunk(new Vector2Int(cx, cz), out Chunk c) ? c.GetSubChunk(sy) : null;
}
