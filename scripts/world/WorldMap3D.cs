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
	[Export] public float CameraDistance = 15f;
	[Export] public float CameraPitch = 45f;
	[Export] public float CameraZoomMin = 5f;
	[Export] public float CameraZoomMax = 40f;
	[Export] public float CameraZoomStep = 1.5f;
	[Export] public float CameraFollowSpeed = 5f;
	[Export] public float CameraRotateSensitivity = 0.3f;
	[Export] public float FirstPersonSensitivity = 0.15f;

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

	// ── Runtime state ──
	Player3D _player;
	Camera3D _camera;
	int _currentZone = -1;
	bool _combatPending;
	Node3D _cameraPivot;

	// Camera control
	float _cameraYaw;    // horizontal rotation around world center
	bool _middleDragging;
	Vector2 _dragStart;

	// First-person mode
	bool _firstPerson;
	float _fpYaw;
	float _fpPitch;
	float _savedCamDistance;
	float _savedCamYaw;

	public override void _Ready()
	{
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

		GD.Print($"[DIAG] Player at ({_player.Position.X:F1},{_player.Position.Y:F1},{_player.Position.Z:F1})");
		GD.Print($"[DIAG] CameraDistance={CameraDistance} Pitch={CameraPitch} Yaw={_cameraYaw}");
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

		_camera = new Camera3D { Name = "Camera3D" };
		_cameraPivot.AddChild(_camera);

		// Initial position: behind and above, looking down ~45°
		UpdateCameraTransform();
		_camera.MakeCurrent();
	}

	void UpdateCamera(float delta)
	{
		if (_player == null) return;

		if (_firstPerson)
		{
			// Snap pivot to eye height, no lerp
			_cameraPivot.GlobalPosition = _player.GlobalPosition + new Vector3(0, 1.6f, 0);
			_cameraPivot.RotationDegrees = new Vector3(0, _fpYaw, 0);
			_camera.RotationDegrees = new Vector3(_fpPitch, 0, 0);
			_camera.Position = Vector3.Zero;
			return;
		}

		// Desktop: smooth follow
		var targetPos = _player.GlobalPosition;
		_cameraPivot.GlobalPosition = _cameraPivot.GlobalPosition.Lerp(
			targetPos, delta * CameraFollowSpeed);

		UpdateCameraTransform();
	}

	void UpdateCameraTransform()
	{
		float pitchRad = Mathf.DegToRad(CameraPitch);
		float yawRad = Mathf.DegToRad(_cameraYaw);

		float x = Mathf.Cos(pitchRad) * Mathf.Sin(yawRad);
		float y = Mathf.Sin(pitchRad);
		float z = Mathf.Cos(pitchRad) * Mathf.Cos(yawRad);

		_camera.Position = new Vector3(x, y, z) * CameraDistance;
		_camera.LookAt(_cameraPivot.GlobalPosition, Vector3.Up);
	}

	void ToggleFirstPerson()
	{
		_firstPerson = !_firstPerson;

		if (_firstPerson)
		{
			_savedCamDistance = CameraDistance;
			_savedCamYaw = _cameraYaw;
			_fpYaw = _cameraYaw;
			_fpPitch = 0f;
			Input.MouseMode = Input.MouseModeEnum.Captured;
			if (_player != null) _player.ProcessMode = ProcessModeEnum.Disabled;
		}
		else
		{
			CameraDistance = _savedCamDistance;
			_cameraYaw = _savedCamYaw;
			_cameraPivot.RotationDegrees = Vector3.Zero;
			_camera.RotationDegrees = Vector3.Zero;
			Input.MouseMode = Input.MouseModeEnum.Visible;
			if (_player != null) _player.ProcessMode = ProcessModeEnum.Inherit;
		}
	}

	public override void _Input(InputEvent e)
	{
		// ── Tab: toggle first-person observation ──
		if (e is InputEventKey key && key.Keycode == Key.Tab)
		{
			if (key.Pressed && !key.Echo)
				ToggleFirstPerson();
			return;
		}

		// ── First-person mouse look ──
		if (_firstPerson && e is InputEventMouseMotion mm)
		{
		_fpYaw   -= mm.Relative.X * FirstPersonSensitivity;
		_fpPitch -= mm.Relative.Y * FirstPersonSensitivity;
			_fpPitch  = Mathf.Clamp(_fpPitch, -89f, 89f);
			return;
		}

		// ── Desktop scroll zoom ──
		if (_firstPerson) return; // block desktop controls in FP

		if (e is InputEventMouseButton mb)
		{
			if (mb.ButtonIndex == MouseButton.WheelUp)
				CameraDistance = Mathf.Clamp(CameraDistance - CameraZoomStep, CameraZoomMin, CameraZoomMax);
			else if (mb.ButtonIndex == MouseButton.WheelDown)
				CameraDistance = Mathf.Clamp(CameraDistance + CameraZoomStep, CameraZoomMin, CameraZoomMax);

			if (mb.ButtonIndex == MouseButton.Middle)
			{
				_middleDragging = mb.Pressed;
				_dragStart = mb.Position;
			}
		}

		if (e is InputEventMouseMotion mm2 && _middleDragging)
			_cameraYaw -= mm2.Relative.X * CameraRotateSensitivity;

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
		var skyTex = MakeSkyGradientTexture(skyTexW, skyTexH);
		float skyPixelSize = Mathf.Max(WorldWidth * 2f / skyTexW, WorldHeight * 2f / skyTexH);
		var skySprite = new Sprite3D
		{
			Name = "SkyGradient",
			Texture = TryLoadTexture("res://assets/texture/world/sky_gradient.png") ?? skyTex,
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
			var cloudTex = MakePixelCloudTexture();
			var cloud = new Sprite3D
			{
				Name = $"Cloud_{i}",
				Texture = TryLoadTexture("res://assets/texture/world/cloud.png") ?? cloudTex,
				Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
				Position = new Vector3(cx, cy, -12f),
				PixelSize = 0.008f * cs,
				Modulate = new Color(1f, 1f, 1f, 0.85f)
			};
			layer.AddChild(cloud);
		}

		// Layer 2 (z=-8): Far mountain range with snow caps
		var farMtnTex = MakeMountainRangeTexture(256, 48,
			new Color(0.22f, 0.27f, 0.38f),
			new Color(0.92f, 0.93f, 0.98f), 701);
		var farMountains = new Sprite3D
		{
			Name = "FarMountains",
			Texture = TryLoadTexture("res://assets/texture/world/mountain_far.png") ?? farMtnTex,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = new Vector3(WorldWidth * 0.5f, WorldHeight * 0.18f, -8f),
			PixelSize = 0.009f,
			Modulate = Colors.White
		};
		layer.AddChild(farMountains);

		// Layer 3 (z=-5): Near mountain range — darker, slightly lower
		var nearMtnTex = MakeMountainRangeTexture(256, 40,
			new Color(0.15f, 0.17f, 0.25f),
			new Color(0.78f, 0.80f, 0.88f), 149);
		var nearMountains = new Sprite3D
		{
			Name = "NearMountains",
			Texture = TryLoadTexture("res://assets/texture/world/mountain_near.png") ?? nearMtnTex,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = new Vector3(WorldWidth * 0.5f, WorldHeight * 0.13f, -5f),
			PixelSize = 0.009f,
			Modulate = Colors.White
		};
		layer.AddChild(nearMountains);

		// Sun — pixel-art sun with dithered edges, upper right
		var sunTex = MakePixelSunTexture(32);
		var sun = new Sprite3D
		{
			Name = "Sun",
			Texture = TryLoadTexture("res://assets/texture/world/sun.png") ?? sunTex,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = new Vector3(WorldWidth - 2.5f, WorldHeight - 1.8f, -13f),
			PixelSize = 0.005f,
			Modulate = Colors.White
		};
		layer.AddChild(sun);

		// Layer 4 (z=-4): Dragon shadow — pixel-art winged silhouette
		var dragonTex = MakeDragonSilhouetteTexture(64, 20);
		var dragon = new Sprite3D
		{
			Name = "DragonShadow",
			Texture = TryLoadTexture("res://assets/texture/world/dragon_shadow.png") ?? dragonTex,
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
			Texture = MakeColorTexture(color, (int)texW, (int)texH),
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
			Texture = TryLoadTexture("res://assets/texture/world/deco_tree.png") ?? MakePixelTreeTexture(color),
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
			Texture = TryLoadTexture($"res://assets/texture/world/deco_rock_{variant}.png") ?? MakePixelRockTexture(color, variant),
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
			Texture = TryLoadTexture($"res://assets/texture/world/deco_ruin_{variant}.png") ?? MakePixelRuinTexture(color, variant),
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
			Texture = TryLoadTexture("res://assets/texture/world/deco_bush.png") ?? MakePixelBushTexture(color),
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

		if (_worldData?.Regions?.Length > 0)
			AddLeafParticles(particlesNode, RegionToWorldPos(_worldData.Regions[0]));

		if (_worldData?.Regions?.Length > 1)
			AddDustParticles(particlesNode, RegionToWorldPos(_worldData.Regions[1]));

		if (_worldData?.Regions?.Length > 4)
			AddSparkleParticles(particlesNode, RegionToWorldPos(_worldData.Regions[4]));

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
			DrawPass1 = MakeParticleQuadMesh(new Color(0.22f, 0.58f, 0.16f, 0.85f), 0.05f)
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
			DrawPass1 = MakeParticleQuadMesh(new Color(0.55f, 0.48f, 0.38f, 0.45f), 0.04f)
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
			DrawPass1 = MakeParticleQuadMesh(new Color(0.60f, 0.85f, 1.0f, 0.90f), 0.04f)
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

	static Mesh MakeParticleQuadMesh(Color color, float size)
	{
		var quad = new QuadMesh { Size = new Vector2(size, size) };
		var mat = new StandardMaterial3D
		{
			AlbedoColor = color,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};
		quad.Material = mat;
		return quad;
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
			Texture = MakeCircleTexture(color),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = pos,
			PixelSize = 0.005f,
			Modulate = color
		};
		dot.SetMeta("enemy_id", enemyId);
		parent.AddChild(dot);
	}

	// ═══════════════════════════════════════════════════════════════
	//  Shop NPC
	// ═══════════════════════════════════════════════════════════════

	void BuildShopNPC()
	{
		var npc = new ShopNPC { Name = "ShopNPC" };
		// Place near 废矿入口 zone
		npc.Position = new Vector3(13f, 0f, 7.5f);

		var sprite = new Sprite3D
		{
			Name = "Sprite",
			Texture = MakeCircleTexture(new Color(1f, 0.85f, 0.3f)),
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
		_player = new Player3D { Name = "Player" };
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

	// ═══════════════════════════════════════════════════════════════
	//  Texture helpers — programmatic colored squares for prototyping
	//  (Replace with real sprites/textures later)
	// ═══════════════════════════════════════════════════════════════

	static Texture2D TryLoadTexture(string path)
	{
		if (ResourceLoader.Exists(path))
		{
			try { var res = ResourceLoader.Load<Texture2D>(path); if (res != null) return res; }
			catch { }
		}
		if (FileAccess.FileExists(path))
		{
			try { var img = Image.LoadFromFile(path); if (img != null && !img.IsEmpty()) return ImageTexture.CreateFromImage(img); }
			catch { }
		}
		return null;
	}

	static ImageTexture MakeColorTexture(Color c, int w = 4, int h = 4)
	{
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(c);
		return ImageTexture.CreateFromImage(img);
	}

	static ImageTexture MakeCircleTexture(Color c, int size = 32)
	{
		var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));
		float half = size * 0.5f;
		for (int y = 0; y < size; y++)
		for (int x = 0; x < size; x++)
		{
			float dx = (x - half) / half;
			float dy = (y - half) / half;
			if (dx * dx + dy * dy <= 1f)
				img.SetPixel(x, y, c);
		}
		return ImageTexture.CreateFromImage(img);
	}

	static ImageTexture MakeTriangleTexture(int w, int h, Color c)
	{
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));
		for (int y = 0; y < h; y++)
		{
			float frac = (float)y / h;
			int left = (int)(w * frac * 0.5f);
			int right = w - left;
			for (int x = left; x < right; x++)
				img.SetPixel(x, y, c);
		}
		return ImageTexture.CreateFromImage(img);
	}

	// ═══════════════════════════════════════════════════════════════
	//  Pixel-art texture helpers
	// ═══════════════════════════════════════════════════════════════

	static ImageTexture MakePixelTreeTexture(Color canopyColor)
	{
		int w = 24, h = 32;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color trunk = new Color(0.30f, 0.20f, 0.10f);
		Color darkGreen = new Color(canopyColor.R * 0.60f, canopyColor.G * 0.60f, canopyColor.B * 0.60f);
		Color darkerGreen = new Color(canopyColor.R * 0.35f, canopyColor.G * 0.35f, canopyColor.B * 0.35f);

		for (int y = 2; y <= 17; y++)
		{
			int halfW;
			if (y <= 3) halfW = 2;
			else if (y <= 4) halfW = 3;
			else if (y <= 5) halfW = 4;
			else if (y <= 6) halfW = 5;
			else if (y <= 8) halfW = 6;
			else if (y <= 12) halfW = 7;
			else if (y <= 14) halfW = 6;
			else if (y <= 15) halfW = 5;
			else if (y <= 16) halfW = 3;
			else halfW = 2;

			for (int x = w / 2 - halfW; x <= w / 2 + halfW; x++)
			{
				if (x < 0 || x >= w) continue;
				Color c = canopyColor;
				int shade = (x * 7 + y * 13) % 8;
				if (shade == 0) c = darkerGreen;
				else if (shade == 1 || shade == 2) c = darkGreen;
				img.SetPixel(x, y, c);
			}
		}

		for (int y = 15; y <= 31; y++)
		{
			if (y >= 24)
			{
				img.SetPixel(10, y, trunk);
				img.SetPixel(11, y, trunk);
				img.SetPixel(12, y, trunk);
			}
			else if (y >= 18)
			{
				Color tc = (y % 2 == 0) ? trunk : new Color(trunk.R * 0.85f, trunk.G * 0.85f, trunk.B * 0.85f);
				img.SetPixel(11, y, tc);
				img.SetPixel(12, y, tc);
			}
			else
			{
				img.SetPixel(11, y, trunk);
			}
		}

		return ImageTexture.CreateFromImage(img);
	}

	static ImageTexture MakePixelRockTexture(Color baseColor, int variant)
	{
		int w = 16, h = 16;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color highlight = new Color(
			Mathf.Min(baseColor.R * 1.4f, 1f),
			Mathf.Min(baseColor.G * 1.4f, 1f),
			Mathf.Min(baseColor.B * 1.4f, 1f));
		Color shadow = new Color(baseColor.R * 0.55f, baseColor.G * 0.55f, baseColor.B * 0.55f);

		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(variant * 313 + baseColor.GetHashCode() & 0x7FFFFFFF);

		int cx = w / 2;
		int cy = h / 2;
		for (int y = 3; y <= 13; y++)
		{
			int maxHalf = 5 - Mathf.Abs(y - cy) / 2;
			if (variant == 1) maxHalf += (y % 3 == 0 ? 1 : 0);
			if (variant == 2) maxHalf += (y > cy ? 0 : 1);

			int left = cx - maxHalf;
			int right = cx + maxHalf;
			if (variant == 0) { left += (y - 3) / 4; right -= (y - 3) / 5; }

			for (int x = left; x <= right; x++)
			{
				if (x < 0 || x >= w || y < 0 || y >= h) continue;
				if (x == left && y < 11)
					img.SetPixel(x, y, highlight);
				else if (x >= right - 1 && y > 4)
					img.SetPixel(x, y, shadow);
				else
				{
					float jit = (rng.Randf() - 0.5f) * 0.15f;
					Color c = new Color(
						baseColor.R + jit,
						baseColor.G + jit,
						baseColor.B + jit);
					img.SetPixel(x, y, c);
				}
			}
		}

		return ImageTexture.CreateFromImage(img);
	}

	static ImageTexture MakePixelRuinTexture(Color baseColor, int variant)
	{
		int w = 16, h = 24;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color brick = new Color(baseColor.R * 1.15f, baseColor.G * 1.15f, baseColor.B * 1.15f);
		Color dark = new Color(baseColor.R * 0.55f, baseColor.G * 0.55f, baseColor.B * 0.55f);
		Color moss = new Color(0.10f, 0.28f, 0.08f);

		if (variant == 0)
		{
			for (int y = 0; y < h; y++)
			{
				int left = 4 + (y / 4);
				int right = 11 - (y / 5);
				if (y > 18) { left = 17; right = 16; }

				for (int x = left; x <= right && x < w; x++)
				{
					Color c = baseColor;
					if ((x + y) % 3 == 0) c = brick;
					if ((x == left || x == right) && (y % 6 > 3)) c = dark;
					if (y > 16 && (x + y) % 4 == 0) c = moss;
					img.SetPixel(x, y, c);
				}
			}
		}
		else
		{
			for (int y = 0; y < h; y++)
			{
				int pillarLeft = 3 + (y / 6);
				int pillarRight = 5;
				int archLeft = 5;
				int archRight = 11 - (y / 5);

				for (int x = pillarLeft; x <= pillarRight && x < w; x++)
				{
					if (y > 17) continue;
					Color c = baseColor;
					if ((x + y) % 3 == 0) c = brick;
					img.SetPixel(x, y, c);
				}
				for (int x = archLeft; x <= archRight && x < w; x++)
				{
					if (y > 17) continue;
					Color c = baseColor;
					if ((x + y) % 4 == 0) c = brick;
					if (x == archRight && (y % 5 > 2)) c = dark;
					if (y > 14 && (x + y) % 5 == 0) c = moss;
					img.SetPixel(x, y, c);
				}
			}
		}

		return ImageTexture.CreateFromImage(img);
	}

	static ImageTexture MakePixelBushTexture(Color baseColor)
	{
		int w = 8, h = 8;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color darkGreen = new Color(baseColor.R * 0.55f, baseColor.G * 0.55f, baseColor.B * 0.55f);
		Color highlight = new Color(
			Mathf.Min(baseColor.R * 1.3f, 1f),
			Mathf.Min(baseColor.G * 1.3f, 1f),
			Mathf.Min(baseColor.B * 1.3f, 1f));

		for (int y = 1; y <= 6; y++)
		{
			int halfW;
			if (y == 1) halfW = 1;
			else if (y == 2) halfW = 2;
			else if (y <= 4) halfW = 3;
			else if (y == 5) halfW = 2;
			else halfW = 1;

			for (int x = w / 2 - halfW; x <= w / 2 + halfW; x++)
			{
				if (x < 0 || x >= w) continue;
				if ((x + y) % 3 == 0)
					img.SetPixel(x, y, darkGreen);
				else if (y == 3 && x == w / 2 + halfW - 1)
					img.SetPixel(x, y, highlight);
				else
					img.SetPixel(x, y, baseColor);
			}
		}

		return ImageTexture.CreateFromImage(img);
	}

	static ImageTexture MakeGrassTuftTexture()
	{
		int w = 8, h = 4;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color grass = new Color(0.16f, 0.38f, 0.10f);
		Color bright = new Color(0.22f, 0.46f, 0.14f);

		img.SetPixel(3, 0, grass);
		img.SetPixel(3, 1, bright);

		img.SetPixel(5, 0, grass);
		img.SetPixel(5, 1, bright);
		img.SetPixel(5, 2, grass);

		img.SetPixel(1, 0, bright);
		img.SetPixel(1, 1, grass);

		return ImageTexture.CreateFromImage(img);
	}

	static ImageTexture MakeSkyGradientTexture(int w, int h)
	{
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);

		Color topColor = new Color(0.08f, 0.12f, 0.30f);
		Color midColor = new Color(0.18f, 0.30f, 0.55f);
		Color lowColor = new Color(0.45f, 0.62f, 0.85f);
		Color horizonColor = new Color(0.78f, 0.88f, 0.98f);

		for (int y = 0; y < h; y++)
		{
			float t = (float)y / h;
			Color c;
			if (t < 0.25f)
				c = topColor.Lerp(midColor, t / 0.25f);
			else if (t < 0.60f)
				c = midColor.Lerp(lowColor, (t - 0.25f) / 0.35f);
			else
				c = lowColor.Lerp(horizonColor, (t - 0.60f) / 0.40f);

			for (int x = 0; x < w; x++)
			{
				float dither = ((x + y) % 8 < 4) ? 0f : 0.02f;
				Color px = new Color(
					Mathf.Min(c.R + dither, 1f),
					Mathf.Min(c.G + dither, 1f),
					Mathf.Min(c.B + dither, 1f));
				img.SetPixel(x, y, px);
			}
		}

		return ImageTexture.CreateFromImage(img);
	}

	static ImageTexture MakePixelCloudTexture()
	{
		int w = 48, h = 24;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color white = new Color(1, 1, 1, 0.95f);
		Color offWhite = new Color(0.88f, 0.90f, 0.94f, 0.78f);
		Color edgeWhite = new Color(0.78f, 0.80f, 0.88f, 0.45f);

		(int x, int y, int r)[] blobs = new (int, int, int)[]
		{
			(18, 12, 8), (28, 10, 9), (22, 14, 7), (32, 13, 5)
		};

		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				float maxOverlap = 0;
				foreach (var (bx, by, br) in blobs)
				{
					float dx = x - bx;
					float dy = y - by;
					float dist = Mathf.Sqrt(dx * dx + dy * dy) / br;
					float overlap = 1f - dist;
					if (overlap > maxOverlap) maxOverlap = overlap;
				}

				if (maxOverlap > 0.72f)
					img.SetPixel(x, y, white);
				else if (maxOverlap > 0.40f)
					img.SetPixel(x, y, offWhite);
				else if (maxOverlap > 0.12f)
					img.SetPixel(x, y, edgeWhite);
			}
		}

		return ImageTexture.CreateFromImage(img);
	}

	static ImageTexture MakeMountainRangeTexture(int w, int h, Color bodyColor, Color snowColor, int seed)
	{
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)seed;

		int stepSize = w / 16;
		int[] heights = new int[w];
		int prevH = h / 3;

		for (int x = 0; x < w; x++)
		{
			if (x % stepSize == 0)
				prevH = rng.RandiRange(h / 4, h);
			heights[x] = prevH;
		}

		for (int x = 0; x < w; x++)
		{
			int mh = heights[x];
			mh += (x * seed + x * x * 3) % 5 - 2;
			mh = Mathf.Clamp(mh, 0, h);

			int snowStart = mh - h / 8;
			if (snowStart < 0) snowStart = 0;

			for (int y = 0; y < mh; y++)
			{
				int ry = h - 1 - y;
				if (y >= snowStart && mh > h / 2)
					img.SetPixel(x, ry, snowColor);
				else
					img.SetPixel(x, ry, bodyColor);
			}
		}

		return ImageTexture.CreateFromImage(img);
	}

	static ImageTexture MakePixelSunTexture(int size)
	{
		var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color sunYellow = new Color(1, 0.88f, 0.35f);
		Color ditherYellow = new Color(1, 0.82f, 0.30f, 0.6f);
		Color outerYellow = new Color(1, 0.75f, 0.25f, 0.30f);

		float half = size * 0.5f;
		int sunRadius = size / 2 - 2;

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float dx = x - half + 0.5f;
				float dy = y - half + 0.5f;
				float dist = Mathf.Sqrt(dx * dx + dy * dy);

				if (dist < sunRadius - 1)
				{
					img.SetPixel(x, y, sunYellow);
				}
				else if (dist < sunRadius + 1)
				{
					if ((x + y) % 2 == 0)
						img.SetPixel(x, y, ditherYellow);
				}
				else if (dist < sunRadius + 3)
				{
					if ((x + y) % 3 == 0)
						img.SetPixel(x, y, outerYellow);
				}
			}
		}

		return ImageTexture.CreateFromImage(img);
	}

	static ImageTexture MakeDragonSilhouetteTexture(int w, int h)
	{
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color body = new Color(0, 0, 0, 0.5f);
		Color wing = new Color(0, 0, 0, 0.2f);

		int center = h / 2;
		int bodyThickness = 2;

		int[] wingPositions = { 8, 22, 36, 50 };
		int[] wingSizes = { 7, 8, 7, 5 };

		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				bool pixel = false;

				if (y >= center - bodyThickness && y <= center + bodyThickness)
					pixel = true;

				for (int i = 0; i < wingPositions.Length; i++)
				{
					int wx = wingPositions[i];
					int ws = wingSizes[i];
					int dist = Mathf.Abs(x - wx);
					if (dist < ws)
					{
						int wingSpan = ws - dist;
						if (y <= center && y >= center - wingSpan * 2)
							pixel = true;
						if (y >= center && y <= center + wingSpan)
							pixel = true;
					}
				}

				if (pixel)
				{
					bool isBody = y >= center - bodyThickness && y <= center + bodyThickness;
					img.SetPixel(x, y, isBody ? body : wing);
				}
			}
		}

		return ImageTexture.CreateFromImage(img);
	}
}
