namespace No1.World;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public class RegionDef
{
    public string Id;
    public string Name;
    public string RequiredBiome;
    public bool IsAnchor;
    public int AvgRadius;
    public int MinCycle;

    public static List<RegionDef> LoadPool()
    {
        var list = new List<RegionDef>();

        using var file = FileAccess.Open("res://assets/data/regions.json", FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PrintErr("RegionPlacer: Failed to open regions.json");
            return list;
        }

        string text = file.GetAsText();
        var json = new Json();
        Error error = json.Parse(text);
        if (error != Error.Ok)
        {
            GD.PrintErr($"RegionPlacer: JSON parse error: {error}");
            return list;
        }

        var data = json.Data.AsGodotDictionary();
        var regions = data["regions"].AsGodotArray();

        foreach (var item in regions)
        {
            var r = item.AsGodotDictionary();
            list.Add(new RegionDef
            {
                Id = r["id"].AsString(),
                Name = r["name"].AsString(),
                RequiredBiome = r["required_biome"].AsString(),
                IsAnchor = (bool)r["is_anchor"],
                AvgRadius = r["avg_radius"].AsInt32(),
                MinCycle = r["min_cycle"].AsInt32()
            });
        }

        return list;
    }
}

public static class RegionPlacer
{
    private static float Dist(int x1, int y1, int x2, int y2)
    {
        int dx = x1 - x2;
        int dy = y1 - y2;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Place regions on the biome map for a given cycle.
    /// Anchors are always placed. Non-anchors are randomly selected based on cycle progression.
    /// Overrides force specific coordinates.
    /// </summary>
    public static RegionPlacement[] Place(Biome[,] biomeMap, int cycleNumber, RegionOverride[] overrides = null)
    {
        int mapWidth = biomeMap.GetLength(0);
        int mapHeight = biomeMap.GetLength(1);

        var pool = RegionDef.LoadPool();
        if (pool.Count == 0)
            return Array.Empty<RegionPlacement>();

        var filtered = pool.Where(r => cycleNumber >= r.MinCycle).ToList();

        var anchors = filtered.Where(r => r.IsAnchor).ToList();
        var nonAnchors = filtered.Where(r => !r.IsAnchor).ToList();

        float ratio = Mathf.Clamp(cycleNumber * 0.2f + 0.5f, 0.3f, 0.9f);
        var rng = new Random((int)(cycleNumber * 7919));

        // Fisher-Yates shuffle non-anchors for deterministic selection
        for (int i = nonAnchors.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (nonAnchors[i], nonAnchors[j]) = (nonAnchors[j], nonAnchors[i]);
        }

        int selectCount = Math.Max(1, (int)(nonAnchors.Count * ratio));
        var selectedNonAnchors = nonAnchors.Take(selectCount).ToList();

        var placedRegions = new List<RegionPlacement>();

        // Remove overridden regions from automatic pools
        var overrideLookup = new Dictionary<string, RegionOverride>();
        if (overrides != null)
        {
            foreach (var ov in overrides)
            {
                overrideLookup[ov.Id] = ov;
            }
            anchors.RemoveAll(r => overrideLookup.ContainsKey(r.Id));
            selectedNonAnchors.RemoveAll(r => overrideLookup.ContainsKey(r.Id));
        }

        // Place anchors first, then non-anchors
        foreach (var region in anchors)
            PlaceRegionGreedy(region, biomeMap, mapWidth, mapHeight, placedRegions);

        foreach (var region in selectedNonAnchors)
            PlaceRegionGreedy(region, biomeMap, mapWidth, mapHeight, placedRegions);

        // Apply overrides: force-position, overwrite existing if same Id
        foreach (var kvp in overrideLookup)
        {
            var ov = kvp.Value;
            var def = pool.FirstOrDefault(r => r.Id == ov.Id);
            string name = def?.Name ?? ov.Id;

            placedRegions.RemoveAll(p => p.Id == ov.Id);

            placedRegions.Add(new RegionPlacement
            {
                Id = ov.Id,
                Name = name,
                TileX = ov.X,
                TileY = ov.Y,
                Radius = ov.Radius > 0 ? ov.Radius : (def?.AvgRadius ?? 10)
            });
        }

        return placedRegions.ToArray();
    }

    private static void PlaceRegionGreedy(
        RegionDef region,
        Biome[,] biomeMap,
        int mapWidth,
        int mapHeight,
        List<RegionPlacement> placedRegions)
    {
        if (!Enum.TryParse(region.RequiredBiome, ignoreCase: true, out Biome requiredBiome))
        {
            GD.PrintErr($"RegionPlacer: Unknown biome '{region.RequiredBiome}' for region '{region.Id}'");
            return;
        }

        const int scanStep = 8;
        int radius = region.AvgRadius;

        int bestX = -1;
        int bestY = -1;
        float bestScore = float.MinValue;

        int margin = radius + 8;
        int startX = Math.Max(margin, 0);
        int startY = Math.Max(margin, 0);
        int endX = Math.Min(mapWidth - margin, mapWidth);
        int endY = Math.Min(mapHeight - margin, mapHeight);

        for (int y = startY; y < endY; y += scanStep)
        {
            for (int x = startX; x < endX; x += scanStep)
            {
                if (biomeMap[x, y] != requiredBiome)
                    continue;

                // Check overlap with already-placed regions
                bool tooClose = false;
                foreach (var placed in placedRegions)
                {
                    float d = Dist(x, y, placed.TileX, placed.TileY);
                    float minDist = (radius + placed.Radius) * 1.5f;
                    if (d < minDist)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose)
                    continue;

                // Score: count matching biome tiles within radius window
                int matchingTiles = 0;
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int sx = x + dx;
                        int sy = y + dy;
                        if (sx >= 0 && sx < mapWidth && sy >= 0 && sy < mapHeight
                            && biomeMap[sx, sy] == requiredBiome)
                        {
                            matchingTiles++;
                        }
                    }
                }

                // Distance bonus: prefer positions farther from already-placed regions
                float score = matchingTiles;
                foreach (var placed in placedRegions)
                {
                    float d = Dist(x, y, placed.TileX, placed.TileY);
                    score += d * 0.1f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestX = x;
                    bestY = y;
                }
            }
        }

        if (bestX < 0)
        {
            GD.PrintErr($"RegionPlacer: No valid position found for region '{region.Id}' (biome: {region.RequiredBiome})");
            return;
        }

        placedRegions.Add(new RegionPlacement
        {
            Id = region.Id,
            Name = region.Name,
            TileX = bestX,
            TileY = bestY,
            Radius = radius
        });
    }
}
