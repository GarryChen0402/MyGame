using UnityEngine;

// Test script: spawns one chunk at world origin and fills every block in it with stone.
// Attach to any object in the scene; runs once in Start.
public class ChunkFillerTester : MonoBehaviour
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

        var chunkGo = new GameObject("Chunk(0,0)");
        chunkGo.transform.position = CoordUtils.ChunkCoordToWorldPos(new Vector2Int(0, 0));
        var chunk = chunkGo.AddComponent<Chunk>();

        // Fill the whole vertical range of the chunk: world y in [-128, 256)
        int minY = CoordUtils.MinSubChunkYIndex * CoordUtils.BlockSizePerChunk;
        int maxY = CoordUtils.MaxSubChunkYIndex * CoordUtils.BlockSizePerChunk;
        int filled = 0;
        for (int y = minY; y < maxY; y++)
        for (int x = 0; x < CoordUtils.BlockSizePerChunk; x++)
        for (int z = 0; z < CoordUtils.BlockSizePerChunk; z++)
        {
            if (chunk.TrySetBlockAt(x, y, z, stoneId)) filled++;
        }

        Debug.Log($"Filled {filled} stone blocks into chunk (0,0), y {minY}..{maxY - 1}.");
    }
}
