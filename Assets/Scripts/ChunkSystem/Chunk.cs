using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Chunk : MonoBehaviour
{
    private SubChunk[] SubChunks ;

    public Vector2Int ChunkCoord {get; private set;}

    public bool IsDirty {get; private set;} = true;

    [SerializeField] 
    private MeshFilter meshFilter;
    [SerializeField]
    private MeshRenderer meshRenderer;
    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material = ResourceSystem.Instance.BlockMaterial;
        SubChunks = new SubChunk[CoordUtils.MaxSubChunkYIndex - CoordUtils.MinSubChunkYIndex + 1];
    }

    private void Update()
    {
        
    }

    private void LateUpdate()
    {
        if(IsDirty)RebuildDirtyMesh();
    }

    public ushort GetBlockAt(int x, int y, int z)
    {
        if(!CoordUtils.IsCorrectChunkLocalCoord(x, y, z))return 0;
        var subCHunk = GetSubChunk(y);
        return subCHunk?.GetBlockAt(x, CoordUtils.ChunkLocalYToSubChunkLocalY(y), z) ?? 0;
    }

    public bool TrySetBlockAt(int x, int y, int z, ushort block)
    {
        if(!CoordUtils.IsCorrectChunkLocalCoord(x, y, z))return false;
        var subChunk = GetOrCreateSubChunk(y);
        if(subChunk.TrySetBlockAt(x, CoordUtils.ChunkLocalYToSubChunkLocalY(y), z, block))
        {
            MarkDirty();
            return true;
        }
        return false;
    }

    private SubChunk GetOrCreateSubChunk(int worldY)
    {
        int yIndex = CoordUtils.WorldYPosToSubChunkYIndex(worldY) - CoordUtils.MinSubChunkYIndex;
        if(SubChunks[yIndex] == null)SubChunks[yIndex] = new SubChunk(ChunkCoord, yIndex + CoordUtils.MinSubChunkYIndex);
        return SubChunks[yIndex];
    }

    private SubChunk GetSubChunk(int worldY)
    {
        int yIndex = CoordUtils.WorldYPosToSubChunkYIndex(worldY) - CoordUtils.MinSubChunkYIndex;
        return SubChunks[yIndex];
    }

    public void RebuildDirtyMesh()
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        var uvs = new List<Vector2>();
        var colors = new List<Color>();
        var normals = new List<Vector3>();

        foreach(var sub in SubChunks)
        {
            if(sub == null) continue;
            // Rebuild the subchunk mesh when missing or stale
            if(sub.GetRenderMesh() == null || sub.IsDirty)
            {
                sub.RebuildRenderMesh();
            }
            if(sub.GetRenderMesh() == null) continue; // empty subchunk, nothing to merge

            int voff = verts.Count;
            Mesh subRenderMesh = sub.GetRenderMesh();
            // Subchunk meshes are already in chunk-local coordinates, merge as-is
            foreach(var v in subRenderMesh.vertices) verts.Add(v);
            uvs.AddRange(subRenderMesh.uv);
            colors.AddRange(subRenderMesh.colors);
            normals.AddRange(subRenderMesh.normals);
            foreach(int t in subRenderMesh.triangles)
                tris.Add(t + voff);
        }

        if (meshFilter.sharedMesh != null) Destroy(meshFilter.sharedMesh);

        var combined = new Mesh
        {
            // Merging up to 24 subchunk meshes can exceed the 65535 limit of 16-bit indices
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };
        combined.SetVertices(verts);
        combined.SetTriangles(tris, 0);
        combined.SetUVs(0, uvs);
        combined.SetColors(colors);
        combined.SetNormals(normals);
        combined.RecalculateBounds();

        meshFilter.sharedMesh = combined;
        IsDirty = false;
    }

    public void MarkDirty()
    {
        IsDirty = true;
    }

    public void SetChunkCoord(Vector2Int coord) => ChunkCoord = coord;
}