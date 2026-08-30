using UnityEngine;

// Deterministic, platform-independent pseudo random helpers for world generation.
// System.Random is avoided: its sequence is not guaranteed identical across
// runtimes, which would break the "same seed → same world" rule.
public static class DeterministicNoise
{
    // SplitMix64 mixing.
    private static ulong Mix(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public static ulong Hash(ulong a, ulong b, ulong c, ulong d)
    {
        ulong h = 0x9E3779B97F4A7C15UL;
        h = Mix(h ^ a);
        h = Mix(h ^ b);
        h = Mix(h ^ c);
        return Mix(h ^ d);
    }

    // [0, 1)
    public static float UnitFloat(ulong h) => (float)(h & 0xFFFFFF) / 0x1000000;

    public static float Range(ulong h, float min, float max) => min + UnitFloat(h) * (max - min);

    // [0, 2π)
    public static float Angle(ulong h) => UnitFloat(h) * Mathf.PI * 2f;

    // Smooth value noise in [-1, 1]; cell corners are shared by neighbors, so the
    // field is continuous everywhere and only depends on world coordinates + seed.
    public static float Value2D(float x, float z, int seed, float frequency)
    {
        x *= frequency;
        z *= frequency;
        int x0 = Mathf.FloorToInt(x);
        int z0 = Mathf.FloorToInt(z);
        float fx = x - x0;
        float fz = z - z0;
        float u = Fade(fx);
        float v = Fade(fz);
        ulong s = (ulong)(uint)seed;
        ulong x0u = (ulong)(uint)x0;
        ulong z0u = (ulong)(uint)z0;
        float v00 = UnitFloat(Hash(s, x0u, z0u, 0));
        float v10 = UnitFloat(Hash(s, x0u + 1, z0u, 0));
        float v01 = UnitFloat(Hash(s, x0u, z0u + 1, 0));
        float v11 = UnitFloat(Hash(s, x0u + 1, z0u + 1, 0));
        return Mathf.Lerp(Mathf.Lerp(v00, v10, u), Mathf.Lerp(v01, v11, u), v) * 2f - 1f;
    }

    // 3D smooth value noise in [-1, 1].
    public static float Value3D(float x, float y, float z, int seed, float frequency)
    {
        x *= frequency;
        y *= frequency;
        z *= frequency;
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        int z0 = Mathf.FloorToInt(z);
        float fx = x - x0;
        float fy = y - y0;
        float fz = z - z0;
        float u = Fade(fx);
        float v = Fade(fy);
        float w = Fade(fz);
        ulong s = (ulong)(uint)seed;
        ulong x0u = (ulong)(uint)x0;
        ulong y0u = (ulong)(uint)y0;
        ulong z0u = (ulong)(uint)z0;
        float result = 0f;
        for(int dx = 0; dx <= 1; dx++)
        for(int dy = 0; dy <= 1; dy++)
        for(int dz = 0; dz <= 1; dz++)
        {
            float val = UnitFloat(Hash(s, x0u + (ulong)dx, y0u + (ulong)dy, z0u + (ulong)dz));
            float wx = dx == 0 ? 1f - u : u;
            float wy = dy == 0 ? 1f - v : v;
            float wz = dz == 0 ? 1f - w : w;
            result += val * wx * wy * wz;
        }
        return result * 2f - 1f;
    }

    // Perlin fade: C1 continuous, zero derivative at cell corners.
    public static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
}
