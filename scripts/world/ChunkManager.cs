namespace No1.World;
using Godot;
using System.Collections.Generic;

/// <summary>
/// Manages 3x3 chunk loading around the player.
/// Max 1 chunk load per frame for staggered performance.
/// </summary>
public partial class ChunkManager : Node
{
	public WorldData WorldData { get; private set; }
	public Player3D Player { get; set; }

	private Node3D _terrainParent;
	private int _playerChunkX = -1;
	private int _playerChunkY = -1;
	private int _loadQueueIndex;
	private List<(int cx, int cy)> _loadQueue = new();

	private const float ChunkMeters = WorldConstants.ChunkDim * WorldConstants.TileSizeMeters;

	// ── Init ──────────────────────────────────────────────────────────

	public void Init(WorldData worldData, Node3D terrainParent)
	{
		WorldData = worldData;
		_terrainParent = terrainParent;

		if (worldData.Chunks == null)
			worldData.Chunks = new ChunkData[WorldConstants.TotalChunks];

		for (int cy = 0; cy < WorldConstants.ChunksY; cy++)
		{
			for (int cx = 0; cx < WorldConstants.ChunksX; cx++)
			{
				int index = cy * WorldConstants.ChunksX + cx;
				worldData.Chunks[index] ??= new ChunkData();
				var c = worldData.Chunks[index];
				c.X = cx;
				c.Y = cy;
				c.IsLoaded = false;
				c.SceneNode = null;
				c.GroundMesh = null;
			}
		}
	}

	// ── Process ───────────────────────────────────────────────────────

	public override void _Process(double delta)
	{
		if (WorldData == null || Player == null)
			return;

		var pos = Player.GlobalPosition;
		var (cx, cy) = WorldToChunk(pos.X, pos.Z);

		if (cx != _playerChunkX || cy != _playerChunkY)
		{
			UpdateChunkWindow(cx, cy);
			_playerChunkX = cx;
			_playerChunkY = cy;
		}

		ProcessLoadQueue();
	}

	// ── Window management ─────────────────────────────────────────────

	void UpdateChunkWindow(int centerCx, int centerCy)
	{
		// Build 3x3 window within world bounds
		var window = new HashSet<(int, int)>();
		for (int dy = -1; dy <= 1; dy++)
		{
			for (int dx = -1; dx <= 1; dx++)
			{
				int wx = centerCx + dx;
				int wy = centerCy + dy;
				if (wx >= 0 && wx < WorldConstants.ChunksX && wy >= 0 && wy < WorldConstants.ChunksY)
				{
					window.Add((wx, wy));
				}
			}
		}

		// Unload chunks outside window
		for (int cy = 0; cy < WorldConstants.ChunksY; cy++)
		{
			for (int cx = 0; cx < WorldConstants.ChunksX; cx++)
			{
				var chunk = WorldData.Chunks[cy * WorldConstants.ChunksX + cx];
				if (chunk.IsLoaded && !window.Contains((cx, cy)))
				{
					UnloadChunk(cx, cy);
				}
			}
		}

		// Queue chunks inside window for loading
		_loadQueue.Clear();
		_loadQueueIndex = 0;
		foreach (var (wx, wy) in window)
		{
			int index = wy * WorldConstants.ChunksX + wx;
			if (!WorldData.Chunks[index].IsLoaded)
			{
				_loadQueue.Add((wx, wy));
			}
		}
	}

	void ProcessLoadQueue()
	{
		if (_loadQueueIndex >= _loadQueue.Count)
			return;

		var (cx, cy) = _loadQueue[_loadQueueIndex];
		LoadChunk(cx, cy);
		_loadQueueIndex++;
	}

	// ── Load / Unload ─────────────────────────────────────────────────

