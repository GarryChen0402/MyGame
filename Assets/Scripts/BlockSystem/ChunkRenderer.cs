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
    }

    private void Update()
    {
        if(chunk == null)return;

        if (chunk.IsRenderMeshDirty)
        {
            // chunk.RebulidCombinedRenderMesh();
            // meshFilter.sharedMesh = chunk.CombinedRenderMesh;
            WorldRenderer.Instance.MarkChunkIntoRebuildQueue(chunk);
        }
    }

    public void SetChunk(Chunk chunk)
    {
        this.chunk = chunk;
        if(chunk.IsRenderMeshDirty)WorldRenderer.Instance.MarkChunkIntoRebuildQueue(chunk);
        // meshFilter.sharedMesh = chunk.CombinedRenderMesh;
    }

    public void RebuildCombinedRenderMesh()
    {
        chunk.RebulidCombinedRenderMesh();
        meshFilter.sharedMesh = chunk.CombinedRenderMesh;
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