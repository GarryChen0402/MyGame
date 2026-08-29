using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ChunkRenderer : MonoBehaviour
{
    [SerializeField]
    private Chunk chunk = null;

    private MeshFilter meshFilter = null;
    private MeshRenderer meshRenderer = null;

    // Double-buffered meshes: upload writes the idle mesh, then swaps the sharedMesh
    // reference, so the mesh being rendered is never mutated in place.
    // Meshes are created in Awake: Unity objects can't be built in field initializers.
    private readonly Mesh[] renderMeshes = new Mesh[2];
    private int activeMeshIndex = 0;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
        meshRenderer.material = ResourceSystem.Instance.BlockMaterial;
        renderMeshes[0] = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        renderMeshes[1] = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };

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
    }

    // A block inside this chunk changed.
    private void OnBlockChanged(BlockChangedEvent evt)
    {
        if(chunk == null || evt.ChunkCoord != chunk.ChunkCoord)return;
        WorldRenderer.Instance.MarkChunkIntoRebuildQueue(chunk, evt.FromInteraction);
    }

    // A neighbor was loaded or unloaded: this chunk's exposed faces may change.
    private void OnChunkLoaded(ChunkLoadedEvent evt)
    {
        if(chunk == null || !IsNeighbor(evt.Chunk.ChunkCoord))return;
        WorldRenderer.Instance.MarkChunkIntoRebuildQueue(chunk);
    }

    private void OnChunkUnloaded(ChunkUnloadedEvent evt)
    {
        if(chunk == null || !IsNeighbor(evt.ChunkCoord))return;
        WorldRenderer.Instance.MarkChunkIntoRebuildQueue(chunk);
    }

    private bool IsNeighbor(Vector2Int coord)
        => Mathf.Abs(coord.x - chunk.ChunkCoord.x) + Mathf.Abs(coord.y - chunk.ChunkCoord.y) == 1;

    public void SetChunk(Chunk chunk)
    {
        this.chunk = chunk;
        WorldRenderer.Instance.MarkChunkIntoRebuildQueue(chunk);
    }

    // Main thread only: uploads the task's computed data into the idle mesh and swaps it in.
    public void ApplyMeshData(ChunkMeshBuildTask task)
    {
        int target = 1 - activeMeshIndex;
        Mesh mesh = renderMeshes[target];
        mesh.Clear();
        mesh.SetVertices(task.Vertices);
        mesh.SetTriangles(task.Triangles, 0);
        mesh.SetUVs(0, task.Uvs);
        mesh.SetNormals(task.Normals);
        mesh.SetColors(task.Colors);
        mesh.RecalculateBounds();
        meshFilter.sharedMesh = mesh;
        activeMeshIndex = target;
    }
}
