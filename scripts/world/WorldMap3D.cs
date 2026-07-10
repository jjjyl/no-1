namespace No1.World;
using System.Collections.Generic;
using Godot;
using No1.Core;
using No1.UI;

/// <summary>
/// 3D world map — tilted desktop view with Sprite3D billboards.
/// All visual elements are Sprite3D, materials from WorldMaterials.
/// Player click-to-moves on XZ plane. Camera follows with zoom/rotate.
/// </summary>
public partial class WorldMap3D : Node3D
{
	// ── World scale ──
	const float Scale2D = 0.01f; // pixels → meters

	// ── World config ──
	[Export] public float WorldWidth = 20f;
	[Export] public float WorldHeight = 15f;

	// ── Camera ──
	[Export] public Camera3D.ProjectionType CamProjection = Camera3D.ProjectionType.Perspective;
	[Export] public float CameraDistance = 8f;
	[Export] public float CameraPitch = 50f;
	[Export] public float CameraYaw = 0f;            // 0=North(面朝Z轴), 90=East(面朝X轴)
	public static float StaticCameraYaw;
	[Export] public float CameraZoomMin = 3f;
	[Export] public float CameraZoomMax = 18f;
	[Export] public float CameraZoomStep = 1.5f;
	[Export] public float CameraFollowSpeed = 5f;


	// ── Terrain ──
	[Export] public float ZoneDefaultWidth = 4.2f;
	[Export] public float ZoneDefaultHeight = 3.2f;
	[Export] public float PathWidth = 0.5f;

	// ── Decorations ──
	[Export] public int TreeCount = 60;
	[Export] public int RockCount = 25;
	[Export] public int RuinCount = 8;
	[Export] public int ScatterCount = 20;
	[Export] public int DecorationSeed = 42;

	// ── Enemy ──
	[Export] public int EnemyDotCount = 3;

	// ── WorldData-driven architecture ──
	WorldData _worldData;
	ChunkManager _chunkManager;
	List<RegionNode> _regionNodes = new();

	// ── Player ──
	/// <summary>Assign a player.tscn (root=Player3D) in editor; falls back to code-built Player3D if null.</summary>
	[Export] public PackedScene PlayerScene;
	[Export] public SpriteFrames PlayerSpriteFrames;

	// ── Runtime state ──
	Player3D _player;
	Camera3D _camera;
	int _currentZone = -1;
	bool _combatPending;
	Node3D _cameraPivot;

	// Camera control

	public override void _Ready()
	{
		StaticCameraYaw = CameraYaw;
		GD.Print("[WorldMap3D] _Ready start");
		_worldData = GameManager.CurrentWorldData;
		if (_worldData == null)
		{
			GD.PrintErr("[WorldMap3D] No WorldData! Cannot build world.");
			BuildCamera();
			BuildReturnButton();
			return;
		}

		GD.Print($"[WorldMap3D] WorldData loaded: seed={_worldData.Seed}, regions={_worldData.Regions?.Length ?? 0}");
		_currentZone = CycleManager.Instance.CurrentRegionIndex;

		var mats = new WorldMaterials();
		AddChild(mats);

		var terrainNode = new Node3D { Name = "Terrain" };
		AddChild(terrainNode);
		_chunkManager = new ChunkManager { Name = "ChunkManager" };
		AddChild(_chunkManager);
		_chunkManager.Init(_worldData, terrainNode);

		BuildParallax();
		GD.Print("[WorldMap3D] Parallax done");
		BuildParticles();
		GD.Print("[WorldMap3D] Particles done");
		BuildRegions();
		GD.Print("[WorldMap3D] Regions done");
		BuildEnemyPlaceholders();
		GD.Print("[WorldMap3D] EnemyPlaceholders done");
		BuildShopNPC();
		BuildCamera();
		GD.Print("[WorldMap3D] Camera done");
		BuildPlayer();
		GD.Print($"[WorldMap3D] Player at {_player.GlobalPosition}");
		_chunkManager.Player = _player;
		BuildReturnButton();
		GD.Print("[WorldMap3D] _Ready complete");
	}

	public override void _Process(double delta)
	{
		UpdateCamera((float)delta);

		if (_player != null && _chunkManager != null)
		{
			float groundY = _chunkManager.GetHeightAt(_player.Position.X, _player.Position.Z);
			_player.Position = new Vector3(_player.Position.X, groundY, _player.Position.Z);
		}

		if (!_combatPending) return;
		if (DialogueManager.IsFullDialogueActive()) return;

		_combatPending = false;
		CycleManager.Instance.LastWorldPosition = _player?.GlobalPosition ?? Vector3.Zero;
		GameManager.Instance.GoToScene(GameManager.SceneCombat);
		CycleManager.Instance.PendingBattleEvents = "res://assets/data/battle_events.json";
	}

