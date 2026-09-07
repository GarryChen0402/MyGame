using System;
using UnityEngine;

// Fills the (x, z) column of a chunk; one call handles exactly that column,
// from minY to maxY of the chunk (the whole vertical strip).
public delegate void FillColumnDelegate(DensityField densityField, Vector2Int chunkCoord, int x, int z);

public class BiomeDefinition : ResourceType
{
    // FullName of the Dimension this biome belongs to; biomes of a dimension are
    // found by reverse lookup on this field (no list stored on the dimension).
    public string DimensionId;

    // Spawn range: grid spacing of this biome's center points (per-biome density).
    // Small range → dense center points → frequent, small regions.
    public float SpawnRange = 48f;

    // Probability that a center point appears in a given grid cell; lower values
    // thin out this biome's presence.
    public float SpawnProbability = 1f;

    // --- Terrain generation baseline info ---
    public float BaseHeight;              // 地表高度基准值
    public float Scale;                   // 高度缩放:噪声波动范围
    public float MinGradientMagnitude = 0.5f;   // 角点梯度幅值范围
    public float MaxGradientMagnitude = 1f;
    public float TerrainLayerStrength;    // 地形层 3D 噪声强度(大尺度地层波动)

    public FillColumnDelegate FillColumn;
}
