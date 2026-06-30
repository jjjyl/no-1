using System;
using System.Collections.Generic;

namespace No1.World;

public static class PathGenerator
{
    private const int WorldWidth = WorldConstants.WorldWidth;
    private const int WorldHeight = WorldConstants.WorldHeight;

    private static readonly (int dx, int dy)[] CardinalDirs =
    {
        (0, 1), (0, -1), (1, 0), (-1, 0)
    };

    // ── Public entry point ──────────────────────────────────────────────────

    public static PathData[] Generate(
        RegionPlacement[] regions,
        Biome[,] biomeMap,
        PathOverride[] overrides = null)
    {
        if (regions == null || regions.Length <= 1)
            return Array.Empty<PathData>();

        if (biomeMap == null)
            throw new ArgumentNullException(nameof(biomeMap));

        overrides ??= Array.Empty<PathOverride>();

        // Step 1 – build minimum spanning tree of regions
        List<(int from, int to)> adjacency = BuildMST(regions);

        // Step 2 & 3 – A* path per edge, respecting waypoint overrides
        var paths = new List<PathData>(adjacency.Count);

        for (int edgeIdx = 0; edgeIdx < adjacency.Count; edgeIdx++)
        {
            (int fromIdx, int toIdx) = adjacency[edgeIdx];

            RegionPlacement fromRegion = regions[fromIdx];
            RegionPlacement toRegion = regions[toIdx];

            // Locate a matching path override (bidirectional match)
            PathOverride? matchingOverride = null;
            for (int ovIdx = 0; ovIdx < overrides.Length; ovIdx++)
            {
                PathOverride ov = overrides[ovIdx];
                if ((ov.FromRegion == fromRegion.Id && ov.ToRegion == toRegion.Id) ||
                    (ov.FromRegion == toRegion.Id && ov.ToRegion == fromRegion.Id))
                {
                    matchingOverride = ov;
                    break;
                }
            }

            List<int> tileIndices;

            if (matchingOverride.HasValue)
            {
                tileIndices = BuildWaypointPath(
                    fromRegion, toRegion, matchingOverride.Value, biomeMap);
            }
            else
            {
                tileIndices = BuildDirectPath(
                    fromRegion.TileX, fromRegion.TileY,
                    toRegion.TileX, toRegion.TileY,
                    biomeMap);
            }

            // Skip edges that have no viable route
            if (tileIndices.Count > 0)
            {
                paths.Add(new PathData
                {
                    TileCount = tileIndices.Count,
                    TileIndices = tileIndices.ToArray()
                });
            }
        }

        return paths.ToArray();
    }

    // ── Step 1: Prim's MST ──────────────────────────────────────────────────

    private static List<(int from, int to)> BuildMST(RegionPlacement[] regions)
    {
        int n = regions.Length;
        var edges = new List<(int from, int to)>(n - 1);

        // Pre-compute squared-distance matrix (no sqrt needed for ordering)
        float[,] dist = new float[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                float dx = regions[i].TileX - regions[j].TileX;
                float dy = regions[i].TileY - regions[j].TileY;
                float d = dx * dx + dy * dy;
                dist[i, j] = d;
                dist[j, i] = d;
            }
        }

        bool[] visited = new bool[n];
        visited[0] = true;

        // Prim: add the closest unvisited region n-1 times
        for (int iter = 1; iter < n; iter++)
        {
            float bestDist = float.MaxValue;
            int bestFrom = 0;
            int bestTo = 0;

            for (int i = 0; i < n; i++)
            {
                if (!visited[i]) continue;

                for (int j = 0; j < n; j++)
                {
                    if (visited[j]) continue;

                    if (dist[i, j] < bestDist)
                    {
                        bestDist = dist[i, j];
                        bestFrom = i;
                        bestTo = j;
                    }
                }
            }

            edges.Add((bestFrom, bestTo));
            visited[bestTo] = true;
        }

