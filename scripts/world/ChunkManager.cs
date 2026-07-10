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

			var terrainMesh = BuildChunkMesh(chunk);
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

			int dim = WorldConstants.ChunkDim;
			var materials = WorldMaterials.Instance;
			var dualMeshes = BuildDualGridMeshes(chunk, cx, cy, dim);
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
			ScatterDecorations(chunk, chunk.SceneNode);
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

	ArrayMesh BuildChunkMesh(ChunkData chunk)
	{
		const float HEIGHT_SCALE = 5.0f;
		const float EDGE_OVERLAP = 0.05f;
		int dim = WorldConstants.ChunkDim;
		float tileSize = WorldConstants.TileSizeMeters;
		float halfExtent = dim * tileSize * 0.5f + EDGE_OVERLAP;
		int vertsPerRow = dim + 1;

		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		for (int z = 0; z <= dim; z++)
		{
			for (int x = 0; x <= dim; x++)
			{
				float height = GetVertexHeight(chunk, x, z, dim, HEIGHT_SCALE);
				float worldX = (float)x / dim * 2.0f * halfExtent - halfExtent;
				float worldZ = (float)z / dim * 2.0f * halfExtent - halfExtent;
				st.SetUV(new Vector2((float)x / dim, (float)z / dim));
				st.AddVertex(new Vector3(worldX, height, worldZ));
			}
		}

		for (int z = 0; z < dim; z++)
		{
			for (int x = 0; x < dim; x++)
			{
				int v00 = z * vertsPerRow + x;
				int v01 = z * vertsPerRow + (x + 1);
				int v10 = (z + 1) * vertsPerRow + x;
				int v11 = (z + 1) * vertsPerRow + (x + 1);

				st.AddIndex(v00);
				st.AddIndex(v10);
				st.AddIndex(v01);

				st.AddIndex(v10);
				st.AddIndex(v11);
				st.AddIndex(v01);
			}
		}

		// ── Skirt: vertical edge walls to hide chunk seams ──
		const float SKIRT_DEPTH = 4.0f;
		int baseIdx = vertsPerRow * vertsPerRow;

		void AddSkirtQuad(int ax, int az, int bx, int bz)
		{
			float ha = GetVertexHeight(chunk, ax, az, dim, HEIGHT_SCALE);
			float hb = GetVertexHeight(chunk, bx, bz, dim, HEIGHT_SCALE);
			float wax = (float)ax / dim * 2.0f * halfExtent - halfExtent;
			float waz = (float)az / dim * 2.0f * halfExtent - halfExtent;
			float wbx = (float)bx / dim * 2.0f * halfExtent - halfExtent;
			float wbz = (float)bz / dim * 2.0f * halfExtent - halfExtent;

			st.SetUV(Vector2.Zero);
			st.AddVertex(new Vector3(wax, ha, waz));
			st.SetUV(Vector2.Zero);
			st.AddVertex(new Vector3(wax, ha - SKIRT_DEPTH, waz));
			st.SetUV(Vector2.Zero);
			st.AddVertex(new Vector3(wbx, hb, wbz));
			st.SetUV(Vector2.Zero);
			st.AddVertex(new Vector3(wbx, hb - SKIRT_DEPTH, wbz));

			st.AddIndex(baseIdx); st.AddIndex(baseIdx + 1); st.AddIndex(baseIdx + 2);
			st.AddIndex(baseIdx + 1); st.AddIndex(baseIdx + 3); st.AddIndex(baseIdx + 2);
			baseIdx += 4;
		}

		for (int z = 0; z < dim; z++) AddSkirtQuad(0, z, 0, z + 1);
		for (int z = 0; z < dim; z++) AddSkirtQuad(dim, z, dim, z + 1);
		for (int x = 0; x < dim; x++) AddSkirtQuad(x, 0, x + 1, 0);
		for (int x = 0; x < dim; x++) AddSkirtQuad(x, dim, x + 1, dim);

		st.GenerateNormals();
		return st.Commit();
	}

	Dictionary<string, ArrayMesh> BuildDualGridMeshes(ChunkData chunk, int cx, int cy, int dim)
	{
		const float HEIGHT_SCALE = 5.0f;
		const float Y_OFFSET = 0.03f;
		const float EDGE_OVERLAP = 0.05f;
		float halfExtent = dim * WorldConstants.TileSizeMeters * 0.5f + EDGE_OVERLAP;
		float cellWidth = 2.0f * halfExtent / dim;

		var sts = new Dictionary<string, SurfaceTool>();
		var vi = new Dictionary<string, int>();

		float WorldVertexHeight(int wgx, int wgz, float scale)
		{
			float sum = 0;
			int count = 0;
			bool allWater = true;

			for (int dz = -1; dz <= 0; dz++)
			for (int dx = -1; dx <= 0; dx++)
			{
				int wx = wgx + dx;
				int wz = wgz + dz;
				if (wx < 0 || wz < 0 || wx >= WorldConstants.WorldWidth || wz >= WorldConstants.WorldHeight)
					continue;

				int ncx = wx / dim;
				int ncy = wz / dim;
				int ltx = wx % dim;
				int ltz = wz % dim;

				var nc = WorldData.Chunks[ncy * WorldConstants.ChunksX + ncx];
				if (nc.Tiles == null) continue;

				var tile = nc.Tiles[ltz * dim + ltx];
				count++;
				if (tile.Type != TileType.Water) allWater = false;
				sum += tile.Height / 255f;
			}

			if (count == 0) return 0;
			if (allWater) return -0.5f;
			return (sum / count) * scale;
		}

		float CornerHeight(ChunkData c, int gx, int gz, int d, float scale)
		{
			if (gx > 0 && gx < d && gz > 0 && gz < d)
				return GetVertexHeight(c, gx, gz, d, scale);
			int wgx = cx * d + gx;
			int wgz = cy * d + gz;
			return WorldVertexHeight(wgx, wgz, scale);
		}

		for (int tz = 0; tz < dim; tz++)
		{
			for (int tx = 0; tx < dim; tx++)
			{
				int idx = tz * dim + tx;

				var tl = chunk.Tiles[idx];
				var tr = chunk.Tiles[tz * dim + Math.Min(tx + 1, dim - 1)];
				var bl = chunk.Tiles[Math.Min(tz + 1, dim - 1) * dim + tx];
				var br = chunk.Tiles[Math.Min(tz + 1, dim - 1) * dim + Math.Min(tx + 1, dim - 1)];

				string key = DualGridEvaluator.GetMaterialKey(tl.Type, tr.Type, bl.Type, br.Type);

				if (!sts.TryGetValue(key, out var st))
				{
					st = new SurfaceTool();
					st.Begin(Mesh.PrimitiveType.Triangles);
					sts[key] = st;
					vi[key] = 0;
				}

				float h00 = CornerHeight(chunk, tx,     tz,     dim, HEIGHT_SCALE) + Y_OFFSET;
				float h10 = CornerHeight(chunk, tx + 1, tz,     dim, HEIGHT_SCALE) + Y_OFFSET;
				float h01 = CornerHeight(chunk, tx,     tz + 1, dim, HEIGHT_SCALE) + Y_OFFSET;
				float h11 = CornerHeight(chunk, tx + 1, tz + 1, dim, HEIGHT_SCALE) + Y_OFFSET;

				float x0 = tx * cellWidth - halfExtent;
				float x1 = x0 + cellWidth;
				float z0 = tz * cellWidth - halfExtent;
				float z1 = z0 + cellWidth;

				st.SetUV(new Vector2(0, 0));
				st.AddVertex(new Vector3(x0, h00, z0));
				st.SetUV(new Vector2(1, 0));
				st.AddVertex(new Vector3(x1, h10, z0));
				st.SetUV(new Vector2(0, 1));
				st.AddVertex(new Vector3(x0, h01, z1));
				st.SetUV(new Vector2(1, 1));
				st.AddVertex(new Vector3(x1, h11, z1));

				int v = vi[key];
				st.AddIndex(v);
				st.AddIndex(v + 1);
				st.AddIndex(v + 2);
				st.AddIndex(v + 1);
				st.AddIndex(v + 3);
				st.AddIndex(v + 2);
				vi[key] = v + 4;
			}
		}

		var result = new Dictionary<string, ArrayMesh>();
		foreach (var kvp in sts)
		{
			kvp.Value.GenerateNormals();
			result[kvp.Key] = kvp.Value.Commit();
		}
		return result;
	}

	float GetVertexHeight(ChunkData chunk, int gx, int gz, int dim, float heightScale)
	{
		bool allWater = true;
		bool anyTile = false;
		float heightSum = 0f;
		int heightCount = 0;

		for (int dz = -1; dz <= 0; dz++)
		{
			for (int dx = -1; dx <= 0; dx++)
			{
				int tx = gx + dx;
				int tz = gz + dz;
				if (tx >= 0 && tx < dim && tz >= 0 && tz < dim)
				{
					anyTile = true;
					var tile = chunk.Tiles[tz * dim + tx];
					if (tile.Type != TileType.Water)
						allWater = false;
					heightSum += tile.Height / 255f;
					heightCount++;
				}
			}
		}

		if (!anyTile)
			return 0f;

		if (allWater)
			return -0.5f;

		return (heightSum / heightCount) * heightScale;
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

	// ── Height query ────────────────────────────────────────────────

	public float GetHeightAt(float worldX, float worldZ)
	{
		int tileX = Mathf.Clamp((int)(worldX / WorldConstants.TileSizeMeters), 0, WorldConstants.WorldWidth - 1);
		int tileZ = Mathf.Clamp((int)(worldZ / WorldConstants.TileSizeMeters), 0, WorldConstants.WorldHeight - 1);

		int cx = tileX / WorldConstants.ChunkDim;
		int cy = tileZ / WorldConstants.ChunkDim;

		if (cx < 0 || cx >= WorldConstants.ChunksX || cy < 0 || cy >= WorldConstants.ChunksY)
			return 0f;

		var chunk = WorldData.Chunks[cy * WorldConstants.ChunksX + cx];
		if (chunk?.Tiles == null)
			return 0f;

		int lx = tileX - cx * WorldConstants.ChunkDim;
		int lz = tileZ - cy * WorldConstants.ChunkDim;

		if (lx < 0 || lx >= WorldConstants.ChunkDim || lz < 0 || lz >= WorldConstants.ChunkDim)
			return 0f;

		float heightSum = 0f;
		int count = 0;
		for (int dz = 0; dz <= 1 && lz + dz < WorldConstants.ChunkDim; dz++)
		{
			for (int dx = 0; dx <= 1 && lx + dx < WorldConstants.ChunkDim; dx++)
			{
				var tile = chunk.Tiles[(lz + dz) * WorldConstants.ChunkDim + (lx + dx)];
				heightSum += tile.Height / 255f;
				count++;
			}
		}

		return count > 0 ? heightSum / count * 5.0f : 0f;
	}

	(int cx, int cy) WorldToChunk(float worldX, float worldZ)
	{
		int cx = Mathf.Clamp((int)(worldX / ChunkMeters), 0, WorldConstants.ChunksX - 1);
		int cy = Mathf.Clamp((int)(worldZ / ChunkMeters), 0, WorldConstants.ChunksY - 1);
		return (cx, cy);
	}

	// ── Decoration scattering ─────────────────────────────────────────

	/// <summary>
	/// Defines a type of decoration: texture, ground-contact point, scale, billboard.
	/// </summary>
	struct DecorationDef
	{
		public string Name;
		public Texture2D Texture;
		public float BaseYFrac;
		public float PixelScaleBase;   // target meters per pixel, multiplied by scale factor
		public Vector2 ScaleRange;
		public BaseMaterial3D.BillboardModeEnum Billboard;
		public int PanelCount;         // 0=sprite, N=crossed quads around Y
	}

	static List<DecorationDef> _decoDefs;

	static void EnsureDecoDefs()
	{
		if (_decoDefs != null)
			return;
		_decoDefs = new();

		void Add(string name, Texture2D tex, float yFrac, float pxPerM, float sMin, float sMax, BaseMaterial3D.BillboardModeEnum bb, int panels = 0)
		{
			_decoDefs.Add(new DecorationDef
			{
				Name = name,
				Texture = tex,
				BaseYFrac = yFrac,
				PixelScaleBase = pxPerM,
				ScaleRange = new Vector2(sMin, sMax),
				Billboard = bb,
				PanelCount = panels
			});
		}

		var treeTex = WorldTextures.TryLoadTexture("res://assets/texture/world/deco_tree.png")
			?? WorldTextures.MakeSimpleTreeTexture(new Color(0.15f, 0.40f, 0.10f));

		Add("Tree",  treeTex, 0.88f, 0.007f, 0.6f, 1.0f, BaseMaterial3D.BillboardModeEnum.Enabled);
		Add("Rock",  WorldTextures.MakeSimpleRockTexture(new Color(0.35f, 0.33f, 0.30f)), 0.90f, 0.08f, 0.7f, 1.05f, BaseMaterial3D.BillboardModeEnum.Enabled);
		Add("Bush",  WorldTextures.MakeSimpleBushTexture(new Color(0.15f, 0.40f, 0.10f)), 0.85f, 0.08f, 0.6f, 0.9f, BaseMaterial3D.BillboardModeEnum.Enabled);
		Add("Tuft",  WorldTextures.MakeSimpleGrassTuftTexture(), 0.80f, 0.04f, 0.5f, 0.8f, BaseMaterial3D.BillboardModeEnum.Enabled);
		Add("Ruin",  WorldTextures.MakeSimpleRuinTexture(new Color(0.28f, 0.24f, 0.20f)), 0.95f, 0.10f, 0.8f, 1.0f, BaseMaterial3D.BillboardModeEnum.Enabled);
		Add("RockSnow", WorldTextures.MakeSimpleRockTexture(new Color(0.55f, 0.55f, 0.58f)), 0.90f, 0.08f, 0.7f, 1.05f, BaseMaterial3D.BillboardModeEnum.Enabled);
	}

	static DecorationDef? FindDeco(string name)
	{
		if (_decoDefs == null) return null;
		foreach (var d in _decoDefs)
			if (d.Name == name) return d;
		return null;
	}

	/// <summary>
	/// Spawn a Sprite3D decoration with correct Offset so ground-contact
	/// point sits at groundPos.Y.  Billboard rotates around the contact
	/// point (tree pivots from base).
	/// </summary>
	static void SpawnDecoration(DecorationDef def, Node parent, Vector3 groundPos, RandomNumberGenerator rng)
	{
		var tex = def.Texture;
		if (tex == null) return;

		float scale = rng.RandfRange(def.ScaleRange.X, def.ScaleRange.Y);
		float texH = tex.GetHeight();
		float texW = tex.GetWidth();
		float worldW = texW * def.PixelScaleBase * scale;
		float worldH = texH * def.PixelScaleBase * scale;

		// Y offset to align BaseYFrac contact point with groundPos.Y
		float yOffset = worldH * (def.BaseYFrac - 0.5f);
		float yRot = rng.RandfRange(-12, 12);

		if (def.PanelCount > 0)
		{
			var mat = WorldTextures.MakeAlphaMaterial(tex);
			var mesh = WorldTextures.BuildCrossMesh(worldW, worldH, def.PanelCount);
			var mi = new MeshInstance3D
			{
				Mesh = mesh,
				MaterialOverride = mat,
				Position = groundPos + new Vector3(0, yOffset, 0),
				RotationDegrees = new Vector3(0, yRot, 0),
			};
			parent.AddChild(mi);
		}
		else
		{
			float offsetY = texH * (def.BaseYFrac - 0.5f);
			var mat = WorldTextures.MakeAlphaMaterial(tex);
			mat.BillboardMode = def.Billboard;
			var sprite = new Sprite3D
			{
				Texture = tex,
				Billboard = def.Billboard,
				Position = groundPos,
				PixelSize = def.PixelScaleBase * scale,
				Offset = new Vector2(0, offsetY),
				Modulate = Colors.White,
				MaterialOverride = mat,
			};
			if (def.Billboard != BaseMaterial3D.BillboardModeEnum.Disabled)
				sprite.RotationDegrees = new Vector3(0, yRot, 0);
			parent.AddChild(sprite);
		}
	}

	void ScatterDecorations(ChunkData chunk, Node3D sceneNode)
	{
		EnsureDecoDefs();

		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(chunk.X * 1000 + chunk.Y) + WorldData.Seed % 1000;

		int dim = WorldConstants.ChunkDim;
		const int STEP = 4;
		float tileSize = WorldConstants.TileSizeMeters;
		float half = dim * tileSize * 0.5f;

		for (int ty = 0; ty < dim; ty += STEP)
		for (int tx = 0; tx < dim; tx += STEP)
		{
			int ti = ty * dim + tx;
			TileType tileType = chunk.Tiles[ti].Type;
			if (tileType == TileType.Water || tileType == TileType.Path)
				continue;

			float groundH = chunk.Tiles[ti].Height / 255f * 5.0f + 0.02f;
			float localX = (tx + 0.5f) * tileSize - half;
			float localZ = (ty + 0.5f) * tileSize - half;
			Vector3 pos = new Vector3(localX, groundH, localZ);

			float roll = rng.Randf();
			DecorationDef? pick = null;

			switch (tileType)
			{
				case TileType.Grass:
					if (roll < 0.05f)       pick = FindDeco("Tree");
					else if (roll < 0.08f)  pick = FindDeco("Rock");
					else if (roll < 0.10f)  pick = FindDeco("Bush");
					else if (roll < 0.14f)  pick = FindDeco("Tuft");
					break;

				case TileType.Dirt:
					if (roll < 0.04f)       pick = FindDeco("Rock");
					else if (roll < 0.05f)  pick = FindDeco("Ruin");
					else if (roll < 0.07f)  pick = FindDeco("Tuft");
					else if (roll < 0.09f)  pick = FindDeco("Tree");
					break;

				case TileType.Rock:
					if (roll < 0.05f)       pick = FindDeco("Rock");
					else if (roll < 0.08f)  pick = FindDeco("Tree");
					break;

				case TileType.Sand:
				case TileType.Swamp:
					if (roll < 0.03f)       pick = FindDeco("Rock");
					else if (roll < 0.04f)  pick = FindDeco("Tuft");
					break;

				case TileType.Snow:
					if (roll < 0.03f)       pick = FindDeco("RockSnow");
					break;
			}

			if (pick != null)
				SpawnDecoration(pick.Value, sceneNode, pos, rng);
		}
	}

	// ── Texture helpers ────────────────────────────────────────────
}
