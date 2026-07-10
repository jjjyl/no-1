namespace No1.World;
using Godot;
using System.Collections.Generic;

/// <summary>
/// Manages 3x3 chunk loading around the player.
/// Max 1 chunk load per frame for staggered performance.
/// Delegates mesh building to TerrainMeshBuilder and decoration to DecorationPlacer.
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
		if (chunk.IsLoaded) return;

		if (chunk.Tiles == null)
		{
			GD.PrintErr($"[CHUNK] ({cx},{cy}) Tiles is null — skipping, will retry");
			return;
		}

		if (chunk.Tiles.Length != WorldConstants.TilesPerChunk)
		{
			GD.PrintErr($"[CHUNK] ({cx},{cy}) Tiles length mismatch: {chunk.Tiles.Length} vs expected {WorldConstants.TilesPerChunk}");
			return;
		}

		try
		{
			var chunkCenter = new Vector3(
				cx * ChunkMeters + ChunkMeters * 0.5f,
				0,
				cy * ChunkMeters + ChunkMeters * 0.5f);

			chunk.SceneNode = new Node3D
			{
				Name = $"Chunk_{cx}_{cy}",
				Position = chunkCenter
			};

			// ── Terrain mesh ──
			int dim = WorldConstants.ChunkDim;
			float tileSize = WorldConstants.TileSizeMeters;

			var terrainMesh = TerrainMeshBuilder.BuildChunkMesh(chunk, dim, tileSize);
			if (terrainMesh == null)
			{
				GD.PrintErr($"[CHUNK] ({cx},{cy}) BuildChunkMesh returned null");
				return;
			}

			var neutralMat = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.5f, 0.5f, 0.5f),
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				Metallic = 0f,
				Roughness = 1.0f,
				TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
			};
			neutralMat.ResourceName = "TerrainBase";

			var terrainMI = new MeshInstance3D
			{
				Mesh = terrainMesh,
				MaterialOverride = neutralMat,
				Name = "terrain"
			};
			chunk.GroundMesh = terrainMI;
			chunk.SceneNode.AddChild(terrainMI);

			// ── Dual grid meshes ──
			var dualMeshes = TerrainMeshBuilder.BuildDualGridMeshes(chunk, cx, cy, dim, WorldData.Chunks);
			var materials = WorldMaterials.Instance;
			foreach (var kvp in dualMeshes)
			{
				var mat = materials.GetMaterialForKey(kvp.Key);
				var mi = new MeshInstance3D
				{
					Mesh = kvp.Value,
					MaterialOverride = mat,
					Name = kvp.Key
				};
				chunk.SceneNode.AddChild(mi);
			}

			_terrainParent.AddChild(chunk.SceneNode);

			// ── Decorations ──
			DecorationPlacer.Scatter(chunk, cx, cy, chunk.SceneNode, WorldData.Chunks, WorldData.Seed);

			chunk.IsLoaded = true;
		}
		catch (System.Exception ex)
		{
			GD.PrintErr($"[CHUNK] ({cx},{cy}) Load failed: {ex.GetType().Name}: {ex.Message}");
			chunk.SceneNode?.QueueFree();
			chunk.SceneNode = null;
			chunk.GroundMesh = null;
		}
	}

	void UnloadChunk(int cx, int cy)
	{
		var chunk = WorldData.Chunks[cy * WorldConstants.ChunksX + cx];
		if (!chunk.IsLoaded) return;
		chunk.SceneNode?.QueueFree();
		chunk.SceneNode = null;
		chunk.GroundMesh = null;
		chunk.IsLoaded = false;
	}

	// ── Public queries ────────────────────────────────────────────────

	/// <summary>
	/// Bilinear-smoothed world-space height for player/enemy foot placement.
	/// </summary>
	public float GetHeightAt(float worldX, float worldZ)
	{
		return TerrainHeight.GetHeightAt(WorldData.Chunks, worldX, worldZ);
	}

	// ── Internal ──────────────────────────────────────────────────────

	(int cx, int cy) WorldToChunk(float worldX, float worldZ)
	{
		int cx = Mathf.Clamp((int)(worldX / ChunkMeters), 0, WorldConstants.ChunksX - 1);
		int cy = Mathf.Clamp((int)(worldZ / ChunkMeters), 0, WorldConstants.ChunksY - 1);
		return (cx, cy);
	}
}
