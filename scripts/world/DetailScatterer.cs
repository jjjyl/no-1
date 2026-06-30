using Godot;
using System;
using System.Collections.Generic;

namespace No1.World;

public static class DetailScatterer
{
    private const int MaxPlacementAttempts = 200;
    private const int HermitMinDistance = 30;
    private const int RuinMinDistance = 20;
    private const int CocoonMinDistance = 60;

    // Biomes valid for hermits
    private static readonly Biome[] HermitBiomes = { Biome.Forest, Biome.Hills, Biome.Plains };

    public static EntitySpawn[] Scatter(
        RegionPlacement[] regions,
        PathData[] paths,
        Biome[,] biomeMap,
        TileData[,] tiles,
        EntityOverride[] entityOverrides = null,
        TileOverride[] tileOverrides = null)
    {
        var spawns = new List<EntitySpawn>();
        int worldWidth = WorldConstants.WorldWidth;
        int worldHeight = WorldConstants.WorldHeight;

        if (regions == null || regions.Length == 0)
        {
            ApplyOverrides(tileOverrides, entityOverrides, tiles, spawns);
            return spawns.ToArray();
        }

        var globalRng = new RandomNumberGenerator();
        globalRng.Seed = DeriveGlobalSeed(regions);

        PlaceEnemies(regions, biomeMap, tiles, spawns, worldWidth, worldHeight);
        PlaceNPCs(regions, biomeMap, tiles, spawns, worldWidth, worldHeight, globalRng);
        PlaceFragments(regions, biomeMap, tiles, spawns, worldWidth, worldHeight, globalRng);
        PlaceRuinEntrances(regions, biomeMap, tiles, spawns, worldWidth, worldHeight, globalRng);
        PlaceTimeCocoon(regions, biomeMap, tiles, spawns, worldWidth, worldHeight, globalRng);
        ApplyOverrides(tileOverrides, entityOverrides, tiles, spawns);

        return spawns.ToArray();
    }

    // ── Seed derivation ────────────────────────────────────────────────────

    private static ulong DeriveGlobalSeed(RegionPlacement[] regions)
    {
        ulong seed = 0;
        for (int i = 0; i < regions.Length; i++)
        {
            seed ^= (ulong)(regions[i].TileX * 10000 + regions[i].TileY * 10 + i * 7 + 137);
        }
        return seed != 0 ? seed : 12345;
    }

    // ── Enemy placement ────────────────────────────────────────────────────

    private static void PlaceEnemies(
        RegionPlacement[] regions,
        Biome[,] biomeMap,
        TileData[,] tiles,
        List<EntitySpawn> spawns,
        int worldWidth,
        int worldHeight)
    {
        for (int i = 0; i < regions.Length; i++)
        {
            var region = regions[i];
            var rng = new RandomNumberGenerator();
            rng.Seed = (ulong)(region.TileX * 10000 + region.TileY * 10 + 42);

            int count = rng.RandiRange(2, 5);
            for (int j = 0; j < count; j++)
            {
                int dist = rng.RandiRange(3, 8);
                float angle = rng.RandfRange(0f, Mathf.Pi * 2f);
                int tx = region.TileX + (int)Math.Round(Math.Cos(angle) * dist);
                int ty = region.TileY + (int)Math.Round(Math.Sin(angle) * dist);

                if (!InBounds(tx, ty, worldWidth, worldHeight))
                    continue;
                if (IsWater(tx, ty, biomeMap))
                    continue;

                var tile = tiles[tx, ty];
                if (tile.HasEnemy)
                    continue;

                tile.HasEnemy = true;
                tile.Modified = true;
                tiles[tx, ty] = tile;

                spawns.Add(new EntitySpawn
                {
                    Type = EntityType.Enemy,
                    Id = "base_mob",
                    TileX = tx,
                    TileY = ty,
                    State = 0
                });
            }
        }
    }

    // ── NPC placement ──────────────────────────────────────────────────────

    private static void PlaceNPCs(
        RegionPlacement[] regions,
        Biome[,] biomeMap,
        TileData[,] tiles,
        List<EntitySpawn> spawns,
        int worldWidth,
        int worldHeight,
        RandomNumberGenerator rng)
    {
        PlaceHermits(regions, biomeMap, tiles, spawns, worldWidth, worldHeight, rng);
        PlaceMerchant(regions, biomeMap, tiles, spawns, worldWidth, worldHeight, rng);
    }

