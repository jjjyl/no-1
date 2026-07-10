namespace No1.World;

using System;

/// <summary>
/// Dual-grid material evaluator. Given the 4 source tiles surrounding a dual-grid quad,
/// determines the canonical material key for that quad (pure or transition).
/// </summary>
public static class DualGridEvaluator
{
    /// <summary>
    /// Returns a canonical material key for a dual-grid quad bounded by 4 source tiles.
    /// Layout:
    ///   tl — tr
    ///   |    |
    ///   bl — br
    /// </summary>
    /// <returns>Pure key like "grass" or transition key like "dirt_grass" (alphabetically sorted).</returns>
    public static string GetMaterialKey(TileType tl, TileType tr, TileType bl, TileType br)
    {
        int a = Count(tl, tr, bl, br, tl);
        int b = Count(tl, tr, bl, br, tr);
        int c = Count(tl, tr, bl, br, bl);
        int d = Count(tl, tr, bl, br, br);

        // 4/4 or 3/1 → majority type wins
        if (a >= 3) return TileKey(tl);
        if (b >= 3) return TileKey(tr);
        if (c >= 3) return TileKey(bl);
        if (d >= 3) return TileKey(br);

        // 2/2 or scattered → return the transition of the two most common types
        var t1 = tl;
        var t2 = tl != tr ? tr : tl != bl ? bl : br;
        int c1 = Count(tl, tr, bl, br, t1);
        int c2 = Count(tl, tr, bl, br, t2);

        // If 2/2, return transition key
        if (c1 == 2 && c2 == 2)
            return TransitionKey(t1, t2);

        // Fallback: all 4 different or 2/1/1 → majority
        if (c1 >= 2) return TileKey(t1);
        return TileKey(tl);
    }

    /// <summary>Canonical pure material key (lowercase TileType name).</summary>
    public static string TileKey(TileType type) => type.ToString().ToLowerInvariant();

    /// <summary>Canonical transition material key (sorted alphabetically, joined by underscore).</summary>
    public static string TransitionKey(TileType a, TileType b)
    {
        var sa = a.ToString().ToLowerInvariant();
        var sb = b.ToString().ToLowerInvariant();
        return string.CompareOrdinal(sa, sb) <= 0
            ? $"{sa}_{sb}"
            : $"{sb}_{sa}";
    }

    private static int Count(TileType a, TileType b, TileType c, TileType d, TileType target)
    {
        int n = 0;
        if (a == target) n++;
        if (b == target) n++;
        if (c == target) n++;
        if (d == target) n++;
        return n;
    }
}
