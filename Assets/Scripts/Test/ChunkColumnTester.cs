using UnityEngine;

// Test script: creates one chunk at world origin (chunk (0,0)) spanning subchunk
// indices -1, 0, 1. From top to bottom each subchunk is fully filled with grass,
// dirt and stone, then rendered as a single combined mesh.
// Attach to any object in the scene; runs once in Start.
public class ChunkColumnTester : MonoBehaviour
{
    private void Start()
    {
        new Minecraft().RegisterAllResources();
        ResourceSystem.Instance.BuildAtlas();

        string mod = Minecraft.ModId;
        if (!TryGetBlockId(mod, "grass", out ushort grassId)
            || !TryGetBlockId(mod, "dirt", out ushort dirtId)
            || !TryGetBlockId(mod, "stone", out ushort stoneId))
            return;

        var chunk = new Chunk(Vector2Int.zero, -1, 1); // subchunk indices -1, 0, 1
        FillAll(chunk, grassId, 1);  // top subchunk
        FillAll(chunk, dirtId, 0);   // middle subchunk
        FillAll(chunk, stoneId, -1); // bottom subchunk
        chunk.RebulidCombinedRenderMesh();

        var go = new GameObject("Chunk(0,0)");
        go.transform.position = Vector3.zero;
        go.AddComponent<MeshFilter>().sharedMesh = chunk.CombinedRenderMesh;
        go.AddComponent<MeshRenderer>().material = ResourceSystem.Instance.BlockMaterial;
    }

    private static bool TryGetBlockId(string mod, string name, out ushort id)
    {
        if (ResourceSystem.Instance.BlockDefinitions.TryGetNumberId($"{mod}:{name}", out id)) return true;
        Debug.LogError($"Block '{mod}:{name}' is not registered.");
        return false;
    }

    private static void FillAll(Chunk chunk, ushort blockId, int subChunkIndex)
    {
        int size = SubChunk.SubChunkBlockSize;
        int yBase = subChunkIndex * size;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        for (int z = 0; z < size; z++)
            chunk.TrySetBlockAt(new Vector3Int(x, yBase + y, z), blockId);
    }
}
