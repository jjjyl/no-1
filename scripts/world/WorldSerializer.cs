using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace No1.World;

public static class WorldSerializer
{
    private const ushort FormatVersion = 1;
    private static readonly byte[] MagicBytes = Encoding.ASCII.GetBytes("WSMK");

    // ── Public API ──────────────────────────────────────────────────────────

    public static void Serialize(WorldData world, string path)
    {
        DirAccess.MakeDirRecursiveAbsolute("user://saves");

        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (f == null)
            throw new System.IO.IOException($"Failed to open file for writing: {path}");

        ushort regionCount = (ushort)(world.Regions?.Length ?? 0);
        ushort pathCount = (ushort)(world.Paths?.Length ?? 0);
        ushort entityCount = CountAllEntities(world);

        WriteHeader(f, world, regionCount, pathCount, entityCount);
        WriteTiles(f, world);
        WriteRegions(f, world);
        WritePaths(f, world);
        WriteEntities(f, world);

        f.Close();
    }

    public static WorldData Deserialize(string path)
    {
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f == null)
            throw new System.IO.IOException($"Failed to open file for reading: {path}");

        ValidateMagic(f);

        ushort version = f.Get16();
        if (version > FormatVersion)
            throw new System.IO.InvalidDataException(
                $"Unsupported file version {version}. Maximum supported: {FormatVersion}.");

        ulong seed = f.Get64();
        int cycle = (int)f.Get32();
        int width = f.Get16();
        int height = f.Get16();
        int chunkSize = f.Get16();
        ushort regionCount = f.Get16();
        ushort pathCount = f.Get16();
        ushort entityCount = f.Get16();
        f.Get16(); // skip reserved

        var world = new WorldData
        {
            Seed = seed,
            CycleNumber = cycle,
            Width = width,
            Height = height,
            ChunkSize = chunkSize,
            Chunks = new ChunkData[WorldConstants.TotalChunks],
            Regions = regionCount > 0 ? new RegionPlacement[regionCount] : Array.Empty<RegionPlacement>(),
            Paths = pathCount > 0 ? new PathData[pathCount] : Array.Empty<PathData>()
        };

        ReadTiles(f, world);
        ReadRegions(f, world, regionCount);
        ReadPaths(f, world, pathCount);
        ReadEntities(f, world, entityCount, chunkSize);