    private static void PlaceHermits(
        RegionPlacement[] regions,
        Biome[,] biomeMap,
        TileData[,] tiles,
        List<EntitySpawn> spawns,
        int worldWidth,
        int worldHeight,
        RandomNumberGenerator rng)
    {
        int hermitCount = rng.RandiRange(5, 15);
        int placed = 0;

        for (int attempt = 0; attempt < MaxPlacementAttempts && placed < hermitCount; attempt++)
        {
            int tx = rng.RandiRange(0, worldWidth - 1);
            int ty = rng.RandiRange(0, worldHeight - 1);

            if (MinDistanceToRegions(tx, ty, regions) < HermitMinDistance)
                continue;
            if (!IsHermitBiome(tx, ty, biomeMap))
                continue;
            if (IsWater(tx, ty, biomeMap))
                continue;

            var tile = tiles[tx, ty];
            if (tile.HasNPC)
                continue;

            tile.HasNPC = true;
            tile.Modified = true;
            tiles[tx, ty] = tile;

            spawns.Add(new EntitySpawn
            {
                Type = EntityType.NPC,
                Id = "hermit",
                TileX = tx,
                TileY = ty,
                State = 0
            });
            placed++;
        }
    }

    private static void PlaceMerchant(
        RegionPlacement[] regions,
        Biome[,] biomeMap,
        TileData[,] tiles,
        List<EntitySpawn> spawns,
        int worldWidth,
        int worldHeight,
        RandomNumberGenerator rng)
    {
        // Place near first region center (within 5 tiles)
        var region = regions[0];

        for (int attempt = 0; attempt < MaxPlacementAttempts; attempt++)
        {
            int offsetX = rng.RandiRange(-5, 5);
            int offsetY = rng.RandiRange(-5, 5);
            int tx = region.TileX + offsetX;
            int ty = region.TileY + offsetY;

            if (!InBounds(tx, ty, worldWidth, worldHeight))
                continue;
            if (IsWater(tx, ty, biomeMap))
                continue;

            var tile = tiles[tx, ty];
            if (tile.HasNPC)
                continue;

            tile.HasNPC = true;
            tile.Modified = true;
            tiles[tx, ty] = tile;

            spawns.Add(new EntitySpawn
            {
                Type = EntityType.NPC,
                Id = "merchant",
                TileX = tx,
                TileY = ty,
                State = 0
            });
            return;
        }
    }

    // ── Fragment placement ─────────────────────────────────────────────────

    private static void PlaceFragments(
        RegionPlacement[] regions,
        Biome[,] biomeMap,
        TileData[,] tiles,
        List<EntitySpawn> spawns,
        int worldWidth,
        int worldHeight,
        RandomNumberGenerator rng)
    {
        int fragmentCount = rng.RandiRange(3, 8);
        int placed = 0;

        for (int attempt = 0; attempt < MaxPlacementAttempts && placed < fragmentCount; attempt++)
        {
            // Pick a random region
            int ri = rng.RandiRange(0, regions.Length - 1);
            var region = regions[ri];

            int dist = rng.RandiRange(5, 12);
            float angle = rng.RandfRange(0f, Mathf.Pi * 2f);
            int tx = region.TileX + (int)Math.Round(Math.Cos(angle) * dist);
            int ty = region.TileY + (int)Math.Round(Math.Sin(angle) * dist);

            if (!InBounds(tx, ty, worldWidth, worldHeight))
                continue;
            if (IsWater(tx, ty, biomeMap))
                continue;

            var tile = tiles[tx, ty];
            if (tile.HasFragment)
                continue;

            tile.HasFragment = true;
            tile.Modified = true;
            tiles[tx, ty] = tile;

            spawns.Add(new EntitySpawn
            {
                Type = EntityType.Fragment,
                Id = "fragment",
                TileX = tx,
                TileY = ty,
                State = 0
            });
            placed++;
        }
    }

    // ── Ruin entrances ─────────────────────────────────────────────────────

