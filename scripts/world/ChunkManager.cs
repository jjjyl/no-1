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

		if (_playerChunkX == -1)
			GD.Print($"[CHUNK-SYS] World={WorldData.Width}x{WorldData.Height} " +
			         $"Chunks={WorldConstants.ChunksX}x{WorldConstants.ChunksY} " +
			         $"TotalChunks={WorldConstants.TotalChunks} TilesPerChunk={WorldConstants.TilesPerChunk}");

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
			chunk.SceneNode = new Node3D { Name = $"Chunk_{cx}_{cy}" };

			var mesh = BuildChunkMesh(chunk);
			if (mesh == null)
			{
				GD.PrintErr($"[CHUNK] ({cx},{cy}) BuildChunkMesh returned null");
				return;
			}

			var mat = SelectMaterial(chunk);
			if (mat == null)
			{
				GD.PrintErr($"[CHUNK] ({cx},{cy}) SelectMaterial returned null");
				return;
			}

			var meshInstance = new MeshInstance3D
			{
				Mesh = mesh,
				Position = new Vector3(
					cx * ChunkMeters + ChunkMeters * 0.5f,
					0,
					cy * ChunkMeters + ChunkMeters * 0.5f),
				MaterialOverride = mat
			};
			chunk.GroundMesh = meshInstance;

			chunk.SceneNode.AddChild(meshInstance);
			_terrainParent.AddChild(chunk.SceneNode);

			var aabb = mesh.GetAabb();
			var vertCount = mesh.GetSurfaceCount() > 0 ? mesh.SurfaceGetArrayLen(0) : 0;
			GD.Print($"[CHUNK] ({cx},{cy}) loaded OK — verts={vertCount} " +
			         $"aabb=({aabb.Position.X:F1},{aabb.Position.Y:F1},{aabb.Position.Z:F1}) size=({aabb.Size.X:F1},{aabb.Size.Y:F1},{aabb.Size.Z:F1}) " +
			         $"worldPos=({meshInstance.GlobalPosition.X:F1},{meshInstance.GlobalPosition.Y:F1},{meshInstance.GlobalPosition.Z:F1}) " +
			         $"matType={mat.GetType().Name}");

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

				// Triangle 1: (x,z) -> (x,z+1) -> (x+1,z)  normal UP
				st.AddIndex(v00);
				st.AddIndex(v10);
				st.AddIndex(v01);

				// Triangle 2: (x,z+1) -> (x+1,z+1) -> (x+1,z)  normal UP
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

	// Set to false once shader path is confirmed working
	static bool UseShaderMaterial = false;

	Material SelectMaterial(ChunkData chunk)
	{
		var analysis = AnalyzeChunkTiles(chunk);
		var baseMat = SelectMaterialFromAnalysis(analysis);

		if (UseShaderMaterial)
		{
			var shader = _tileShader ??= ResourceLoader.Load<Shader>("res://shaders/tile_ground.gdshader");
			if (shader == null)
			{
				GD.PrintErr("[CHUNK] tile_ground.gdshader not found or failed to compile — using StandardMaterial3D fallback");
				_tileShader = null;
				return baseMat ?? new StandardMaterial3D
				{
					AlbedoColor = new Color(0.2f, 0.45f, 0.15f),
					ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
					CullMode = BaseMaterial3D.CullModeEnum.Disabled
				};
			}

			var sm = new ShaderMaterial { Shader = shader };
			if (baseMat != null && baseMat.AlbedoTexture != null)
				sm.SetShaderParameter("tex", baseMat.AlbedoTexture);
			else
				sm.SetShaderParameter("tex", MakeColorTex(baseMat?.AlbedoColor ?? new Color(0.2f, 0.4f, 0.15f)));

			float tileCount = analysis.DominantRatio > 0.8f ? 4f : 12f;
			sm.SetShaderParameter("tile_count", tileCount);
			return sm;
		}

		return new StandardMaterial3D
		{
			AlbedoColor = new Color(0.25f, 0.55f, 0.20f),
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};
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

	struct ChunkTileAnalysis
	{
		public int TotalSamples;
		public TileType Dominant;
		public TileType SecondDominant;
		public float DominantRatio;
	}

	ChunkTileAnalysis AnalyzeChunkTiles(ChunkData chunk)
	{
		int[] counts = new int[8];
		int step = 8;
		int total = 0;

		for (int ty = 0; ty < WorldConstants.ChunkDim; ty += step)
		{
			for (int tx = 0; tx < WorldConstants.ChunkDim; tx += step)
			{
				int tileIndex = ty * WorldConstants.ChunkDim + tx;
				TileType type = chunk.Tiles[tileIndex].Type;
				if (type != TileType.Path)
				{
					counts[(int)type]++;
					total++;
				}
			}
		}

		TileType dominant = TileType.Grass;
		TileType second = TileType.Grass;
		int maxCount = 0;
		int secondCount = 0;

		for (int i = 0; i < 8; i++)
		{
			if (counts[i] > maxCount)
			{
				secondCount = maxCount;
				second = dominant;
				maxCount = counts[i];
				dominant = (TileType)i;
			}
			else if (counts[i] > secondCount)
			{
				secondCount = counts[i];
				second = (TileType)i;
			}
		}

		float ratio = total > 0 ? (float)maxCount / total : 1f;

		return new ChunkTileAnalysis
		{
			TotalSamples = total,
			Dominant = dominant,
			SecondDominant = second,
			DominantRatio = ratio
		};
	}

	StandardMaterial3D SelectMaterialFromAnalysis(ChunkTileAnalysis a)
	{
		var m = WorldMaterials.Instance;
		if (m == null) return null;

		if (a.DominantRatio > 0.7f || a.TotalSamples == 0)
			return GetMaterialForTileType(a.Dominant);

		TileType d = a.Dominant;
		TileType s = a.SecondDominant;

		return (d, s) switch
		{
			(TileType.Grass, TileType.Dirt) or (TileType.Dirt, TileType.Grass) => m.GrassBase,
			(TileType.Grass, TileType.Rock) or (TileType.Rock, TileType.Grass) => m.ZoneMine,
			(TileType.Grass, TileType.Sand) or (TileType.Sand, TileType.Grass) => m.ZoneCliff,
			(TileType.Grass, TileType.Water) or (TileType.Water, TileType.Grass) => m.ZoneSpring,
			(_, TileType.Snow) or (TileType.Snow, _) => m.ZoneSpring,
			_ => GetMaterialForTileType(d)
		};
	}

	StandardMaterial3D GetMaterialForTileType(TileType type)
	{
		var m = WorldMaterials.Instance;
		if (m == null) return null;

		return type switch
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

	void ScatterDecorations(ChunkData chunk, Node3D sceneNode)
	{
		var mats = WorldMaterials.Instance;

		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(chunk.X * 1000 + chunk.Y) + WorldData.Seed % 1000;

		int dim = WorldConstants.ChunkDim;
		int count = rng.RandiRange(30, Mathf.Min(80, dim * dim / 50));

		for (int i = 0; i < count; i++)
		{
			int tx = rng.RandiRange(0, dim - 1);
			int ty = rng.RandiRange(0, dim - 1);
			int ti = ty * dim + tx;
			TileType type = chunk.Tiles[ti].Type;

			float wx = (chunk.X * dim + tx) * WorldConstants.TileSizeMeters;
			float wz = (chunk.Y * dim + ty) * WorldConstants.TileSizeMeters;
			float ox = rng.RandfRange(-0.2f, 0.2f);
			float oz = rng.RandfRange(-0.2f, 0.2f);
			Vector3 pos = new Vector3(wx + ox, 0, wz + oz);

			float roll = rng.Randf();

			switch (type)
			{
				case TileType.Grass:
					if (roll < 0.6f)
						MakeSimpleTree(sceneNode, pos, mats?.DecoTree?.AlbedoColor ?? new Color(0.15f, 0.40f, 0.10f));
					else if (roll < 0.9f)
						MakeSimpleBush(sceneNode, pos, mats?.DecoTree?.AlbedoColor ?? new Color(0.15f, 0.40f, 0.10f));
					else
						MakeSimpleGrassTuft(sceneNode, pos);
					break;

				case TileType.Dirt:
					if (roll < 0.8f)
						MakeSimpleRock(sceneNode, pos, mats?.DecoRock?.AlbedoColor ?? new Color(0.35f, 0.33f, 0.30f));
					else
						MakeSimpleRuin(sceneNode, pos, mats?.DecoRuin?.AlbedoColor ?? new Color(0.28f, 0.24f, 0.20f));
					break;

				case TileType.Rock:
					if (roll < 0.7f)
						MakeSimpleRock(sceneNode, pos, mats?.DecoRock?.AlbedoColor ?? new Color(0.35f, 0.33f, 0.30f));
					else
						MakeSimpleTree(sceneNode, pos, mats?.DecoTree?.AlbedoColor ?? new Color(0.15f, 0.40f, 0.10f));
					break;

				case TileType.Sand:
				case TileType.Swamp:
					if (roll < 0.5f)
						MakeSimpleRock(sceneNode, pos, mats?.DecoRock?.AlbedoColor ?? new Color(0.35f, 0.33f, 0.30f));
					else
						MakeSimpleGrassTuft(sceneNode, pos);
					break;

				case TileType.Water:
				case TileType.Snow:
				case TileType.Path:
					continue;

				default:
					if (roll < 0.5f)
						MakeSimpleRock(sceneNode, pos, mats?.DecoRock?.AlbedoColor ?? new Color(0.35f, 0.33f, 0.30f));
					else
						MakeSimpleGrassTuft(sceneNode, pos);
					break;
			}
		}
	}

	void MakeSimpleTree(Node parent, Vector3 pos, Color color)
	{
		float scale = 0.8f + (GD.Randi() % 10) * 0.04f;
		float rotDeg = (GD.Randi() % 20) - 10;

		var sprite = new Sprite3D
		{
			Texture = MakeSimpleTreeTexture(color),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = pos + new Vector3(0, 0.45f * scale, 0),
			PixelSize = 0.028f * scale,
			Modulate = Colors.White,
			RotationDegrees = new Vector3(0, rotDeg, 0)
		};
		parent.AddChild(sprite);
	}

	void MakeSimpleRock(Node parent, Vector3 pos, Color color)
	{
		float scale = 0.7f + (GD.Randi() % 7) * 0.05f;

		var sprite = new Sprite3D
		{
			Texture = MakeSimpleRockTexture(color),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = pos + new Vector3(0, 0.08f * scale, 0),
			PixelSize = 0.022f * scale,
			Modulate = Colors.White
		};
		parent.AddChild(sprite);
	}

	void MakeSimpleBush(Node parent, Vector3 pos, Color color)
	{
		float scale = 0.6f + (GD.Randi() % 5) * 0.06f;
		float rotDeg = (GD.Randi() % 30) - 15;

		var sprite = new Sprite3D
		{
			Texture = MakeSimpleBushTexture(color),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = pos + new Vector3(0, 0.06f * scale, 0),
			PixelSize = 0.020f * scale,
			Modulate = Colors.White,
			RotationDegrees = new Vector3(0, rotDeg, 0)
		};
		parent.AddChild(sprite);
	}

	void MakeSimpleGrassTuft(Node parent, Vector3 pos)
	{
		float scale = 0.5f + (GD.Randi() % 4) * 0.08f;
		float rotDeg = (GD.Randi() % 360);

		var sprite = new Sprite3D
		{
			Texture = MakeSimpleGrassTuftTexture(),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = pos + new Vector3(0, 0.02f, 0),
			PixelSize = 0.012f * scale,
			Modulate = Colors.White,
			RotationDegrees = new Vector3(0, rotDeg, 0)
		};
		parent.AddChild(sprite);
	}

	void MakeSimpleRuin(Node parent, Vector3 pos, Color color)
	{
		float rotDeg = (GD.Randi() % 15) - 7;

		var sprite = new Sprite3D
		{
			Texture = MakeSimpleRuinTexture(color),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = pos + new Vector3(0, 0.40f, 0),
			PixelSize = 0.030f,
			Modulate = Colors.White,
			RotationDegrees = new Vector3(0, rotDeg, 0)
		};
		parent.AddChild(sprite);
	}

	// ── Simple procedural textures for decorations ────────────────────

	static ImageTexture MakeSimpleTreeTexture(Color canopyColor)
	{
		int w = 16, h = 20;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color trunk = new Color(0.30f, 0.20f, 0.10f);
		Color darkCanopy = new Color(canopyColor.R * 0.7f, canopyColor.G * 0.7f, canopyColor.B * 0.7f);

		for (int y = 0; y <= 12; y++)
		{
			int halfW = 1 + (12 - y) / 2;
			for (int x = w / 2 - halfW; x <= w / 2 + halfW; x++)
			{
				if (x < 0 || x >= w) continue;
				Color c = ((x + y) % 3 == 0) ? darkCanopy : canopyColor;
				img.SetPixel(x, y, c);
			}
		}

		for (int y = 13; y < h; y++)
		{
			img.SetPixel(w / 2 - 1, y, trunk);
			img.SetPixel(w / 2, y, trunk);
		}

		return ImageTexture.CreateFromImage(img);
	}

	static ImageTexture MakeSimpleRockTexture(Color baseColor)
	{
		int w = 12, h = 10;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color dark = new Color(baseColor.R * 0.7f, baseColor.G * 0.7f, baseColor.B * 0.7f);
		Color light = new Color(
			Mathf.Min(baseColor.R * 1.2f, 1f),
			Mathf.Min(baseColor.G * 1.2f, 1f),
			Mathf.Min(baseColor.B * 1.2f, 1f));

		int cy = h / 2;
		for (int y = 1; y < h - 1; y++)
		{
			int halfW = 3 + Mathf.Abs(y - cy) / 2;
			for (int x = w / 2 - halfW; x <= w / 2 + halfW; x++)
			{
				if (x < 0 || x >= w) continue;
				if ((x + y) % 3 == 0)
					img.SetPixel(x, y, light);
				else if ((x + y) % 4 == 0)
					img.SetPixel(x, y, dark);
				else
					img.SetPixel(x, y, baseColor);
			}
		}

		return ImageTexture.CreateFromImage(img);
	}

	static ImageTexture MakeSimpleBushTexture(Color baseColor)
	{
		int w = 8, h = 6;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color dark = new Color(baseColor.R * 0.7f, baseColor.G * 0.7f, baseColor.B * 0.7f);
		int cy = h / 2;

		for (int y = 0; y < h; y++)
		{
			int dy = Mathf.Abs(y - cy);
			int halfW = 2 - dy / 2;
			if (y == 0 || y == h - 1) halfW = 1;
			for (int x = w / 2 - halfW; x <= w / 2 + halfW; x++)
			{
				if (x < 0 || x >= w) continue;
				Color c = ((x + y) % 3 == 0) ? dark : baseColor;
				img.SetPixel(x, y, c);
			}
		}

		return ImageTexture.CreateFromImage(img);
	}

	static ImageTexture MakeSimpleGrassTuftTexture()
	{
		int w = 4, h = 6;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color grass = new Color(0.16f, 0.40f, 0.12f);
		Color light = new Color(0.22f, 0.48f, 0.18f);

		img.SetPixel(0, 0, light);
		img.SetPixel(0, 1, grass);

		img.SetPixel(2, 0, light);
		img.SetPixel(2, 1, grass);
		img.SetPixel(2, 2, grass);

		img.SetPixel(1, 0, grass);
		img.SetPixel(1, 1, light);

		return ImageTexture.CreateFromImage(img);
	}

	static ImageTexture MakeSimpleRuinTexture(Color baseColor)
	{
		int w = 8, h = 16;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color dark = new Color(baseColor.R * 0.6f, baseColor.G * 0.6f, baseColor.B * 0.6f);
		Color light = new Color(
			Mathf.Min(baseColor.R * 1.15f, 1f),
			Mathf.Min(baseColor.G * 1.15f, 1f),
			Mathf.Min(baseColor.B * 1.15f, 1f));

		for (int y = 2; y < h; y++)
		{
			int halfW = (y < 6) ? 2 : 3;
			for (int x = w / 2 - halfW; x <= w / 2 + halfW; x++)
			{
				if (x < 0 || x >= w) continue;
				if ((x + y) % 3 == 0)
					img.SetPixel(x, y, light);
				else if ((x == w / 2 - halfW || x == w / 2 + halfW) && y > 8)
					img.SetPixel(x, y, dark);
				else
					img.SetPixel(x, y, baseColor);
			}
		}

		return ImageTexture.CreateFromImage(img);
	}
}
