using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ChunkRenderer : MonoBehaviour
{
    [SerializeField]
    private Chunk chunk = null;

    private MeshFilter meshFilter = null;
    private MeshRenderer meshRenderer = null;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
        meshRenderer.material = ResourceSystem.Instance.BlockMaterial;
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
    
}