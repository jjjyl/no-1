using Godot;
using System;
using System.Collections.Generic;

namespace No1.World;

public static class WorldGenerator
{
    // ── Public entry point ──────────────────────────────────────────────────

    public static WorldData Generate(ulong seed, int cycleNumber, string[] overridePaths = null)
    {
        // 1. Generate terrain
        var (biomeMap, heightMap) = TerrainGenerator.Generate(seed);

        ulong effectiveSeed = seed;

        // 2. Load and apply overrides
        OverlayData overlay = null;
        if (overridePaths != null && overridePaths.Length > 0)
        {
            overlay = MergeOverlays(overridePaths);
            if (overlay != null)
            {
                if (overlay.BaseSeed != 0)
                {
                    effectiveSeed = overlay.BaseSeed;

                    // Re-generate terrain with overridden seed so that
                    // "same seed = same world" holds.
                    (biomeMap, heightMap) = TerrainGenerator.Generate(effectiveSeed);
                }

                if (overlay.BiomeOverrides != null && overlay.BiomeOverrides.Length > 0)
                    TerrainGenerator.ApplyBiomeOverrides(biomeMap, overlay.BiomeOverrides);
            }
        }

        // 3. Place regions
        var regions = RegionPlacer.Place(biomeMap, cycleNumber, overlay?.RegionOverrides);

        // 4. Generate paths
        var paths = PathGenerator.Generate(regions, biomeMap, overlay?.PathOverrides);

        // 5. Scatter details
        TileData[,] tiles = CreateTileArray(biomeMap, heightMap);
        var entities = DetailScatterer.Scatter(regions, paths, biomeMap, tiles,
            overlay?.EntityOverrides, overlay?.TileOverrides);

        // 6. Assemble WorldData
        return AssembleWorldData(effectiveSeed, cycleNumber, regions, paths, tiles, entities);
    }

    // ── Biome → TileType ────────────────────────────────────────────────────

    private static TileType BiomeToTileType(Biome b)
    {
        return b switch
        {
            Biome.Water    => TileType.Water,
            Biome.Mountain => TileType.Rock,
            Biome.Snow     => TileType.Snow,
            Biome.Desert   => TileType.Sand,
            Biome.Swamp    => TileType.Swamp,
            Biome.Forest   => TileType.Grass,
            Biome.Plains   => TileType.Grass,
            Biome.Tundra   => TileType.Snow,
            Biome.Hills    => TileType.Rock,
            _              => TileType.Grass
        };
    }

    // ── Passability ─────────────────────────────────────────────────────────

    private static bool PassableForType(TileType t)
    {
        return t != TileType.Water;
    }

    // ── Create tile array ───────────────────────────────────────────────────

    private static TileData[,] CreateTileArray(Biome[,] biomeMap, float[,] heightMap)
    {
        int width = WorldConstants.WorldWidth;
        int height = WorldConstants.WorldHeight;
        var tiles = new TileData[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                TileType type = BiomeToTileType(biomeMap[x, y]);
                tiles[x, y] = new TileData
                {
                    Type = type,
                    Height = (byte)(heightMap[x, y] * 255f),
                    Passable = PassableForType(type)
                };
            }
        }

