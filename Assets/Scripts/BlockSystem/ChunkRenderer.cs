using UnityEngine;

public class ChunkRenderer : MonoBehaviour
{
    [SerializeField]
    private Chunk chunk = null;

    private MeshFilter meshFilter = null;
    private MeshRenderer meshRenderer = null;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        if(meshFilter == null)gameObject.AddComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        if(meshRenderer == null)gameObject.AddComponent<MeshRenderer>();
    }

    private void Update()
    {
        if(chunk == null)return;

        if (chunk.IsRenderMeshDirty)
        {
            chunk.RebulidCombinedRenderMesh();
            meshFilter.sharedMesh = chunk.CombinedRenderMesh;
        }
    }

    public void SetChunk(Chunk chunk)
    {
        this.chunk = chunk;
        if(chunk.IsRenderMeshDirty)chunk.RebulidCombinedRenderMesh();
        meshFilter.sharedMesh = chunk.CombinedRenderMesh;
    }
}