	// ═══════════════════════════════════════════════════════════════
	//  Camera — tilted desktop view with follow, zoom, rotation
	// ═══════════════════════════════════════════════════════════════

	void BuildCamera()
	{
		// Pivot orbits around player; camera is child at fixed pitch offset
		_cameraPivot = new Node3D { Name = "CameraPivot" };
		AddChild(_cameraPivot);

		_camera = new Camera3D
		{
			Name = "Camera3D",
			Projection = CamProjection,
		};
		_cameraPivot.AddChild(_camera);

		// Initial position: behind and above, looking down ~45°
		UpdateCameraTransform();
		_camera.MakeCurrent();
	}

	void UpdateCamera(float delta)
	{
		if (_player == null) return;

		// Smooth follow
		var targetPos = _player.GlobalPosition;
		_cameraPivot.GlobalPosition = _cameraPivot.GlobalPosition.Lerp(
			targetPos, delta * CameraFollowSpeed);

		UpdateCameraTransform();
	}

	void UpdateCameraTransform()
	{
		float pitchRad = Mathf.DegToRad(CameraPitch);
		float yawRad = Mathf.DegToRad(CameraYaw);

		float x = Mathf.Cos(pitchRad) * Mathf.Sin(yawRad);
		float y = Mathf.Sin(pitchRad);
		float z = Mathf.Cos(pitchRad) * Mathf.Cos(yawRad);

		_camera.Position = new Vector3(x, y, z) * CameraDistance;
		_camera.LookAt(_cameraPivot.GlobalPosition, Vector3.Up);
	}

	public override void _Input(InputEvent e)
	{
		if (No1.Debug.DebugConsole.IsOpen) return;

		// ── Scroll zoom ──

		if (e is InputEventMouseButton mb)
		{
			if (mb.ButtonIndex == MouseButton.WheelUp)
				CameraDistance = Mathf.Clamp(CameraDistance - CameraZoomStep, CameraZoomMin, CameraZoomMax);
			else if (mb.ButtonIndex == MouseButton.WheelDown)
				CameraDistance = Mathf.Clamp(CameraDistance + CameraZoomStep, CameraZoomMin, CameraZoomMax);
		}

		// Let player handle left-click (don't consume here)
	}

	// ═══════════════════════════════════════════════════════════════
	//  Parallax — layered Sprite3D billboards at Z depths
	// ═══════════════════════════════════════════════════════════════

