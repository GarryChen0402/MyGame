using UnityEngine;

// 3D density field of a chunk: density > 0 → solid block, ≤ 0 → air.
// Built once per chunk from the surface profile plus Y-direction layers
// (large-scale terrain layer + cave carving).
public class DensityField
{
    public const int Size = 16;

    public Chunk Chunk { get; private set; }
    public Vector2Int ChunkCoord { get; private set; }
    public int MinY { get; private set; }
    public int Height { get; private set; }

    private readonly float[] data;

    public DensityField(Chunk chunk, int minY, int maxYExclusive)
    {
        Chunk = chunk;
        ChunkCoord = chunk.ChunkCoord;
        MinY = minY;
        Height = maxYExclusive - minY;
        data = new float[Size * Size * Height];
    }

    // y is the chunk-local y coordinate (MinY <= y < MinY + Height).
    public float GetDensity(int x, int y, int z)
        => data[(x * Size + z) * Height + (y - MinY)];

    // density = (surface - y) + layerStrength * terrainNoise + cave carving.
    public void Build(SurfaceProfile profile, int seed)
    {
        const float terrainFrequency = 1f / 40f;    // 大尺度地层波动
        const float caveFrequency = 1f / 22f;       // 主洞穴骨架
        const float caveDetailFrequency = 1f / 11f; // 洞穴细节
        const float caveThreshold = 0.55f;
        const float caveStrength = 1.2f;

        for(int x = 0; x < Size; x++)
        for(int z = 0; z < Size; z++)
        {
            int pidx = x * Size + z;
            float surface = profile.BaseHeight[pidx] + profile.Noise[pidx] * profile.Scale[pidx];
            float layer = profile.LayerStrength[pidx];
            float worldX = ChunkCoord.x * Size + x;
            float worldZ = ChunkCoord.y * Size + z;

            for(int y = 0; y < Height; y++)
            {
                float worldY = MinY + y;
                float density = surface - worldY;

                density += layer * DeterministicNoise.Value3D(worldX, worldY, worldZ, seed ^ 0xACE1, terrainFrequency);

                float cave = DeterministicNoise.Value3D(worldX, worldY, worldZ, seed ^ 0xBEEF, caveFrequency)
                           + 0.5f * DeterministicNoise.Value3D(worldX, worldY, worldZ, seed ^ 0xCAFE, caveDetailFrequency);
                if(cave > caveThreshold)
                    density -= (cave - caveThreshold) * caveStrength;

                data[(x * Size + z) * Height + y] = density;
            }
        }
    }
}