        return edges;
    }

    // ── Step 2: direct A* between two region centers ────────────────────────

    private static List<int> BuildDirectPath(
        int startX, int startY, int endX, int endY, Biome[,] biomeMap)
    {
        List<int> core = FindPathCore(startX, startY, endX, endY, biomeMap);
        if (core.Count == 0) return core;
        return WidenPath(core);
    }

    // ── Step 3: waypoint-override path (segmented A*) ──────────────────────

    private static List<int> BuildWaypointPath(
        RegionPlacement from,
        RegionPlacement to,
        PathOverride ov,
        Biome[,] biomeMap)
    {
        var allTiles = new HashSet<int>();

        int prevX = from.TileX;
        int prevY = from.TileY;

        // Route through each waypoint
        for (int wpIdx = 0; wpIdx < ov.Waypoints.Length; wpIdx++)
        {
            (int wx, int wy) = ov.Waypoints[wpIdx];

            List<int> segment = FindPathCore(prevX, prevY, wx, wy, biomeMap);
            if (segment.Count == 0)
                return new List<int>(); // waypoint unreachable → abort entire path

            List<int> widened = WidenPath(segment);
            for (int t = 0; t < widened.Count; t++)
                allTiles.Add(widened[t]);

            prevX = wx;
            prevY = wy;
        }

        // Final segment: last waypoint → destination region center
        List<int> finalSeg = FindPathCore(prevX, prevY, to.TileX, to.TileY, biomeMap);
        if (finalSeg.Count == 0)
            return new List<int>();

        List<int> finalWide = WidenPath(finalSeg);
        for (int t = 0; t < finalWide.Count; t++)
            allTiles.Add(finalWide[t]);

        return new List<int>(allTiles);
    }

    // ── Core A* (returns flat-index list for a single-tile line) ────────────

    private static List<int> FindPathCore(
        int startX, int startY, int endX, int endY, Biome[,] biomeMap)
    {
        int startIdx = FlatIndex(startX, startY);
        int endIdx = FlatIndex(endX, endY);

        if (startIdx == endIdx)
            return new List<int> { startIdx };

        var openSet = new PriorityQueue<int, float>();
        var gScore = new Dictionary<int, float> { [startIdx] = 0f };
        var cameFrom = new Dictionary<int, int>();
        var closedSet = new HashSet<int>();

        openSet.Enqueue(startIdx, Heuristic(startX, startY, endX, endY));

        while (openSet.Count > 0)
        {
            int current = openSet.Dequeue();

            if (current == endIdx)
                return ReconstructPath(cameFrom, current);

            if (!closedSet.Add(current))
                continue; // already visited

            int cx = current % WorldWidth;
            int cy = current / WorldWidth;
            float currentG = gScore[current];

            for (int dirIdx = 0; dirIdx < CardinalDirs.Length; dirIdx++)
            {
                (int dx, int dy) = CardinalDirs[dirIdx];
                int nx = cx + dx;
                int ny = cy + dy;

                if (nx < 0 || nx >= WorldWidth || ny < 0 || ny >= WorldHeight)
                    continue;

                int neighborIdx = FlatIndex(nx, ny);
                if (closedSet.Contains(neighborIdx))
                    continue;

                float moveCost = TileCost(biomeMap[nx, ny]);
                float tentativeG = currentG + moveCost;

                if (tentativeG < gScore.GetValueOrDefault(neighborIdx, float.MaxValue))
                {
                    gScore[neighborIdx] = tentativeG;
                    cameFrom[neighborIdx] = current;
                    float fScore = tentativeG + Heuristic(nx, ny, endX, endY);
                    openSet.Enqueue(neighborIdx, fScore);
                }
            }
        }

        return new List<int>(); // no path
    }

    private static List<int> ReconstructPath(Dictionary<int, int> cameFrom, int current)
    {
        var path = new List<int>();
        path.Add(current);

        while (cameFrom.TryGetValue(current, out int parent))
        {
            current = parent;
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    // ── Path widening (3 tiles wide, perpendicular to travel direction) ─────

    private static List<int> WidenPath(List<int> corePath)
    {
        var result = new HashSet<int>();
        int count = corePath.Count;

        for (int i = 0; i < count; i++)
        {
            int flat = corePath[i];
            int x = flat % WorldWidth;
            int y = flat / WorldWidth;

            // Determine travel direction at this point
            int dx = 0;
            int dy = 0;

            if (i < count - 1)
            {
                int nFlat = corePath[i + 1];
                dx = (nFlat % WorldWidth) - x;
                dy = (nFlat / WorldWidth) - y;
            }
            else if (i > 0)
            {
                int pFlat = corePath[i - 1];
                dx = x - (pFlat % WorldWidth);
                dy = y - (pFlat / WorldWidth);
            }

            // Center tile
            result.Add(flat);

            // Perpendicular wings (-dy, dx) and (dy, -dx)
            if (dx != 0 || dy != 0)
            {
                // Wing 1
                int wx1 = x - dy;
                int wy1 = y + dx;
                if (wx1 >= 0 && wx1 < WorldWidth && wy1 >= 0 && wy1 < WorldHeight)
                    result.Add(FlatIndex(wx1, wy1));

                // Wing 2
                int wx2 = x + dy;
                int wy2 = y - dx;
                if (wx2 >= 0 && wx2 < WorldWidth && wy2 >= 0 && wy2 < WorldHeight)
                    result.Add(FlatIndex(wx2, wy2));
            }
        }

        return new List<int>(result);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static float TileCost(Biome biome)
    {
        return biome switch
        {
            Biome.Water    => 5.0f,
            Biome.Mountain => 2.0f,
            _              => 1.0f
        };
    }

    private static float Heuristic(int x1, int y1, int x2, int y2)
    {
        return Math.Abs(x2 - x1) + Math.Abs(y2 - y1);
    }

    private static int FlatIndex(int x, int y)
    {
        return y * WorldWidth + x;
    }
}