	void BuildParallax()
	{
		var layer = new Node3D { Name = "Parallax" };
		var rng = new RandomNumberGenerator();
		rng.Seed = 1234;

		// Layer 0 (z=-15): Full-screen sky gradient
		var skyTexW = 256; var skyTexH = 128;
		var skyTex = WorldTextures.MakeSkyGradientTexture(skyTexW, skyTexH);
		float skyPixelSize = Mathf.Max(WorldWidth * 2f / skyTexW, WorldHeight * 2f / skyTexH);
		var skySprite = new Sprite3D
		{
			Name = "SkyGradient",
			Texture = WorldTextures.TryLoadTexture("res://assets/texture/world/sky_gradient.png") ?? skyTex,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = new Vector3(WorldWidth * 0.5f, WorldHeight * 0.45f, -15f),
			PixelSize = skyPixelSize,
			Modulate = Colors.White
		};
		layer.AddChild(skySprite);

		// Layer 1 (z=-12): Pixel-art clouds
		int cloudCount = rng.RandiRange(3, 5);
		for (int i = 0; i < cloudCount; i++)
		{
			float cx = rng.RandfRange(1f, WorldWidth - 1f);
			float cy = rng.RandfRange(WorldHeight * 0.55f, WorldHeight * 0.85f);
			float cs = rng.RandfRange(0.5f, 1.3f);
			var cloudTex = WorldTextures.MakePixelCloudTexture();
			var cloud = new Sprite3D
			{
				Name = $"Cloud_{i}",
				Texture = WorldTextures.TryLoadTexture("res://assets/texture/world/cloud.png") ?? cloudTex,
				Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
				Position = new Vector3(cx, cy, -12f),
				PixelSize = 0.008f * cs,
				Modulate = new Color(1f, 1f, 1f, 0.85f)
			};
			layer.AddChild(cloud);
		}

		// Layer 2 (z=-8): Far mountain range with snow caps
		var farMtnTex = WorldTextures.MakeMountainRangeTexture(256, 48,
			new Color(0.22f, 0.27f, 0.38f),
			new Color(0.92f, 0.93f, 0.98f), 701);
		var farMountains = new Sprite3D
		{
			Name = "FarMountains",
			Texture = WorldTextures.TryLoadTexture("res://assets/texture/world/mountain_far.png") ?? farMtnTex,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = new Vector3(WorldWidth * 0.5f, WorldHeight * 0.18f, -8f),
			PixelSize = 0.009f,
			Modulate = Colors.White
		};
		layer.AddChild(farMountains);

		// Layer 3 (z=-5): Near mountain range — darker, slightly lower
		var nearMtnTex = WorldTextures.MakeMountainRangeTexture(256, 40,
			new Color(0.15f, 0.17f, 0.25f),
			new Color(0.78f, 0.80f, 0.88f), 149);
		var nearMountains = new Sprite3D
		{
			Name = "NearMountains",
			Texture = WorldTextures.TryLoadTexture("res://assets/texture/world/mountain_near.png") ?? nearMtnTex,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = new Vector3(WorldWidth * 0.5f, WorldHeight * 0.13f, -5f),
			PixelSize = 0.009f,
			Modulate = Colors.White
		};
		layer.AddChild(nearMountains);

		// Sun — pixel-art sun with dithered edges, upper right
		var sunTex = WorldTextures.MakePixelSunTexture(32);
		var sun = new Sprite3D
		{
			Name = "Sun",
			Texture = WorldTextures.TryLoadTexture("res://assets/texture/world/sun.png") ?? sunTex,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = new Vector3(WorldWidth - 2.5f, WorldHeight - 1.8f, -13f),
			PixelSize = 0.005f,
			Modulate = Colors.White
		};
		layer.AddChild(sun);

		// Layer 4 (z=-4): Dragon shadow — pixel-art winged silhouette
		var dragonTex = WorldTextures.MakeDragonSilhouetteTexture(64, 20);
		var dragon = new Sprite3D
		{
			Name = "DragonShadow",
			Texture = WorldTextures.TryLoadTexture("res://assets/texture/world/dragon_shadow.png") ?? dragonTex,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = new Vector3(-5f, 5f, -4f),
			PixelSize = 0.006f,
			Modulate = new Color(0, 0, 0, 0.35f)
		};
		layer.AddChild(dragon);
		AnimateDragon(dragon);

		AddChild(layer);
	}

	Sprite3D AddBillboard(Node parent, string name, Vector3 pos, Vector2 size, Material mat, float texW = 16, float texH = 16)
	{
		var color = mat is StandardMaterial3D sm ? sm.AlbedoColor : Colors.White;
		var sprite = new Sprite3D
		{
			Name = name,
			Position = pos,
			Texture = WorldTextures.MakeColorTexture(color, (int)texW, (int)texH),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			PixelSize = Mathf.Max(size.X / texW, size.Y / texH),
			Modulate = color
		};
		parent.AddChild(sprite);
		return sprite;
	}

	async void AnimateDragon(Sprite3D shadow)
	{
		while (IsInsideTree())
		{
			await ToSignal(GetTree().CreateTimer(8 + GD.Randf() * 10), "timeout");
			if (!IsInsideTree()) return;

			shadow.Position = new Vector3(-6f, shadow.Position.Y, shadow.Position.Z);
			var tween = CreateTween();
			tween.TweenProperty(shadow, "position:x", WorldWidth + 3f, 12f);
			await ToSignal(tween, "finished");
		}
	}

	// ═══════════════════════════════════════════════════════════════
	//  Terrain — now driven by ChunkManager (data-driven from WorldData)
	// ═══════════════════════════════════════════════════════════════

	// ═══════════════════════════════════════════════════════════════
	//  Region nodes — data-driven from WorldData.Regions
	// ═══════════════════════════════════════════════════════════════

	void BuildRegions()
	{
		var regionsParent = new Node3D { Name = "Regions" };
		for (int i = 0; i < _worldData.Regions.Length; i++)
		{
			var rn = new RegionNode();
			rn.Initialize(_worldData.Regions[i], i);
			rn.CombatPending += () =>
			{
				if (CycleManager.Instance.SkipStartEvents)
				{
					CycleManager.Instance.SkipStartEvents = false;
					return;
				}
				_combatPending = true;
			};
			regionsParent.AddChild(rn);
			_regionNodes.Add(rn);
		}
		AddChild(regionsParent);
	}