        return tiles;
    }

    // ── Assemble WorldData into chunks ──────────────────────────────────────

    private static WorldData AssembleWorldData(
        ulong seed,
        int cycleNumber,
        RegionPlacement[] regions,
        PathData[] paths,
        TileData[,] tiles,
        EntitySpawn[] entities)
    {
        var world = new WorldData
        {
            Seed = seed,
            CycleNumber = cycleNumber,
            Regions = regions,
            Paths = paths,
            Chunks = new ChunkData[WorldConstants.TotalChunks]
        };

        int chunkDim = WorldConstants.ChunkDim;
        int chunksX = WorldConstants.ChunksX;
        int worldWidth = WorldConstants.WorldWidth;
        int worldHeight = WorldConstants.WorldHeight;

        for (int cy = 0; cy < WorldConstants.ChunksY; cy++)
        {
            for (int cx = 0; cx < chunksX; cx++)
            {
                int chunkIdx = cy * chunksX + cx;
                var chunk = new ChunkData { X = cx, Y = cy };

                int startX = cx * chunkDim;
                int startY = cy * chunkDim;
                int endX = Math.Min(startX + chunkDim, worldWidth);
                int endY = Math.Min(startY + chunkDim, worldHeight);

                for (int ly = 0; ly < chunkDim; ly++)
                {
                    int wy = startY + ly;
                    if (wy >= worldHeight) break;

                    for (int lx = 0; lx < chunkDim; lx++)
                    {
                        int wx = startX + lx;
                        if (wx >= worldWidth) break;

                        chunk.Tiles[ly * chunkDim + lx] = tiles[wx, wy];
                    }
                }

                // Collect entities whose tile coords fall within this chunk
                for (int i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    int ecx = e.TileX / chunkDim;
                    int ecy = e.TileY / chunkDim;
                    if (ecx == cx && ecy == cy)
                        chunk.Entities.Add(e);
                }

                world.Chunks[chunkIdx] = chunk;
            }
        }

        return world;
    }

    // ── Overlay merging ─────────────────────────────────────────────────────

    private static OverlayData MergeOverlays(string[] paths)
    {
        if (paths == null || paths.Length == 0)
            return null;

        string mapName = null;
        string description = null;
        ulong baseSeed = 0ul;
        var tileDict = new Dictionary<(int x, int y), TileOverride>();
        var regionDict = new Dictionary<string, RegionOverride>();
        var pathDict = new Dictionary<(string from, string to), PathOverride>();
        var entityList = new List<EntityOverride>();
        var biomeList = new List<BiomeOverride>();

        foreach (string path in paths)
        {
            if (string.IsNullOrEmpty(path))
                continue;

            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr($"WorldGenerator: Failed to open overlay file: {path}");
                continue;
            }

            string text = file.GetAsText();
            var json = new Json();
            Error error = json.Parse(text);
            if (error != Error.Ok)
            {
                GD.PrintErr($"WorldGenerator: JSON parse error in {path}: {error}");
                continue;
            }

            var data = json.Data.AsGodotDictionary();

            // Top-level string / scalar fields — last file wins
            if (data.ContainsKey("map_name"))
                mapName = data["map_name"].AsString();
            if (data.ContainsKey("description"))
                description = data["description"].AsString();
            if (data.ContainsKey("base_seed"))
                baseSeed = data["base_seed"].AsUInt64();

            // Tile overrides — keyed by (x, y), last wins
            if (data.ContainsKey("tile_overrides"))
                ParseTileOverrides(data["tile_overrides"].AsGodotArray(), tileDict);

            // Region overrides — keyed by Id, last wins
            if (data.ContainsKey("region_overrides"))
                ParseRegionOverrides(data["region_overrides"].AsGodotArray(), regionDict);

            // Path overrides — keyed by unordered (from, to) pair, last wins
            if (data.ContainsKey("path_overrides"))
                ParsePathOverrides(data["path_overrides"].AsGodotArray(), pathDict);

            // Entity overrides — just append
            if (data.ContainsKey("entity_overrides"))
                ParseEntityOverrides(data["entity_overrides"].AsGodotArray(), entityList);

            // Biome overrides — just append (later ones overwrite earlier during Apply)
            if (data.ContainsKey("biome_overrides"))
                ParseBiomeOverrides(data["biome_overrides"].AsGodotArray(), biomeList);
        }

        return new OverlayData
        {
            MapName = mapName,
            Description = description,
            BaseSeed = baseSeed,
            TileOverrides = new List<TileOverride>(tileDict.Values).ToArray(),
            RegionOverrides = new List<RegionOverride>(regionDict.Values).ToArray(),
            PathOverrides = new List<PathOverride>(pathDict.Values).ToArray(),
            EntityOverrides = entityList.ToArray(),
            BiomeOverrides = biomeList.ToArray()
        };
    }

    // ── JSON field parsers ──────────────────────────────────────────────────

    private static void ParseTileOverrides(
        Godot.Collections.Array arr,
        Dictionary<(int x, int y), TileOverride> dict)
    {
        foreach (var item in arr)
        {
            var d = item.AsGodotDictionary();
            int x = d["x"].AsInt32();
            int y = d["y"].AsInt32();
            dict[(x, y)] = new TileOverride
            {
                X = x,
                Y = y,
                Type = ParseEnum<TileType>(d["type"].AsString())
            };
        }
    }

    private static void ParseRegionOverrides(
        Godot.Collections.Array arr,
        Dictionary<string, RegionOverride> dict)
    {
        foreach (var item in arr)
        {
            var d = item.AsGodotDictionary();
            string id = d["id"].AsString();
            dict[id] = new RegionOverride
            {
                Id = id,
                X = d["x"].AsInt32(),
                Y = d["y"].AsInt32(),
                Radius = d["radius"].AsInt32()
            };
        }
    }

    private static void ParsePathOverrides(
        Godot.Collections.Array arr,
        Dictionary<(string from, string to), PathOverride> dict)
    {
        foreach (var item in arr)
        {
            var d = item.AsGodotDictionary();
            string from = d["from_region"].AsString();
            string to = d["to_region"].AsString();
            var waypoints = ParseWaypoints(d["waypoints"].AsGodotArray());

            // Bidirectional key so that either ordering matches
            var key = string.CompareOrdinal(from, to) < 0 ? (from, to) : (to, from);
            dict[key] = new PathOverride
            {
                FromRegion = from,
                ToRegion = to,
                Waypoints = waypoints
            };
        }
    }

    private static void ParseEntityOverrides(
        Godot.Collections.Array arr,
        List<EntityOverride> list)
    {
        foreach (var item in arr)
        {
            var d = item.AsGodotDictionary();
            list.Add(new EntityOverride
            {
                Type = ParseEnum<EntityType>(d["type"].AsString()),
                Id = d["id"].AsString(),
                X = d["x"].AsInt32(),
                Y = d["y"].AsInt32()
            });
        }
    }

    private static void ParseBiomeOverrides(
        Godot.Collections.Array arr,
        List<BiomeOverride> list)
    {
        foreach (var item in arr)
        {
            var d = item.AsGodotDictionary();
            list.Add(new BiomeOverride
            {
                X = d["x"].AsInt32(),
                Y = d["y"].AsInt32(),
                W = d["w"].AsInt32(),
                H = d["h"].AsInt32(),
                Biome = ParseEnum<Biome>(d["biome"].AsString())
            });
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static (int x, int y)[] ParseWaypoints(Godot.Collections.Array arr)
    {
        var waypoints = new (int x, int y)[arr.Count];
        for (int i = 0; i < arr.Count; i++)
        {
            var wp = arr[i].AsGodotDictionary();
            waypoints[i] = (wp["x"].AsInt32(), wp["y"].AsInt32());
        }
        return waypoints;
    }

    private static T ParseEnum<T>(string value) where T : struct, Enum
    {
        if (Enum.TryParse(value, ignoreCase: true, out T result))
            return result;
        GD.PrintErr($"WorldGenerator: Failed to parse '{value}' as {typeof(T).Name}");
        return default;
    }
}
