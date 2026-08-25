using UnityEngine;

// Test script: creates one subchunk at world origin (chunk (0,0), index 0 => world y 0..15),
// fills every block with minecraft:stone, rebuilds its render mesh and displays it.
// Attach to any object in the scene; runs once in Start.
public class SubChunkTester : MonoBehaviour
{
    private void Start()
    {
        new Minecraft().RegisterAllResources();
        ResourceSystem.Instance.BuildAtlas();

        if (!ResourceSystem.Instance.BlockDefinitions.TryGetNumberId($"{Minecraft.ModId}:stone", out ushort stoneId))
        {
            Debug.LogError($"Block '{Minecraft.ModId}:stone' is not registered.");
            return;
        }

        var sub = new SubChunk(new Vector2Int(0, 0), 0);

        for (int y = 0; y < SubChunk.SubChunkBlockSize; y++)
        for (int x = 0; x < SubChunk.SubChunkBlockSize; x++)
        for (int z = 0; z < SubChunk.SubChunkBlockSize; z++)
            sub.TrySetBlockAt(new Vector3Int(x, y, z), stoneId);

        // Wire the neighbor query so interior faces are culled; coords outside this subchunk read as air
        Vector3Int originInt = Vector3Int.FloorToInt(sub.GetSubChunkOrigin());
        SubChunkRenderMeshRebuilder.QueryBlockIdAt = coord => sub.GetBlockAt(coord - originInt);

        sub.RebuildRenderMesh();

        var go = new GameObject("SubChunk(0,0,0)");
        go.transform.position = sub.GetSubChunkOrigin();   // mesh vertices are subchunk-local
        go.AddComponent<MeshFilter>().sharedMesh = sub.RenderMesh;
        go.AddComponent<MeshRenderer>().material = ResourceSystem.Instance.BlockMaterial;
    }
}
