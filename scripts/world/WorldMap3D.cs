namespace No1.World;
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
	// ── World size in meters (2D 2000×1500px mapped at 0.01 scale) ──
	const float WorldW = 20f;
	const float WorldH = 15f;
	const float Scale2D = 0.01f; // pixels → meters

	// ── Zone data (names must match EventManager location keys) ──
	static readonly string[] _zoneNames = { "林地边缘", "废矿入口", "断崖台地" };

	// 2D pixel coords → 3D XZ (pixel * 0.01)
	static readonly Vector3[] _zonePoses =
	{
		new(4f,    0, 12f),       // 林地边缘 — 左下
		new(10f,   0, 8f),        // 废矿入口 — 中央
		new(10f,   0, 3.5f),      // 断崖台地 — 上方
	};

	// ── Runtime state ──
	Player3D _player;
	Camera3D _camera;
	int _currentZone = -1;
	bool _combatPending;
	bool _started;
	Node3D _cameraPivot;
	ITerrainProvider _terrain;

	// Camera control
	float _cameraDistance = 15f;
	float _cameraYaw;    // horizontal rotation around world center
	float _cameraPitch = 45f; // angle from horizontal
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
		_currentZone = CycleManager.Instance.CurrentNodeIndex;
		_terrain = new FlatTerrainProvider();

		var mats = new WorldMaterials();
		AddChild(mats);

		BuildParallax();
		BuildTerrain();
		ScatterDecorations();
		BuildZoneTriggers();
		BuildEnemyPlaceholders();
		BuildShopNPC();
		BuildCamera();
		BuildPlayer();
		BuildReturnButton();
		BuildZoneLabels();
	}

	public override void _Process(double delta)
	{
		if (!_started)
		{
			_started = true;
			TriggerStartZone();
		}

		UpdateCamera((float)delta);

		if (!_combatPending) return;
		if (DialogueManager.IsFullDialogueActive()) return;

		_combatPending = false;
		GameManager.Instance.GoToScene("res://scenes/combat/combat.tscn");
		CycleManager.Instance.PendingEnemyScene = null;
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
			targetPos, delta * 5f);

		UpdateCameraTransform();
	}

	void UpdateCameraTransform()
	{
		float pitchRad = Mathf.DegToRad(_cameraPitch);
		float yawRad = Mathf.DegToRad(_cameraYaw);

		float x = Mathf.Cos(pitchRad) * Mathf.Sin(yawRad);
		float y = Mathf.Sin(pitchRad);
		float z = Mathf.Cos(pitchRad) * Mathf.Cos(yawRad);

		_camera.Position = new Vector3(x, y, z) * _cameraDistance;
		_camera.LookAt(_cameraPivot.GlobalPosition, Vector3.Up);
	}

	void ToggleFirstPerson()
	{
		_firstPerson = !_firstPerson;

		if (_firstPerson)
		{
			_savedCamDistance = _cameraDistance;
			_savedCamYaw = _cameraYaw;
			_fpYaw = _cameraYaw;
			_fpPitch = 0f;
			Input.MouseMode = Input.MouseModeEnum.Captured;
			if (_player != null) _player.ProcessMode = ProcessModeEnum.Disabled;
		}
		else
		{
			_cameraDistance = _savedCamDistance;
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
			_fpYaw   -= mm.Relative.X * 0.15f;
			_fpPitch -= mm.Relative.Y * 0.15f;
			_fpPitch  = Mathf.Clamp(_fpPitch, -89f, 89f);
			return;
		}

		// ── Desktop scroll zoom ──
		if (_firstPerson) return; // block desktop controls in FP

		if (e is InputEventMouseButton mb)
		{
			if (mb.ButtonIndex == MouseButton.WheelUp)
				_cameraDistance = Mathf.Clamp(_cameraDistance - 1.5f, 5f, 40f);
			else if (mb.ButtonIndex == MouseButton.WheelDown)
				_cameraDistance = Mathf.Clamp(_cameraDistance + 1.5f, 5f, 40f);

			if (mb.ButtonIndex == MouseButton.Middle)
			{
				_middleDragging = mb.Pressed;
				_dragStart = mb.Position;
			}
		}

		if (e is InputEventMouseMotion mm2 && _middleDragging)
			_cameraYaw -= mm2.Relative.X * 0.3f;

		// Let player handle left-click (don't consume here)
	}

	// ═══════════════════════════════════════════════════════════════
	//  Parallax — layered Sprite3D billboards at Z depths
	// ═══════════════════════════════════════════════════════════════

	void BuildParallax()
	{
		var layer = new Node3D { Name = "Parallax" };

		// Sky — huge backdrop, far behind
		AddBillboard(layer, "Sky",
			new Vector3(WorldW * 0.5f, WorldH * 0.3f, -12f),
			new Vector2(30, 18),
			WorldMaterials.Instance.Sky);

		// Sun — upper right
		AddBillboard(layer, "Sun",
			new Vector3(15f, 10f, -11f),
			new Vector2(1.8f, 1.8f),
			WorldMaterials.Instance.Sun);

		// Mountains — 10 triangle silhouettes spread across horizon
		for (int i = 0; i < 10; i++)
		{
			float mx = i * 2.3f + ((i * 137) % 60) * 0.01f;
			float mh = 0.8f + ((i * 73) % 100) * 0.01f;
			float mw = 0.8f + ((i * 53) % 60) * 0.01f;

			var mountain = new Sprite3D
			{
				Texture = MakeTriangleTexture((int)(mw * 200), (int)(mh * 200),
					WorldMaterials.Instance.Mountain.AlbedoColor),
				Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
				Position = new Vector3(mx, 3.5f, -6f),
				PixelSize = 0.005f
			};
			mountain.Name = $"Mountain_{i}";
			layer.AddChild(mountain);
		}

		// Dragon shadow — animated billboard
		var dragon = AddBillboard(layer, "DragonShadow",
			new Vector3(-5f, 5f, -5f),
			new Vector2(3.5f, 0.5f),
			WorldMaterials.Instance.DragonShadow);
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
			tween.TweenProperty(shadow, "position:x", WorldW + 3f, 12f);
			await ToSignal(tween, "finished");
		}
	}

	// ═══════════════════════════════════════════════════════════════
	//  Terrain — ground plane + zone plates + paths
	// ═══════════════════════════════════════════════════════════════

	void BuildTerrain()
	{
		var terrain = new Node3D { Name = "Terrain" };

		// Grass base — large plane at Y=0
		var grass = MakeGroundPlane("GrassBase",
			new Vector3(WorldW * 0.5f, 0, WorldH * 0.5f),
			new Vector2(WorldW, WorldH),
			WorldMaterials.Instance.GrassBase.AlbedoColor);
		terrain.AddChild(grass);

		// Zone plates
		AddZonePlate(terrain, 0, WorldMaterials.Instance.ZoneForest, 4.2f, 3.2f);
		AddZonePlate(terrain, 1, WorldMaterials.Instance.ZoneMine, 4.2f, 3.2f);
		AddZonePlate(terrain, 2, WorldMaterials.Instance.ZoneCliff, 4.5f, 3.0f);

		// Paths between zones
		Draw3DPath(terrain, _zonePoses[0], _zonePoses[1], 0.6f,
			WorldMaterials.Instance.Path01.AlbedoColor);
		Draw3DPath(terrain, _zonePoses[1], _zonePoses[2], 0.5f,
			WorldMaterials.Instance.Path12.AlbedoColor);

		AddChild(terrain);
	}

	MeshInstance3D MakeGroundPlane(string name, Vector3 pos, Vector2 size, Color color)
	{
		var mesh = new QuadMesh { Size = size };
		var mat = new StandardMaterial3D
		{
			AlbedoColor = color,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};
		var instance = new MeshInstance3D
		{
			Name = name,
			Mesh = mesh,
			Position = pos,
			RotationDegrees = new Vector3(-90, 0, 0),
			MaterialOverride = mat
		};
		return instance;
	}

	void AddZonePlate(Node parent, int idx, Material mat, float w, float h)
	{
		var color = mat is StandardMaterial3D sm ? sm.AlbedoColor : Colors.White;
		var plate = MakeGroundPlane($"Zone_{idx}",
			_zonePoses[idx] + new Vector3(0, 0.005f, 0),
			new Vector2(w, h), color);
		parent.AddChild(plate);
	}

	void Draw3DPath(Node parent, Vector3 from, Vector3 to, float width, Color color)
	{
		var dir = new Vector3(to.X - from.X, 0, to.Z - from.Z);
		var len = dir.Length();
		var mid = from + dir * 0.5f;
		var angle = Mathf.Atan2(dir.X, dir.Z);

		var mesh = new QuadMesh { Size = new Vector2(len, width) };
		var mat = new StandardMaterial3D
		{
			AlbedoColor = color,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};
		var instance = new MeshInstance3D
		{
			Name = "Path",
			Mesh = mesh,
			Position = mid + new Vector3(0, 0.003f, 0),
			RotationDegrees = new Vector3(-90, Mathf.RadToDeg(angle), 0),
			MaterialOverride = mat
		};
		parent.AddChild(instance);
	}

	// ═══════════════════════════════════════════════════════════════
	//  Decorations — random Sprite3D trees/rocks/ruins
	// ═══════════════════════════════════════════════════════════════

	void ScatterDecorations()
	{
		var deco = new Node3D { Name = "Decorations" };
		var rng = new RandomNumberGenerator();
		rng.Seed = 42; // fixed seed → same layout every time; vary per cycle later

		var mats = WorldMaterials.Instance;

		// Trees — clusters near forest zone (zone 0)
		for (int i = 0; i < 25; i++)
		{
			float dx = (rng.Randf() - 0.3f) * 8f;
			float dz = (rng.Randf() - 0.7f) * 8f;
			MakeTree(deco, new Vector3(4f + dx, 0, 12f + dz), rng.RandfRange(0.6f, 1.3f), mats.DecoTree);
		}

		// Rocks — near mine zone (zone 1)
		for (int i = 0; i < 15; i++)
		{
			float dx = (rng.Randf() - 0.5f) * 6f;
			float dz = (rng.Randf() - 0.5f) * 6f;
			MakeRock(deco, new Vector3(10f + dx, 0, 8f + dz), rng.RandfRange(0.3f, 0.8f), mats.DecoRock);
		}

		// Ruins — near cliff zone (zone 2)
		for (int i = 0; i < 8; i++)
		{
			float dx = (rng.Randf() - 0.5f) * 5f;
			float dz = (rng.Randf() - 0.5f) * 5f;
			MakeRuin(deco, new Vector3(10f + dx, 0, 3.5f + dz), mats.DecoRuin);
		}

		// Scattered decorations across the world
		for (int i = 0; i < 20; i++)
		{
			float x = rng.RandfRange(1f, WorldW - 1f);
			float z = rng.RandfRange(1f, WorldH - 1f);
			float type = rng.Randf();
			if (type < 0.5f)
				MakeTree(deco, new Vector3(x, 0, z), rng.RandfRange(0.4f, 0.9f), mats.DecoTree);
			else
				MakeRock(deco, new Vector3(x, 0, z), rng.RandfRange(0.2f, 0.5f), mats.DecoRock);
		}

		AddChild(deco);
	}

	void MakeTree(Node parent, Vector3 pos, float scale, Material mat)
	{
		var color = mat is StandardMaterial3D sm ? sm.AlbedoColor : Colors.Green;

		// Trunk — brown rectangle
		var trunk = new Sprite3D
		{
			Texture = MakeColorTexture(new Color(0.25f, 0.18f, 0.10f), 4, 16),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = pos + new Vector3(0, 0.3f * scale, 0),
			PixelSize = 0.03f * scale,
			Modulate = new Color(0.25f, 0.18f, 0.10f)
		};
		parent.AddChild(trunk);

		// Canopy — green triangle
		var canopy = new Sprite3D
		{
			Texture = MakeTriangleTexture(24, 18, color),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = pos + new Vector3(0, 0.8f * scale, 0),
			PixelSize = 0.025f * scale,
			Modulate = color
		};
		parent.AddChild(canopy);
	}

	void MakeRock(Node parent, Vector3 pos, float scale, Material mat)
	{
		var color = mat is StandardMaterial3D sm ? sm.AlbedoColor : Colors.Gray;
		var rock = new Sprite3D
		{
			Texture = MakeCircleTexture(color, 24),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = pos + new Vector3(0, 0.15f * scale, 0),
			PixelSize = 0.025f * scale,
			Modulate = color
		};
		parent.AddChild(rock);
	}

	void MakeRuin(Node parent, Vector3 pos, Material mat)
	{
		var color = mat is StandardMaterial3D sm ? sm.AlbedoColor : new Color(0.28f, 0.24f, 0.20f);
		var ruin = new Sprite3D
		{
			Texture = MakeColorTexture(color, 6, 14),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = pos + new Vector3(0, 0.45f, 0),
			PixelSize = 0.06f,
			Modulate = color
		};
		parent.AddChild(ruin);
	}

	// ═══════════════════════════════════════════════════════════════
	//  Zone triggers — Area3D + Box colliders
	// ═══════════════════════════════════════════════════════════════

	void BuildZoneTriggers()
	{
		var triggers = new Node3D { Name = "Triggers" };
		for (int i = 0; i < _zonePoses.Length; i++)
		{
			var area = new Area3D
			{
				Position = _zonePoses[i],
				Name = _zoneNames[i]
			};
			var shape = new CollisionShape3D();
			shape.Shape = new BoxShape3D { Size = new Vector3(2.6f, 1f, 2.6f) };
			area.AddChild(shape);

			int idx = i;
			area.BodyEntered += (body) =>
			{
				if (body == _player && _currentZone != idx)
					OnEnterZone(idx);
			};

			triggers.AddChild(area);
		}
		AddChild(triggers);
	}

	void OnEnterZone(int idx)
	{
		_currentZone = idx;
		GD.Print($"[WorldMap3D] Entered: {_zoneNames[idx]}");
		CycleManager.Instance.CurrentNodeIndex = idx;

		EventManager.CheckEvents(_zoneNames[idx], CycleManager.Instance, _ => { });

		if (!string.IsNullOrEmpty(CycleManager.Instance.PendingEnemyScene))
			_combatPending = true;
	}

	void TriggerStartZone()
	{
		if (CycleManager.Instance.SkipStartEvents)
		{
			CycleManager.Instance.SkipStartEvents = false;
			return;
		}

		int start = CycleManager.Instance.CurrentNodeIndex;
		GD.Print($"[WorldMap3D] Start zone: {_zoneNames[start]}");
		EventManager.CheckEvents(_zoneNames[start], CycleManager.Instance, _ => { });
		if (!string.IsNullOrEmpty(CycleManager.Instance.PendingEnemyScene))
			_combatPending = true;
	}

	// ═══════════════════════════════════════════════════════════════
	//  Enemy placeholders
	// ═══════════════════════════════════════════════════════════════

	void BuildEnemyPlaceholders()
	{
		var enemies = new Node3D { Name = "Enemies" };
		AddEnemyDot(enemies, new Vector3(6f, 0.01f, 10f));
		AddEnemyDot(enemies, new Vector3(13f, 0.01f, 9.5f));
		AddEnemyDot(enemies, new Vector3(9f, 0.01f, 5f));
		AddChild(enemies);
	}

	void AddEnemyDot(Node parent, Vector3 pos)
	{
		var dot = new Sprite3D
		{
			Name = "EnemyDot",
			Texture = MakeCircleTexture(WorldMaterials.Instance.EnemyDot.AlbedoColor),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = pos,
			PixelSize = 0.005f,
			Modulate = WorldMaterials.Instance.EnemyDot.AlbedoColor
		};
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
		_player.Position = _zonePoses[CycleManager.Instance.CurrentNodeIndex];
		AddChild(_player);
	}

	// ═══════════════════════════════════════════════════════════════
	//  Zone labels — Label3D billboard
	// ═══════════════════════════════════════════════════════════════

	void BuildZoneLabels()
	{
		for (int i = 0; i < _zonePoses.Length; i++)
		{
			var label = new Label3D
			{
				Text = _zoneNames[i],
				Position = _zonePoses[i] + new Vector3(0, 1.2f, 0),
				Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
				Modulate = new Color(1, 1, 1, 0.6f),
				FontSize = 48,
				OutlineSize = 2
			};
			AddChild(label);
		}
	}

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
			GameManager.Instance.GoToScene("res://scenes/temple/temple.tscn");
		};
		canvas.AddChild(btn);
		AddChild(canvas);
	}

	// ═══════════════════════════════════════════════════════════════
	//  Texture helpers — programmatic colored squares for prototyping
	//  (Replace with real sprites/textures later)
	// ═══════════════════════════════════════════════════════════════

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
}
