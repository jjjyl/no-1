using Godot;
using System.Collections.Generic;

namespace No1.World;

// ── Enums (byte-backed for binary storage) ────────────────────────────────

public enum TileType : byte
{
    Grass,
    Dirt,
    Water,
    Rock,
    Snow,
    Sand,
    Swamp,
    Path
}

public enum Biome : byte
{
    Tundra,
    Snow,
    Plains,
    Forest,
    Swamp,
    Desert,
    Hills,
    Mountain,
    Water
}

public enum EntityType : byte
{
    Enemy,
    NPC,
    Fragment
}

// ── TileData (2 bytes storage) ────────────────────────────────────────────

public struct TileData
{
    public TileType Type;
    public byte Height;     // 0-255 maps to 0.0-1.0
    public bool Passable;
    public bool HasEnemy;
    public bool HasNPC;
    public bool HasFragment;
    public bool Modified;

    /// <summary>
    /// Packs into ushort.
    /// Bit layout:
    ///   bits  7-0  = Height
    ///   bit   8    = Passable
    ///   bit   9    = HasEnemy
    ///   bit   10   = HasNPC
    ///   bit   11   = HasFragment
    ///   bit   12   = Modified
    ///   bits 15-13 = Type
    /// </summary>
    public ushort ToU16()
    {
        ushort value = Height;

        if (Passable)   value |= 1 << 8;
        if (HasEnemy)   value |= 1 << 9;
        if (HasNPC)     value |= 1 << 10;
        if (HasFragment) value |= 1 << 11;
        if (Modified)   value |= 1 << 12;

        value |= (ushort)((byte)Type << 13);
        return value;
    }

    public static TileData FromU16(ushort value)
    {
        TileData tile = default;

        tile.Height      = (byte)(value & 0xFF);
        tile.Passable    = (value & (1 << 8)) != 0;
        tile.HasEnemy    = (value & (1 << 9)) != 0;
        tile.HasNPC      = (value & (1 << 10)) != 0;
        tile.HasFragment = (value & (1 << 11)) != 0;
        tile.Modified    = (value & (1 << 12)) != 0;
        tile.Type        = (TileType)((value >> 13) & 0x7);

        return tile;
    }
}

// ── ChunkData ─────────────────────────────────────────────────────────────

public class ChunkData
{
    public int X;
    public int Y;
    public TileData[] Tiles;           // 16384 elements (128x128)
    public List<EntitySpawn> Entities;
    public bool IsGenerated;
    public bool IsLoaded;
    public Node3D SceneNode;
    public MeshInstance3D GroundMesh;

    public ChunkData()
    {
        Tiles = new TileData[WorldConstants.TilesPerChunk];
        Entities = new List<EntitySpawn>();
    }
}

// ── WorldData ─────────────────────────────────────────────────────────────

public class WorldData
{
    public ulong Seed;
    public int CycleNumber;
    public int Width = 1000;
    public int Height = 800;
    public int ChunkSize = 128;
    public ChunkData[] Chunks;
    public RegionPlacement[] Regions;
    public PathData[] Paths;

    private int ChunksX => (Width + ChunkSize - 1) / ChunkSize;

    public ChunkData GetChunk(int cx, int cy)
    {
        return Chunks[cy * ChunksX + cx];
    }

    public TileData GetTile(int x, int y)
    {
        int cx = x / ChunkSize;
        int cy = y / ChunkSize;
        int localX = x % ChunkSize;
        int localY = y % ChunkSize;

        ChunkData chunk = GetChunk(cx, cy);
        return chunk.Tiles[localY * ChunkSize + localX];
    }
}

// ── RegionPlacement ───────────────────────────────────────────────────────

public struct RegionPlacement
{
    public string Id;
    public string Name;
    public int TileX;
    public int TileY;
    public int Radius;
}

// ── PathData ──────────────────────────────────────────────────────────────

public struct PathData
{
    public int TileCount;
    public int[] TileIndices;   // flat indices into the 1000x800 grid
}

// ── EntitySpawn ───────────────────────────────────────────────────────────

public struct EntitySpawn
{
    public EntityType Type;
    public string Id;
    public int TileX;
    public int TileY;
    // 0 = alive / present, 1 = dead / collected
    public byte State;
}

// ── OverlayData ───────────────────────────────────────────────────────────

public class OverlayData
{
    public string MapName;
    public string Description;
    public ulong BaseSeed;
    public TileOverride[] TileOverrides;
    public RegionOverride[] RegionOverrides;
    public PathOverride[] PathOverrides;
    public EntityOverride[] EntityOverrides;
    public BiomeOverride[] BiomeOverrides;
}

// ── Override structs ──────────────────────────────────────────────────────

public struct TileOverride
{
    public int X;
    public int Y;
    public TileType Type;
}

public struct RegionOverride
{
    public string Id;
    public int X;
    public int Y;
    public int Radius;
}

public struct PathOverride
{
    public string FromRegion;
    public string ToRegion;
    public (int x, int y)[] Waypoints;
}

public struct EntityOverride
{
    public EntityType Type;
    public string Id;
    public int X;
    public int Y;
}

public struct BiomeOverride
{
    public int X;
    public int Y;
    public int W;
    public int H;
    public Biome Biome;
}

// ── WorldConstants ────────────────────────────────────────────────────────

public static class WorldConstants
{
    // TODO: set false for full world after testing
    public const bool DebugSmall = true;

    public const int WorldWidth  = DebugSmall ? 384 : 1000;
    public const int WorldHeight = DebugSmall ? 256 : 800;
    public const int ChunkDim    = DebugSmall ? 64  : 128;
    public static readonly int ChunksX = (WorldWidth  + ChunkDim - 1) / ChunkDim;
    public static readonly int ChunksY = (WorldHeight + ChunkDim - 1) / ChunkDim;
    public static readonly int TotalChunks = ChunksX * ChunksY;
    public static readonly int TilesPerChunk = ChunkDim * ChunkDim;
    public const float TileSizeMeters = 0.5f;
}

// ── Utility methods ───────────────────────────────────────────────────────

public static class WorldUtils
{
    /// <summary>
    /// Converts tile coordinates to a flat index (row-major: y * width + x).
    /// </summary>
    public static int TileIndex(int x, int y, int width = 1000)
    {
        return y * width + x;
    }

    /// <summary>
    /// Converts chunk coordinates to a flat chunk array index.
    /// </summary>
    public static int ChunkIndex(int cx, int cy)
    {
        return cy * WorldConstants.ChunksX + cx;
    }

    /// <summary>
    /// Returns the chunk coordinates that contain a given tile.
    /// </summary>
    public static (int cx, int cy) TileToChunk(int tileX, int tileY)
    {
        int cx = tileX / WorldConstants.ChunkDim;
        int cy = tileY / WorldConstants.ChunkDim;
        return (cx, cy);
    }

    /// <summary>
    /// Returns the world-space center of a chunk in meters (XZ plane, Y = 0).
    /// </summary>
    public static Vector3 ChunkToWorld(int cx, int cy)
    {
        float chunkMeters = WorldConstants.ChunkDim * WorldConstants.TileSizeMeters;
        float centerX = cx * chunkMeters + chunkMeters * 0.5f;
        float centerZ = cy * chunkMeters + chunkMeters * 0.5f;
        return new Vector3(centerX, 0f, centerZ);
    }
}