        f.Close();
        return world;
    }

    public static bool Exists(ulong seed, int cycle, string[] overridePaths)
    {
        return FileAccess.FileExists(GetFilePath(seed, cycle, overridePaths));
    }

    public static string GetFilePath(ulong seed, int cycle, string[] overridePaths)
    {
        string hash = overridePaths != null && overridePaths.Length > 0
            ? ((uint)string.Join(",", overridePaths).GetHashCode()).ToString("X8")
            : "none";
        return $"user://saves/world_{seed}_{cycle}_{hash}.bin";
    }

    // ── Header ──────────────────────────────────────────────────────────────

    private static void WriteHeader(FileAccess f, WorldData world,
        ushort regionCount, ushort pathCount, ushort entityCount)
    {
        f.StoreBuffer(MagicBytes);                 // 4 bytes: "WSMK"
        f.Store16(FormatVersion);                  // 2 bytes
        f.Store64(world.Seed);                     // 8 bytes
        f.Store32((uint)world.CycleNumber);        // 4 bytes
        f.Store16((ushort)world.Width);            // 2 bytes
        f.Store16((ushort)world.Height);           // 2 bytes
        f.Store16((ushort)world.ChunkSize);        // 2 bytes
        f.Store16(regionCount);                    // 2 bytes
        f.Store16(pathCount);                      // 2 bytes
        f.Store16(entityCount);                    // 2 bytes
        f.Store16(0);                              // 2 bytes reserved
    }

    private static void ValidateMagic(FileAccess f)
    {
        byte[] read = f.GetBuffer(4);
        string magic = Encoding.ASCII.GetString(read);
        if (magic != "WSMK")
            throw new System.IO.InvalidDataException(
                $"Invalid file format. Expected 'WSMK', got '{magic}'.");
    }

    // ── Tiles ───────────────────────────────────────────────────────────────

    private static void WriteTiles(FileAccess f, WorldData world)
    {
        foreach (var chunk in world.Chunks)
        {
            f.Store16((ushort)chunk.X);
            f.Store16((ushort)chunk.Y);
            for (int i = 0; i < WorldConstants.TilesPerChunk; i++)
            {
                f.Store16(chunk.Tiles[i].ToU16());
            }
        }
    }

    private static void ReadTiles(FileAccess f, WorldData world)
    {
        for (int i = 0; i < WorldConstants.TotalChunks; i++)
        {
            int cx = f.Get16();
            int cy = f.Get16();
            var chunk = new ChunkData { X = cx, Y = cy };
            for (int j = 0; j < WorldConstants.TilesPerChunk; j++)
            {
                chunk.Tiles[j] = TileData.FromU16(f.Get16());
            }
            world.Chunks[i] = chunk;
        }
    }

    // ── Regions ─────────────────────────────────────────────────────────────

    private static void WriteRegions(FileAccess f, WorldData world)
    {
        if (world.Regions == null) return;
        foreach (var r in world.Regions)
        {
            WriteString(f, r.Id);
            WriteString(f, r.Name);
            f.Store16((ushort)r.TileX);
            f.Store16((ushort)r.TileY);
            f.Store16((ushort)r.Radius);
        }
    }

    private static void ReadRegions(FileAccess f, WorldData world, ushort count)
    {
        for (int i = 0; i < count; i++)
        {
            world.Regions[i] = new RegionPlacement
            {
                Id = ReadString(f),
                Name = ReadString(f),
                TileX = f.Get16(),
                TileY = f.Get16(),
                Radius = f.Get16()
            };
        }
    }

    // ── Paths ───────────────────────────────────────────────────────────────

    private static void WritePaths(FileAccess f, WorldData world)
    {
        if (world.Paths == null) return;
        foreach (var p in world.Paths)
        {
            f.Store32((uint)p.TileCount);
            for (int i = 0; i < p.TileCount; i++)
            {
                f.Store32((uint)p.TileIndices[i]);
            }
        }
    }

    private static void ReadPaths(FileAccess f, WorldData world, ushort count)
    {
        for (int i = 0; i < count; i++)
        {
            int tileCount = (int)f.Get32();
            int[] indices = new int[tileCount];
            for (int j = 0; j < tileCount; j++)
            {
                indices[j] = (int)f.Get32();
            }
            world.Paths[i] = new PathData { TileCount = tileCount, TileIndices = indices };
        }
    }

    // ── Entities ────────────────────────────────────────────────────────────

    private static ushort CountAllEntities(WorldData world)
    {
        ushort count = 0;
        foreach (var chunk in world.Chunks)
        {
            if (chunk.Entities != null)
                count += (ushort)chunk.Entities.Count;
        }
        return count;
    }

    private static void WriteEntities(FileAccess f, WorldData world)
    {
        foreach (var chunk in world.Chunks)
        {
            if (chunk.Entities == null) continue;
            foreach (var e in chunk.Entities)
            {
                f.Store8((byte)e.Type);
                WriteString(f, e.Id);
                f.Store16((ushort)e.TileX);
                f.Store16((ushort)e.TileY);
                f.Store8(e.State);
            }
        }
    }

    private static void ReadEntities(FileAccess f, WorldData world, ushort count, int chunkSize)
    {
        var entitiesByChunk = new List<EntitySpawn>[WorldConstants.TotalChunks];
        for (int i = 0; i < WorldConstants.TotalChunks; i++)
            entitiesByChunk[i] = new List<EntitySpawn>();

        for (int i = 0; i < count; i++)
        {
            var entity = new EntitySpawn
            {
                Type = (EntityType)f.Get8(),
                Id = ReadString(f),
                TileX = f.Get16(),
                TileY = f.Get16(),
                State = f.Get8()
            };

            int cx = entity.TileX / chunkSize;
            int cy = entity.TileY / chunkSize;
            int ci = cy * WorldConstants.ChunksX + cx;
            if (ci >= 0 && ci < WorldConstants.TotalChunks)
                entitiesByChunk[ci].Add(entity);
        }

        for (int i = 0; i < WorldConstants.TotalChunks; i++)
            world.Chunks[i].Entities = entitiesByChunk[i];
    }

    // ── String helpers ──────────────────────────────────────────────────────

    private static void WriteString(FileAccess f, string s)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(s ?? "");
        f.Store8((byte)bytes.Length);
        if (bytes.Length > 0)
            f.StoreBuffer(bytes);
    }

    private static string ReadString(FileAccess f)
    {
        int len = f.Get8();
        if (len == 0)
            return "";
        byte[] bytes = f.GetBuffer(len);
        return Encoding.UTF8.GetString(bytes);
    }
}
