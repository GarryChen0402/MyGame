using System.Collections.Generic;
using UnityEngine;

// Infinite, deterministic, per-biome-density center point map (Voronoi style).
// Each biome owns an independent grid whose cell size is its SpawnRange; a cell
// yields at most one center point, derived from (seed, biome, cell) on demand.
// A query only inspects the 3x3 cells around the query position.
public class BiomeCenterMap
{
    private static readonly Dictionary<string, BiomeCenterMap> Cache = new();

    // Main thread only: the cache dictionary is unsynchronized. WorldManager
    // warms the map up before workers sample it, so worker threads only ever do
    // read-only lookups (GetBiomeAt / GetNearestTwo are pure deterministic queries).
    public static BiomeCenterMap GetOrCreate(string dimensionFullName, int seed)
    {
        if(Cache.TryGetValue(dimensionFullName, out var map))return map;
        map = new BiomeCenterMap(dimensionFullName, seed);
        Cache[dimensionFullName] = map;
        return map;
    }

    private readonly int seed;
    private readonly List<BiomeDefinition> biomes = new();

    // Coordinate warp (like vanilla's shift): bends the straight Voronoi borders
    // into irregular curves. Amplitude in blocks.
    private const float WarpAmplitude = 12f;
    private const float WarpFrequency = 1f / 24f;

    private BiomeCenterMap(string dimensionFullName, int seed)
    {
        this.seed = seed;
        foreach(var biome in ResourceSystem.Instance.BiomeDefinitions.Values)
            if(biome.DimensionId == dimensionFullName)biomes.Add(biome);
    }

    public bool HasBiomes => biomes.Count > 0;

    public BiomeDefinition GetBiomeAt(int x, int z)
    {
        GetNearestTwo(x, z, out var first, out _, out _);
        return first;
    }

    // Nearest and second-nearest center points with their distance difference.
    // The Voronoi border sits where d2 == d1, so d2 - d1 < TransitionWidth means
    // the position lies inside the transition band around a border.
    public void GetNearestTwo(int x, int z, out BiomeDefinition first, out BiomeDefinition second, out float distanceDiff)
    {
        // Warp the query position before the nearest-center lookup.
        float wx = x + DeterministicNoise.Value2D(x, z, seed ^ 0x1357, WarpFrequency) * WarpAmplitude;
        float wz = z + DeterministicNoise.Value2D(x + 100000f, z + 100000f, seed ^ 0x2468, WarpFrequency) * WarpAmplitude;

        first = null;
        second = null;
        float d1 = float.MaxValue, d2 = float.MaxValue;
        ulong seedU = (ulong)(uint)seed;
        for(int i = 0; i < biomes.Count; i++)
        {
            var biome = biomes[i];
            float spacing = biome.SpawnRange;
            int cellX = Mathf.FloorToInt(wx / spacing);
            int cellZ = Mathf.FloorToInt(wz / spacing);
            for(int cx = cellX - 1; cx <= cellX + 1; cx++)
            for(int cz = cellZ - 1; cz <= cellZ + 1; cz++)
            {
                ulong h = DeterministicNoise.Hash(seedU, (ulong)(uint)i, (ulong)(uint)cx, (ulong)(uint)cz);
                if(DeterministicNoise.UnitFloat(h) > biome.SpawnProbability)continue;
                // Center point offset within its cell; separate hashes for x/z.
                float ox = DeterministicNoise.UnitFloat(DeterministicNoise.Hash(h, 1, 0, 0)) * spacing;
                float oz = DeterministicNoise.UnitFloat(DeterministicNoise.Hash(h, 2, 0, 0)) * spacing;
                float px = cx * spacing + ox;
                float pz = cz * spacing + oz;
                float dx = wx - px;
                float dz = wz - pz;
                float dist = dx * dx + dz * dz;
                if(dist < d1)
                {
                    d2 = d1;
                    second = first;
                    d1 = dist;
                    first = biome;
                }
                else if(dist < d2)
                {
                    d2 = dist;
                    second = biome;
                }
            }
        }
        distanceDiff = first == null ? 0f : d2 - d1;
    }
}