	void MakeTree(Node parent, Vector3 pos, float scale, Material mat)
	{
		var color = mat is StandardMaterial3D sm ? sm.AlbedoColor : Colors.Green;
		float rotDeg = (GD.Randi() % 20) - 10;

		var sprite = new Sprite3D
		{
			Texture = WorldTextures.TryLoadTexture("res://assets/texture/world/deco_tree.png") ?? WorldTextures.MakePixelTreeTexture(color),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = pos + new Vector3(0, 0.50f * scale, 0),
			PixelSize = 0.030f * scale,
			Modulate = Colors.White,
			RotationDegrees = new Vector3(0, rotDeg, 0)
		};
		parent.AddChild(sprite);
	}

	void MakeRock(Node parent, Vector3 pos, float scale, Material mat)
	{
		var color = mat is StandardMaterial3D sm ? sm.AlbedoColor : Colors.Gray;
		int variant = (int)(GD.Randi() % 3);

		var sprite = new Sprite3D
		{
			Texture = WorldTextures.TryLoadTexture($"res://assets/texture/world/deco_rock_{variant}.png") ?? WorldTextures.MakePixelRockTexture(color, variant),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = pos + new Vector3(0, 0.12f * scale, 0),
			PixelSize = 0.030f * scale,
			Modulate = Colors.White
		};
		parent.AddChild(sprite);
	}

	void MakeRuin(Node parent, Vector3 pos, Material mat)
	{
		var color = mat is StandardMaterial3D sm ? sm.AlbedoColor : new Color(0.28f, 0.24f, 0.20f);
		int variant = (int)(GD.Randi() % 2);

		var sprite = new Sprite3D
		{
			Texture = WorldTextures.TryLoadTexture($"res://assets/texture/world/deco_ruin_{variant}.png") ?? WorldTextures.MakePixelRuinTexture(color, variant),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = pos + new Vector3(0, 0.42f, 0),
			PixelSize = 0.035f,
			Modulate = Colors.White
		};
		parent.AddChild(sprite);
	}

	void MakeBush(Node parent, Vector3 pos, float scale, Material mat)
	{
		var color = mat is StandardMaterial3D sm ? sm.AlbedoColor : Colors.Green;

		var sprite = new Sprite3D
		{
			Texture = WorldTextures.TryLoadTexture("res://assets/texture/world/deco_bush.png") ?? WorldTextures.MakePixelBushTexture(color),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = pos + new Vector3(0, 0.08f * scale, 0),
			PixelSize = 0.025f * scale,
			Modulate = Colors.White
		};
		parent.AddChild(sprite);
	}

	// ═══════════════════════════════════════════════════════════════
	//  Particles — zone-specific ambient effects
	// ═══════════════════════════════════════════════════════════════

	void BuildParticles()
	{
		var particlesNode = new Node3D { Name = "Particles" };
		if (_worldData?.Regions == null) { AddChild(particlesNode); return; }

		foreach (var region in _worldData.Regions)
		{
			Vector3 pos = RegionToWorldPos(region);
			switch (region.Id)
			{
				case "forest_edge":
					AddLeafParticles(particlesNode, pos);
					break;
				case "abandoned_mine":
					AddDustParticles(particlesNode, pos);
					break;
				case "crystal_cave":
					AddSparkleParticles(particlesNode, pos);
					break;
			}
		}

		AddChild(particlesNode);
	}

	Vector3 RegionToWorldPos(RegionPlacement region)
	{
		return new Vector3(
			region.TileX * WorldConstants.TileSizeMeters,
			0,
			region.TileY * WorldConstants.TileSizeMeters);
	}

