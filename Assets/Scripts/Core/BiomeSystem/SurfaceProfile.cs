using UnityEngine;

// Per-chunk 2D surface profile: the four corner samples (baseline / gradient /
// scale / terrain layer strength, each derived from the corner's biome) are
// combined with a Perlin-style smooth interpolation. Corners are shared with
// neighboring chunks (they derive from world coordinates), so the result is
// continuous across biome and chunk borders.
public class SurfaceProfile
{
    public const int Size = 16;

    public float[] BaseHeight = new float[Size * Size];
    public float[] Noise = new float[Size * Size];        // 归一化噪声 ≈ [-1, 1]
    public float[] Scale = new float[Size * Size];
    public float[] LayerStrength = new float[Size * Size];

    // Max |gradient·(p-corner)| for a unit gradient over a 1x1 cell (√2/2);
    // divides the raw interpolation to keep the noise amplitude near [-1, 1].
    private const float GradientNormalizer = 0.70710678f;

    public void Build(BiomeCenterMap centerMap, Vector2Int chunkCoord, int seed)
    {
        Corner c00 = SampleCorner(centerMap, chunkCoord.x * Size, chunkCoord.y * Size, seed);
        Corner c10 = SampleCorner(centerMap, chunkCoord.x * Size + Size, chunkCoord.y * Size, seed);
        Corner c01 = SampleCorner(centerMap, chunkCoord.x * Size, chunkCoord.y * Size + Size, seed);
        Corner c11 = SampleCorner(centerMap, chunkCoord.x * Size + Size, chunkCoord.y * Size + Size, seed);

        for(int x = 0; x < Size; x++)
        for(int z = 0; z < Size; z++)
        {
            float fx = x / (float)Size;
            float fz = z / (float)Size;
            float u = DeterministicNoise.Fade(fx);
            float v = DeterministicNoise.Fade(fz);
            int idx = x * Size + z;

            // Perlin-style gradient contributions.
            float n00 = Vector2.Dot(c00.Gradient, new Vector2(fx, fz));
            float n10 = Vector2.Dot(c10.Gradient, new Vector2(fx - 1f, fz));
            float n01 = Vector2.Dot(c01.Gradient, new Vector2(fx, fz - 1f));
            float n11 = Vector2.Dot(c11.Gradient, new Vector2(fx - 1f, fz - 1f));
            Noise[idx] = Mathf.Lerp(Mathf.Lerp(n00, n10, u), Mathf.Lerp(n01, n11, u), v) / GradientNormalizer;

            // Baseline / scale / layer strength: fade-weighted bilinear blend, so
            // per-biome values transition smoothly instead of stepping at borders.
            BaseHeight[idx] = Bilerp(c00.BaseHeight, c10.BaseHeight, c01.BaseHeight, c11.BaseHeight, u, v);
            Scale[idx] = Bilerp(c00.Scale, c10.Scale, c01.Scale, c11.Scale, u, v);
            LayerStrength[idx] = Bilerp(c00.LayerStrength, c10.LayerStrength, c01.LayerStrength, c11.LayerStrength, u, v);
        }
    }

    private struct Corner
    {
        public float BaseHeight;
        public float Scale;
        public float LayerStrength;
        public Vector2 Gradient;   // direction × magnitude
    }

    private static Corner SampleCorner(BiomeCenterMap centerMap, int worldX, int worldZ, int seed)
    {
        var biome = centerMap.GetBiomeAt(worldX, worldZ);
        Corner c = new();
        if(biome == null)return c;   // dimension without biomes: neutral profile

        c.BaseHeight = biome.BaseHeight;
        c.Scale = biome.Scale;
        c.LayerStrength = biome.TerrainLayerStrength;

        // Gradient: deterministic random unit direction; magnitude randomized
        // within the biome's range, weighting this corner's influence on the noise.
        ulong s = (ulong)(uint)seed;
        ulong hx = (ulong)(uint)worldX;
        ulong hz = (ulong)(uint)worldZ;
        float angle = DeterministicNoise.Angle(DeterministicNoise.Hash(s, hx, hz, 1));
        float magnitude = DeterministicNoise.Range(DeterministicNoise.Hash(s, hx, hz, 2), biome.MinGradientMagnitude, biome.MaxGradientMagnitude);
        c.Gradient = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * magnitude;
        return c;
    }

    private static float Bilerp(float a00, float a10, float a01, float a11, float u, float v)
        => Mathf.Lerp(Mathf.Lerp(a00, a10, u), Mathf.Lerp(a01, a11, u), v);
}