	void LoadChunk(int cx, int cy)
	{
		var chunk = WorldData.Chunks[cy * WorldConstants.ChunksX + cx];
		if (chunk.IsLoaded)
			return;

		chunk.SceneNode = new Node3D { Name = $"Chunk_{cx}_{cy}" };

		var mesh = new QuadMesh
		{
			Size = new Vector2(ChunkMeters, ChunkMeters)
		};
		var meshInstance = new MeshInstance3D
		{
			Mesh = mesh,
			RotationDegrees = new Vector3(-90, 0, 0),
			Position = new Vector3(
				cx * ChunkMeters + ChunkMeters * 0.5f,
				0,
				cy * ChunkMeters + ChunkMeters * 0.5f),
			MaterialOverride = SelectMaterial(chunk)
		};
		chunk.GroundMesh = meshInstance;

		chunk.SceneNode.AddChild(meshInstance);
		_terrainParent.AddChild(chunk.SceneNode);

		chunk.IsLoaded = true;
	}

	ShaderMaterial SelectMaterial(ChunkData chunk)
	{
		var baseMat = PickBaseMaterial(chunk);
		var shader = _tileShader ??= ResourceLoader.Load<Shader>("res://shaders/tile_ground.gdshader");
		var sm = new ShaderMaterial { Shader = shader };
		if (baseMat != null && baseMat.AlbedoTexture != null)
			sm.SetShaderParameter("tex", baseMat.AlbedoTexture);
		else
			sm.SetShaderParameter("tex", MakeColorTex(baseMat?.AlbedoColor ?? new Color(0.2f, 0.4f, 0.15f)));
		sm.SetShaderParameter("tile_count", (float)WorldConstants.ChunkDim * 0.25f);
		return sm;
	}
	Shader _tileShader;

	void UnloadChunk(int cx, int cy)
	{
		var chunk = WorldData.Chunks[cy * WorldConstants.ChunksX + cx];
		if (!chunk.IsLoaded) return;
		chunk.SceneNode?.QueueFree();
		chunk.SceneNode = null;
		chunk.GroundMesh = null;
		chunk.IsLoaded = false;
	}

	ImageTexture MakeColorTex(Color c)
	{
		var img = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
		img.Fill(c);
		return ImageTexture.CreateFromImage(img);
	}

	StandardMaterial3D PickBaseMaterial(ChunkData chunk)
	{
		// Sample every 8th tile for speed (~256 samples per chunk)
		int[] counts = new int[8];
		int step = 8;

		for (int ty = 0; ty < WorldConstants.ChunkDim; ty += step)
		{
			for (int tx = 0; tx < WorldConstants.ChunkDim; tx += step)
			{
				int tileIndex = ty * WorldConstants.ChunkDim + tx;
				TileType type = chunk.Tiles[tileIndex].Type;
				if (type != TileType.Path)
				{
					counts[(int)type]++;
				}
			}
		}

		// Find dominant tile type
		TileType dominant = TileType.Grass;
		int maxCount = 0;
		for (int i = 0; i < 8; i++)
		{
			if (counts[i] > maxCount)
			{
				maxCount = counts[i];
				dominant = (TileType)i;
			}
		}

		if (maxCount == 0)
			return WorldMaterials.Instance?.GrassBase;

		var m = WorldMaterials.Instance;
		if (m == null)
			return null;

		return dominant switch
		{
			TileType.Grass => m.GrassBase,
			TileType.Dirt => m.Path01,
			TileType.Water => m.ZoneCrystal,
			TileType.Rock => m.ZoneMine,
			TileType.Snow => m.ZoneSpring,
			TileType.Sand => m.ZoneCliff,
			TileType.Swamp => m.ZoneWasteland,
			_ => m.GrassBase
		};
	}

	// ── Coordinate conversion ─────────────────────────────────────────

	(int cx, int cy) WorldToChunk(float worldX, float worldZ)
	{
		int cx = Mathf.Clamp((int)(worldX / ChunkMeters), 0, WorldConstants.ChunksX - 1);
		int cy = Mathf.Clamp((int)(worldZ / ChunkMeters), 0, WorldConstants.ChunksY - 1);
		return (cx, cy);
	}
}
