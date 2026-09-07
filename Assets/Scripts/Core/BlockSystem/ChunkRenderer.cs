using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

// Renders one chunk as one mesh per subchunk (vanilla-style sections): a block
// edit rebuilds and uploads only the affected subchunk meshes instead of the
// whole chunk, and each subchunk is frustum-culled independently.
// Meshes are double-buffered per subchunk: upload writes the idle mesh, then
// swaps the sharedMesh reference, so the mesh being rendered is never mutated.
public class ChunkRenderer : MonoBehaviour
{
    [SerializeField]
    private Chunk chunk = null;

    // Per-subchunk render state; index s maps to chunk subchunk index
    // MinSubChunkIndex + s (task geometry is built at that subchunk's world Y).
    private class SubChunkRenderer
    {
        public MeshFilter Filter;
        public readonly Mesh[] Meshes = new Mesh[2];
        public int ActiveIndex = 0;
    }

    private readonly List<SubChunkRenderer> subRenderers = new();
    // Reused when rebasing a subchunk's triangle indices to its local vertex range.
    private readonly List<int> scratchTriangles = new();

    private void Awake()
    {
        EventBus.Instance.Subscribe<BlockChangedEvent>(OnBlockChanged);
        EventBus.Instance.Subscribe<ChunkLoadedEvent>(OnChunkLoaded);
        EventBus.Instance.Subscribe<ChunkUnloadedEvent>(OnChunkUnloaded);
    }

    private void OnDestroy()
    {
        // Unsubscribe is idempotent on multicast delegate snapshots; safe to call twice.
        EventBus.Instance.Unsubscribe<BlockChangedEvent>(OnBlockChanged);
        EventBus.Instance.Unsubscribe<ChunkLoadedEvent>(OnChunkLoaded);
        EventBus.Instance.Unsubscribe<ChunkUnloadedEvent>(OnChunkUnloaded);
        foreach(var sub in subRenderers)
            foreach(var mesh in sub.Meshes)
                if(mesh != null)Destroy(mesh);
    }

    // A block inside this chunk changed: the changed block's own subchunk is dirty,
    // plus its ±Y siblings (a block at a subchunk boundary feeds the neighbor
    // subchunk's shared horizontal plane) and, for boundary blocks, the adjacent
    // chunk's subchunk at the same index.
    private void OnBlockChanged(BlockChangedEvent evt)
    {
        if(chunk == null)return;
        int xDis = chunk.ChunkCoord.x - evt.ChunkCoord.x;
        int zDis = chunk.ChunkCoord.y - evt.ChunkCoord.y;
        bool sameChunk = xDis == 0 && zDis == 0;
        bool xBoundary = (xDis == 1 && evt.ChunkLocalCoord.x == 15) || (xDis == -1 && evt.ChunkLocalCoord.x == 0);
        bool zBoundary = (zDis == 1 && evt.ChunkLocalCoord.z == 15) || (zDis == -1 && evt.ChunkLocalCoord.z == 0);
        if(!sameChunk && !xBoundary && !zBoundary)return;

        int subIdx = chunk.DimensionYCoordToSubChunkYIndex(evt.ChunkLocalCoord.y);
        var dirtySubs = new HashSet<int> { subIdx };
        if(sameChunk)
        {
            int yLocal = ((evt.ChunkLocalCoord.y % SubChunk.SubChunkBlockSize) + SubChunk.SubChunkBlockSize) % SubChunk.SubChunkBlockSize;
            if(yLocal == 0)dirtySubs.Add(subIdx - 1);
            if(yLocal == SubChunk.SubChunkBlockSize - 1)dirtySubs.Add(subIdx + 1);
        }
        WorldRenderer.Instance.MarkChunkRebuildSubs(chunk, dirtySubs, evt.FromInteraction);
    }

