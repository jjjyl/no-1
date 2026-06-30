using Godot;

namespace No1.World;

public static class TerrainGenerator
{
    public static (Biome[,] biomeMap, float[,] heightMap) Generate(ulong seed)
    {
        var heightNoise = new FastNoiseLite
        {
            Seed = (int)seed,
            Frequency = 0.008f,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
            FractalOctaves = 4,
            FractalLacunarity = 2.5f,
            FractalGain = 0.5f
        };

        var tempNoise = new FastNoiseLite
        {
            Seed = (int)(seed + 100),
            Frequency = 0.005f,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
            FractalOctaves = 3
        };

        var humidityNoise = new FastNoiseLite
        {
            Seed = (int)(seed + 200),
            Frequency = 0.006f,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
            FractalOctaves = 3
        };

        int worldWidth = WorldConstants.WorldWidth;
        int worldHeight = WorldConstants.WorldHeight;
        var biomeMap = new Biome[worldWidth, worldHeight];
        var heightMap = new float[worldWidth, worldHeight];

        for (int y = 0; y < worldHeight; y++)
        {
            float latMod = (1f - (float)y / worldHeight) * 0.5f;

            for (int x = 0; x < worldWidth; x++)
            {
                float h = (heightNoise.GetNoise2D(x, y) + 1f) * 0.5f;
                float t = (tempNoise.GetNoise2D(x, y) + 1f) * 0.5f;
                float m = (humidityNoise.GetNoise2D(x, y) + 1f) * 0.5f;

                t = (t + latMod) / 2f;
                t -= h * 0.3f;
                t = Mathf.Clamp(t, 0f, 1f);

                biomeMap[x, y] = DetermineBiome(h, t, m);
                heightMap[x, y] = h;
            }
        }

        return (biomeMap, heightMap);
    }

    public static void ApplyBiomeOverrides(Biome[,] biomeMap, BiomeOverride[] overrides)
    {
        int worldWidth = WorldConstants.WorldWidth;
        int worldHeight = WorldConstants.WorldHeight;

        foreach (var ov in overrides)
        {
            int xStart = Mathf.Max(ov.X, 0);
            int yStart = Mathf.Max(ov.Y, 0);
            int xEnd = Mathf.Min(ov.X + ov.W, worldWidth);
            int yEnd = Mathf.Min(ov.Y + ov.H, worldHeight);

            for (int y = yStart; y < yEnd; y++)
            {
                for (int x = xStart; x < xEnd; x++)
                {
                    biomeMap[x, y] = ov.Biome;
                }
            }
        }
    }

    private static Biome DetermineBiome(float h, float t, float m)
    {
        if (h < 0.15f) return Biome.Water;
        if (h > 0.75f) return Biome.Mountain;

        if (t < 0.2f) return Biome.Tundra;
        if (t > 0.7f && m < 0.3f) return Biome.Desert;
        if (m > 0.7f) return Biome.Swamp;
        if (h > 0.5f) return Biome.Hills;
        if (m > 0.4f) return Biome.Forest;

        return Biome.Plains;
    }
}
