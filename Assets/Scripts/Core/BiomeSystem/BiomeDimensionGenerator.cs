using UnityEngine;

// Replaces the old generator: center point map → corner noise → surface profile →
// 3D density field → per-column biome fill rules (FillColumn).
public class BiomeDimensionGenerator : DimensionGenerator
{
    // 过渡带:到 Voronoi 边界距离 ≤ 8 格 → 均等随机采用最近两个群系的填充规则。
    // 到边界距离 ≈ (d2 - d1) / 2,故距离差阈值取 16。
    private const float TransitionWidth = 16f;

    public override void FillNewChunk(Chunk chunk, Vector2Int chunkCoord, DimensionDefinition dimDefinition)
    {
        int seed = WorldManager.Instance.Seed;
        var centerMap = BiomeCenterMap.GetOrCreate(dimDefinition.FullName, seed);
        if(!centerMap.HasBiomes)return;

        var profile = new SurfaceProfile();
        profile.Build(centerMap, chunkCoord, seed);

        var density = new DensityField(chunk, dimDefinition.MinSubChunkIndex * 16, (dimDefinition.MaxSubChunkIndex + 1) * 16);
        density.Build(profile, seed);

        for(int x = 0; x < DensityField.Size; x++)
        for(int z = 0; z < DensityField.Size; z++)
        {
            int worldX = chunkCoord.x * 16 + x;
            int worldZ = chunkCoord.y * 16 + z;

            BiomeDefinition fill = SelectFillBiome(centerMap, worldX, worldZ, seed);
            if(fill?.FillColumn != null)
                fill.FillColumn(density, chunkCoord, x, z);
        }
    }

    private static BiomeDefinition SelectFillBiome(BiomeCenterMap centerMap, int worldX, int worldZ, int seed)
    {
        centerMap.GetNearestTwo(worldX, worldZ, out var first, out var second, out float diff);
        if(second == null || diff >= TransitionWidth)return first;
        // 过渡带内:均等随机采用最近的两个群系之一(确定性随机)。
        ulong h = DeterministicNoise.Hash((ulong)(uint)seed, (ulong)(uint)worldX, (ulong)(uint)worldZ, 7);
        return DeterministicNoise.UnitFloat(h) < 0.5f ? first : second;
    }
}