    // A neighbor was loaded or unloaded: this chunk's exposed faces may change.
    private void OnChunkLoaded(ChunkLoadedEvent evt)
    {
        if(chunk == null || !IsNeighbor(evt.Chunk.ChunkCoord))return;
        WorldRenderer.Instance.MarkChunkRebuildFull(chunk, ChunkRebuildType.Normal);
    }

    private void OnChunkUnloaded(ChunkUnloadedEvent evt)
    {
        if(chunk == null || !IsNeighbor(evt.ChunkCoord))return;
        WorldRenderer.Instance.MarkChunkRebuildFull(chunk, ChunkRebuildType.Normal);
    }

    private bool IsNeighbor(Vector2Int coord)
        => Mathf.Abs(coord.x - chunk.ChunkCoord.x) + Mathf.Abs(coord.y - chunk.ChunkCoord.y) == 1;

    public void SetChunk(Chunk chunk)
    {
        this.chunk = chunk;
        // One child renderer per subchunk, positioned at the subchunk's world Y
        // (geometry is built relative to the chunk origin). sharedMaterial keeps
        // a single material instance for every subchunk renderer.
        int subCount = chunk.MaxSubChunkIndex - chunk.MinSubChunkIndex + 1;
        for(int s = 0; s < subCount; s++)
        {
            var go = new GameObject($"SubChunk {chunk.MinSubChunkIndex + s}");
            go.transform.SetParent(transform, false);
            // Vertices are built with their world Y already applied (SubOriginY),
            // so the subchunk renderer sits at the chunk origin like the old
            // merged mesh - offsetting it here would double the Y shift.
            go.transform.localPosition = Vector3.zero;
            var sub = new SubChunkRenderer();
            sub.Filter = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ResourceSystem.Instance.BlockMaterial;
            sub.Meshes[0] = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            sub.Meshes[1] = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            subRenderers.Add(sub);
        }
        WorldRenderer.Instance.MarkChunkRebuildFull(chunk, ChunkRebuildType.Initial);
    }

    // Last applied build result; partial rebuilds copy untouched subchunks from it.
    public ChunkMeshBuildTask AppliedTask {get; private set;}

    // Main thread only: uploads each rebuilt subchunk's range into its idle mesh
    // and swaps it in. Untouched subchunks (RebuildFlags false) keep the mesh from
    // the previous apply, so an edit uploads only the affected sections.
    public void ApplyMeshData(ChunkMeshBuildTask task)
    {
        for(int s = 0; s < subRenderers.Count; s++)
        {
            if(!task.RebuildFlags[s])continue;
            var sub = subRenderers[s];
            int target = 1 - sub.ActiveIndex;
            Mesh mesh = sub.Meshes[target];
            mesh.Clear();
            // Clear() resets subMeshCount to 0, so re-create submesh 0 explicitly.
            mesh.subMeshCount = 1;
            if(task.SubVertexCount[s] > 0)
            {
                mesh.SetVertices(task.Vertices, task.SubStartVertex[s], task.SubVertexCount[s]);
                mesh.SetUVs(0, task.Uvs, task.SubStartVertex[s], task.SubVertexCount[s]);
                mesh.SetNormals(task.Normals, task.SubStartVertex[s], task.SubVertexCount[s]);
                mesh.SetColors(task.Colors, task.SubStartVertex[s], task.SubVertexCount[s]);
                // Task triangle indices are absolute in the merged chunk list;
                // rebase them onto this subchunk's local vertex range.
                scratchTriangles.Clear();
                int startTri = task.SubStartTri[s];
                int baseVertex = task.SubStartVertex[s];
                for(int i = 0; i < task.SubTriCount[s]; i++)
                    scratchTriangles.Add(task.Triangles[startTri + i] - baseVertex);
                mesh.SetTriangles(scratchTriangles, 0);
            }
            mesh.RecalculateBounds();
            sub.Filter.sharedMesh = mesh;
            sub.ActiveIndex = target;
        }
        AppliedTask = task;
    }
}
