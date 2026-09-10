using UnityEngine;

public partial class Minecraft
{
    // ---- G: biomes ----
    private void RegisterBiomes()
    {
        // Biome Definitions
        ResourceSystem.Instance.BiomeDefinitions.Register(new BiomeDefinition()
        {
            modId = ModId,
            name = "plains",
            DimensionId = testDi.FullName,
            SpawnRange = 96f,
            SpawnProbability = 1f,
            BaseHeight = 67f,
            Scale = 3f,
            MinGradientMagnitude = 0.4f,
            MaxGradientMagnitude = 0.8f,
            TerrainLayerStrength = 0.15f,
            FillColumn = FillPlainsColumn
        });
        ResourceSystem.Instance.BiomeDefinitions.Register(new BiomeDefinition()
        {
            modId = ModId,
            name = "mountains",
            DimensionId = testDi.FullName,
            SpawnRange = 128f,
            SpawnProbability = 1f,
            BaseHeight = 92f,
            Scale = 14f,
            MinGradientMagnitude = 0.8f,
            MaxGradientMagnitude = 1.4f,
            TerrainLayerStrength = 0.35f,
            FillColumn = FillMountainsColumn
        });
    }

    // 平原填充:地表草方块,下 3 格泥土,再下石头;洞穴(密度 ≤ 0)处留空。
    private static void FillPlainsColumn(DensityField density, Vector2Int chunkCoord, int x, int z)
    {
        var chunk = density.Chunk;
        int minY = density.MinY;
        int maxY = minY + density.Height;
        int topY = -1;
        for(int y = maxY - 1; y >= minY; y--)
        {
            if(density.GetDensity(x, y, z) > 0) { topY = y; break; }
        }
        if(topY < minY)return;
        // Chunk data stores state ids; blocks without properties use their default state.
        ushort grassState = ResourceSystem.Instance.GetDefaultState(grassId);
        ushort dirtState = ResourceSystem.Instance.GetDefaultState(dirtId);
        ushort stoneState = ResourceSystem.Instance.GetDefaultState(stoneId);
        for(int y = topY; y >= minY; y--)
        {
            if(density.GetDensity(x, y, z) <= 0)continue;
            ushort id = y == topY ? grassState : y >= topY - 3 ? dirtState : stoneState;
            chunk.TrySetBlockAt(new Vector3Int(x, y, z), id);
        }
    }

    // 山地填充:全部石头。
    private static void FillMountainsColumn(DensityField density, Vector2Int chunkCoord, int x, int z)
    {
        var chunk = density.Chunk;
        int minY = density.MinY;
        int maxY = minY + density.Height;
        ushort stoneState = ResourceSystem.Instance.GetDefaultState(stoneId);
        for(int y = maxY - 1; y >= minY; y--)
        {
            if(density.GetDensity(x, y, z) > 0)
                chunk.TrySetBlockAt(new Vector3Int(x, y, z), stoneState);
        }
    }
}