    private static void PlaceRuinEntrances(
        RegionPlacement[] regions,
        Biome[,] biomeMap,
        TileData[,] tiles,
        List<EntitySpawn> spawns,
        int worldWidth,
        int worldHeight,
        RandomNumberGenerator rng)
    {
        int ruinCount = rng.RandiRange(1, 3);
        int placed = 0;

        for (int attempt = 0; attempt < MaxPlacementAttempts && placed < ruinCount; attempt++)
        {
            int tx = rng.RandiRange(0, worldWidth - 1);
            int ty = rng.RandiRange(0, worldHeight - 1);

            if (MinDistanceToRegions(tx, ty, regions) < RuinMinDistance)
                continue;

            Biome biome = biomeMap[tx, ty];
            if (biome != Biome.Mountain && biome != Biome.Hills)
                continue;
            if (IsWater(tx, ty, biomeMap))
                continue;

            var tile = tiles[tx, ty];

            // Set tile to Dirt if on Mountain (visual variety)
            if (biome == Biome.Mountain)
            {
                tile.Type = TileType.Dirt;
            }

            tile.Modified = true;
            tiles[tx, ty] = tile;

            spawns.Add(new EntitySpawn
            {
                Type = EntityType.Enemy,
                Id = "ruin_entrance",
                TileX = tx,
                TileY = ty,
                State = 0
            });
            placed++;
        }
    }

    // ── Time cocoon ────────────────────────────────────────────────────────

    private static void PlaceTimeCocoon(
        RegionPlacement[] regions,
        Biome[,] biomeMap,
        TileData[,] tiles,
        List<EntitySpawn> spawns,
        int worldWidth,
        int worldHeight,
        RandomNumberGenerator rng)
    {
        // 30% chance
        if (rng.RandfRange(0f, 1f) > 0.3f)
            return;

        for (int attempt = 0; attempt < MaxPlacementAttempts; attempt++)
        {
            int tx = rng.RandiRange(0, worldWidth - 1);
            int ty = rng.RandiRange(0, worldHeight - 1);

            if (MinDistanceToRegions(tx, ty, regions) < CocoonMinDistance)
                continue;
            if (IsWater(tx, ty, biomeMap))
                continue;

            var tile = tiles[tx, ty];
            tile.Modified = true;
            tiles[tx, ty] = tile;

            spawns.Add(new EntitySpawn
            {
                Type = EntityType.Enemy,
                Id = "time_cocoon",
                TileX = tx,
                TileY = ty,
                State = 0
            });
            return;
        }
    }

    // ── Override application ───────────────────────────────────────────────

    private static void ApplyOverrides(
        TileOverride[] tileOverrides,
        EntityOverride[] entityOverrides,
        TileData[,] tiles,
        List<EntitySpawn> spawns)
    {
        if (entityOverrides != null)
        {
            foreach (var ov in entityOverrides)
            {
                int tx = ov.X;
                int ty = ov.Y;

                var tile = tiles[tx, ty];
                switch (ov.Type)
                {
                    case EntityType.Enemy:
                        tile.HasEnemy = true;
                        break;
                    case EntityType.NPC:
                        tile.HasNPC = true;
                        break;
                    case EntityType.Fragment:
                        tile.HasFragment = true;
                        break;
                }
                tile.Modified = true;
                tiles[tx, ty] = tile;

                spawns.Add(new EntitySpawn
                {
                    Type = ov.Type,
                    Id = ov.Id,
                    TileX = tx,
                    TileY = ty,
                    State = 0
                });
            }
        }

        if (tileOverrides != null)
        {
            foreach (var ov in tileOverrides)
            {
                var tile = tiles[ov.X, ov.Y];
                tile.Type = ov.Type;
                tile.Modified = true;
                tiles[ov.X, ov.Y] = tile;
            }
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static bool InBounds(int x, int y, int worldWidth, int worldHeight)
    {
        return x >= 0 && x < worldWidth && y >= 0 && y < worldHeight;
    }

    private static bool IsWater(int x, int y, Biome[,] biomeMap)
    {
        return biomeMap[x, y] == Biome.Water;
    }

    private static bool IsHermitBiome(int x, int y, Biome[,] biomeMap)
    {
        Biome b = biomeMap[x, y];
        for (int i = 0; i < HermitBiomes.Length; i++)
        {
            if (b == HermitBiomes[i])
                return true;
        }
        return false;
    }

    private static float MinDistanceToRegions(int x, int y, RegionPlacement[] regions)
    {
        float minDist = float.MaxValue;
        for (int i = 0; i < regions.Length; i++)
        {
            int dx = x - regions[i].TileX;
            int dy = y - regions[i].TileY;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (dist < minDist)
                minDist = dist;
        }
        return minDist;
    }
}