	void AddLeafParticles(Node parent, Vector3 center)
	{
		var gp = new GpuParticles3D
		{
			Name = "LeafParticles",
			Amount = 30,
			Lifetime = 4.5f,
			AmountRatio = 1.0f,
			VisibilityAabb = new Aabb(center, new Vector3(8, 3, 8)),
			DrawPass1 = WorldTextures.MakeParticleQuadMesh(new Color(0.22f, 0.58f, 0.16f, 0.85f), 0.05f)
		};

		var mat = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
			EmissionBoxExtents = new Vector3(3f, 0.5f, 3f),
			Gravity = new Vector3(0.15f, -0.45f, 0),
			InitialVelocityMin = 0.2f,
			InitialVelocityMax = 0.7f,
			AngleMin = 0,
			AngleMax = 360,
			ScaleMin = 0.5f,
			ScaleMax = 1.5f,
			LifetimeRandomness = 0.35f,
			Direction = new Vector3(0, -1, 0),
			Spread = 50,
			DampingMin = 0.04f,
			DampingMax = 0.12f
		};
		gp.ProcessMaterial = mat;
		gp.Position = center;
		parent.AddChild(gp);
	}

	void AddDustParticles(Node parent, Vector3 center)
	{
		var gp = new GpuParticles3D
		{
			Name = "DustParticles",
			Amount = 20,
			Lifetime = 6.0f,
			AmountRatio = 1.0f,
			VisibilityAabb = new Aabb(center, new Vector3(6, 2, 6)),
			DrawPass1 = WorldTextures.MakeParticleQuadMesh(new Color(0.55f, 0.48f, 0.38f, 0.45f), 0.04f)
		};

		var mat = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
			EmissionBoxExtents = new Vector3(2f, 0.25f, 2f),
			Gravity = Vector3.Zero,
			InitialVelocityMin = 0.05f,
			InitialVelocityMax = 0.20f,
			AngleMin = 0,
			AngleMax = 360,
			ScaleMin = 0.6f,
			ScaleMax = 1.4f,
			LifetimeRandomness = 0.5f,
			Direction = new Vector3(0, 1, 0),
			Spread = 120,
			DampingMin = 0.30f,
			DampingMax = 0.50f
		};
		gp.ProcessMaterial = mat;
		gp.Position = center;
		parent.AddChild(gp);
	}

	void AddSparkleParticles(Node parent, Vector3 center)
	{
		var gp = new GpuParticles3D
		{
			Name = "SparkleParticles",
			Amount = 15,
			Lifetime = 1.0f,
			AmountRatio = 1.0f,
			VisibilityAabb = new Aabb(center, new Vector3(5, 3, 5)),
			DrawPass1 = WorldTextures.MakeParticleQuadMesh(new Color(0.60f, 0.85f, 1.0f, 0.90f), 0.04f)
		};

		var mat = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = 2f,
			Gravity = Vector3.Zero,
			InitialVelocityMin = 0.1f,
			InitialVelocityMax = 0.5f,
			AngleMin = 0,
			AngleMax = 360,
			ScaleMin = 0.4f,
			ScaleMax = 1.3f,
			LifetimeRandomness = 0.5f,
			Direction = new Vector3(0, 1, 0),
			Spread = 80,
			DampingMin = 0.01f,
			DampingMax = 0.05f
		};
		gp.ProcessMaterial = mat;
		gp.Position = center;
		parent.AddChild(gp);
	}

	// ═══════════════════════════════════════════════════════════════
	//  Region triggers — now handled by RegionNode (Area3D + signal)
	// ═══════════════════════════════════════════════════════════════

	// ═══════════════════════════════════════════════════════════════
	//  Enemy placeholders — data-driven from WorldData chunks
	// ═══════════════════════════════════════════════════════════════

	void BuildEnemyPlaceholders()
	{
		var enemies = new Node3D { Name = "Enemies" };
		if (_worldData?.Chunks == null) return;

		var seen = new HashSet<string>();
		for (int ci = 0; ci < _worldData.Chunks.Length; ci++)
		{
			var chunk = _worldData.Chunks[ci];
			if (chunk?.Entities == null) continue;
			foreach (var entity in chunk.Entities)
			{
				if (entity.Type != EntityType.Enemy) continue;
				if (entity.State != 0) continue;

				string key = $"{entity.Id}_{entity.TileX}_{entity.TileY}";
				if (!seen.Add(key)) continue;

				float wx = entity.TileX * WorldConstants.TileSizeMeters;
				float wz = entity.TileY * WorldConstants.TileSizeMeters;
				AddEnemyDot(enemies, new Vector3(wx, 0.01f, wz), entity.Id);
			}
		}
		AddChild(enemies);
	}

	void AddEnemyDot(Node parent, Vector3 pos, string enemyId)
	{
		var def = EnemyState.Get(enemyId);
		Color color = WorldMaterials.Instance.EnemyDot.AlbedoColor;
		string name = enemyId;

		if (def != null)
		{
			name = def.Name;
			color = def.Category switch
			{
				"boss"  => new Color(0.9f, 0.2f, 0.1f),
				"elite" => new Color(1f, 0.7f, 0.1f),
				_       => WorldMaterials.Instance.EnemyDot.AlbedoColor,
			};
		}

		var dot = new Sprite3D
		{
			Name = name,
			Texture = WorldTextures.MakeCircleTexture(color),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = pos,
			PixelSize = 0.005f,
			Modulate = color
		};
		dot.SetMeta("enemy_id", enemyId);
		parent.AddChild(dot);
	}

	Vector3? FindMerchantPosition()
	{
		if (_worldData?.Chunks == null) return null;
		foreach (var chunk in _worldData.Chunks)
		{
			if (chunk?.Entities == null) continue;
			foreach (var entity in chunk.Entities)
			{
				if (entity.Type == EntityType.NPC && entity.Id == "merchant")
					return new Vector3(
						entity.TileX * WorldConstants.TileSizeMeters,
						0,
						entity.TileY * WorldConstants.TileSizeMeters);
			}
		}
		return null;
	}

	// ═══════════════════════════════════════════════════════════════
	//  Shop NPC
	// ═══════════════════════════════════════════════════════════════

	void BuildShopNPC()
	{
		var npc = new ShopNPC { Name = "ShopNPC" };
		npc.Position = FindMerchantPosition() ?? new Vector3(13f, 0f, 7.5f);

		var sprite = new Sprite3D
		{
			Name = "Sprite",
			Texture = WorldTextures.MakeCircleTexture(new Color(1f, 0.85f, 0.3f)),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			PixelSize = 0.008f,
			Modulate = new Color(1f, 0.85f, 0.3f)
		};
		npc.AddChild(sprite);

		var label = new Label3D
		{
			Name = "Label",
			Text = "商人",
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			PixelSize = 0.004f,
			Position = new Vector3(0f, 0.3f, 0f),
			Modulate = new Color(1f, 0.85f, 0.3f)
		};
		npc.AddChild(label);

		var area = new Area3D { Name = "Trigger" };
		var shape = new CollisionShape3D();
		shape.Shape = new BoxShape3D { Size = new Vector3(2f, 2f, 2f) };
		area.AddChild(shape);
		npc.AddChild(area);

		AddChild(npc);
	}

	// ═══════════════════════════════════════════════════════════════
	//  Player
	// ═══════════════════════════════════════════════════════════════

	void BuildPlayer()
	{
		if (PlayerScene != null)
		{
			_player = PlayerScene.Instantiate<Player3D>();
			_player.Name = "Player";
		}
		else
		{
			_player = new Player3D { Name = "Player" };
		}
		var savedPos = CycleManager.Instance.LastWorldPosition;
		if (savedPos != Vector3.Zero)
		{
			_player.Position = savedPos;
			CycleManager.Instance.LastWorldPosition = Vector3.Zero;
		}
		else
		{
			int startIdx = CycleManager.Instance.CurrentRegionIndex;
			if (_worldData?.Regions?.Length > startIdx)
			{
				var region = _worldData.Regions[startIdx];
				_player.Position = new Vector3(
					region.TileX * WorldConstants.TileSizeMeters,
					0,
					region.TileY * WorldConstants.TileSizeMeters);
			}
			else
			{
				_player.Position = new Vector3(WorldWidth * 0.5f, 0, WorldHeight * 0.5f);
			}
		}
		AddChild(_player);
	}

	// ═══════════════════════════════════════════════════════════════
	//  Zone labels — now handled by RegionNode (Label3D billboard)
	// ═══════════════════════════════════════════════════════════════

	// ═══════════════════════════════════════════════════════════════
	//  UI — CanvasLayer buttons (identical to 2D version)
	// ═══════════════════════════════════════════════════════════════

	void BuildReturnButton()
	{
		var canvas = new CanvasLayer();
		float right = DisplayServer.WindowGetSize().X;

		var invBtn = new Button
		{
			Text = "物品",
			Position = new Vector2(right - 320, 16),
			Size = new Vector2(140, 36)
		};
		invBtn.Pressed += () => DialogueManager.Instance.ShowCharacterPanel();
		canvas.AddChild(invBtn);

		var btn = new Button
		{
			Text = "返回神殿",
			Position = new Vector2(right - 160, 16),
			Size = new Vector2(140, 36)
		};
		btn.Pressed += () =>
		{
			CycleManager.Instance.ReturnToTemple();
			GameManager.Instance.GoToScene(GameManager.SceneTemple);
		};
		canvas.AddChild(btn);
		AddChild(canvas);
	}

}
