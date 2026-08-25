using System.Collections.Generic;
using UnityEngine;

// Test script: creates one chunk at world origin (chunk (0,0)) as a column of three
// subchunks with indices -1, 0, 1. From top to bottom each subchunk is fully filled
// with grass, dirt and stone. Every subchunk renders as its own GameObject.
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

        var subs = new Dictionary<int, SubChunk>();
        SubChunk stoneSub = subs[-1] = new SubChunk(new Vector2Int(0, 0), -1); // bottom
        SubChunk dirtSub  = subs[0]  = new SubChunk(new Vector2Int(0, 0), 0);  // middle
        SubChunk grassSub = subs[1]  = new SubChunk(new Vector2Int(0, 0), 1);  // top
        FillAll(grassSub, grassId);
        FillAll(dirtSub, dirtId);
        FillAll(stoneSub, stoneId);

        // Wire the neighbor query so faces between subchunks are culled as well;
        // coords outside this chunk's column read as air
        SubChunkRenderMeshRebuilder.QueryBlockIdAt = coord =>
        {
            int size = SubChunk.SubChunkBlockSize;
            int idx = Mathf.FloorToInt(coord.y / (float)size);
            if (!subs.TryGetValue(idx, out var sub)) return 0;
            return sub.GetBlockAt(coord - new Vector3Int(0, idx * size, 0));
        };

        foreach (var kvp in subs)
        {
            var sub = kvp.Value;
            sub.RebuildRenderMesh();
            var go = new GameObject($"SubChunk(0,{kvp.Key},0)");
            go.transform.position = sub.GetSubChunkOrigin(); // mesh vertices are subchunk-local
            go.AddComponent<MeshFilter>().sharedMesh = sub.RenderMesh;
            go.AddComponent<MeshRenderer>().material = ResourceSystem.Instance.BlockMaterial;
        }
    }

    private static bool TryGetBlockId(string mod, string name, out ushort id)
    {
        if (ResourceSystem.Instance.BlockDefinitions.TryGetNumberId($"{mod}:{name}", out id)) return true;
        Debug.LogError($"Block '{mod}:{name}' is not registered.");
        return false;
    }

    private static void FillAll(SubChunk sub, ushort blockId)
    {
        int size = SubChunk.SubChunkBlockSize;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        for (int z = 0; z < size; z++)
            sub.TrySetBlockAt(new Vector3Int(x, y, z), blockId);
    }

    // Minecraft.cs currently only registers air/stone, so grass/dirt are filled in here.
    // Remove once Minecraft.cs registers all three block definitions.
    private static void RegisterMissingBlockDefinitions()
    {
        string mod = Minecraft.ModId;
        if (!ResourceSystem.Instance.CustomModels.TryGetResourceWithFullName($"{mod}:full_block", out var cube))
        {
            Debug.LogError($"Model '{mod}:full_block' is not registered.");
            return;
        }

        if (!ResourceSystem.Instance.BlockDefinitions.ContainsValue($"{mod}:dirt"))
            ResourceSystem.Instance.BlockDefinitions.Register(new BlockDefinition
            {
                modId = mod,
                name = "dirt",
                ModelId = cube.FullName,
                TextureIds = new()
                {
                    ["top"] = $"{mod}:dirt", ["bottom"] = $"{mod}:dirt",
                    ["front"] = $"{mod}:dirt", ["back"] = $"{mod}:dirt",
                    ["left"] = $"{mod}:dirt", ["right"] = $"{mod}:dirt"
                }
            });

        if (!ResourceSystem.Instance.BlockDefinitions.ContainsValue($"{mod}:grass"))
            ResourceSystem.Instance.BlockDefinitions.Register(new BlockDefinition
            {
                modId = mod,
                name = "grass",
                ModelId = cube.FullName,
                TextureIds = new()
                {
                    ["top"] = $"{mod}:grass", ["bottom"] = $"{mod}:dirt",
                    ["front"] = $"{mod}:grass_side", ["back"] = $"{mod}:grass_side",
                    ["left"] = $"{mod}:grass_side", ["right"] = $"{mod}:grass_side"
                }
            });
    }
}
