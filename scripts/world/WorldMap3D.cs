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

	// ── Data paths ──
	public const string MapNodesDataPath = "res://assets/data/map_nodes.json";

	// ── Zone data (loaded from map_nodes.json at runtime) ──
	List<string> _zoneNames = new();
	List<Vector3> _zonePoses = new();
	List<int[]> _zoneConnections = new();
	bool _zoneDataLoaded;

	// ── Runtime state ──
	Player3D _player;
	Camera3D _camera;
	int _currentZone = -1;
	bool _combatPending;
	bool _started;
	Node3D _cameraPivot;
	ITerrainProvider _terrain;

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

	// ═══════════════════════════════════════════════════════════════
	//  Zone data loading — reads map_nodes.json at runtime
	// ═══════════════════════════════════════════════════════════════

	bool LoadZoneData()
	{
		var file = FileAccess.Open(MapNodesDataPath, FileAccess.ModeFlags.Read);
		if (file == null) return false;

		try
		{
			var dict = Json.ParseString(file.GetAsText()).AsGodotDictionary();
			var arr = dict["nodes"].AsGodotArray();
			for (int i = 0; i < arr.Count; i++)
			{
				var d = arr[i].AsGodotDictionary();
				_zoneNames.Add(d["name"].AsString());

				var pos3d = d["pos_3d"].AsGodotDictionary();
				float x = (float)pos3d["x"].AsDouble();
				float z = (float)pos3d["z"].AsDouble();
				_zonePoses.Add(new Vector3(x, 0, z));

				var conns = d["connections"].AsGodotArray();
				var arr2 = new int[conns.Count];
				for (int j = 0; j < conns.Count; j++)
					arr2[j] = (int)conns[j].AsDouble();
				_zoneConnections.Add(arr2);
			}
			_zoneDataLoaded = true;
			return true;
		}
		catch
		{
			return false;
		}
	}

	void LoadFallbackZones()
	{
		_zoneNames = new() { "林地边缘", "废矿入口", "断崖台地" };
		_zonePoses = new() { new(4, 0, 12), new(10, 0, 8), new(10, 0, 3.5f) };
		_zoneConnections = new() { new[] { 0, 1 }, new[] { 0, 1, 2 }, new[] { 1, 2 } };
		_zoneDataLoaded = true;
	}

	StandardMaterial3D GetZoneMaterial(string zoneName)
	{
		var m = WorldMaterials.Instance;
		return zoneName switch
		{
			"林地边缘" => m.ZoneForest,
			"废矿入口" => m.ZoneMine,
			"断崖台地" => m.ZoneCliff,
			"古战场" => m.ZoneBattlefield,
			"结晶洞穴" => m.ZoneCrystal,
			"荒原边缘" => m.ZoneWasteland,
			"忘却之塔" => m.ZoneTower,
			"圣泉" => m.ZoneSpring,
			_ => m.ZoneForest
		};
	}

	StandardMaterial3D GetPathMaterial(int pathIndex)
	{
		var m = WorldMaterials.Instance;
		return (pathIndex % 3) switch
		{
			0 => m.Path01,
			1 => m.Path12,
			_ => m.Path34
		};
	}

	public override void _Ready()
	{
		if (!LoadZoneData())
			LoadFallbackZones();

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

		// Sky — huge backdrop, far behind
		AddBillboard(layer, "Sky",
			new Vector3(WorldWidth * 0.5f, WorldHeight * 0.3f, -12f),
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
			tween.TweenProperty(shadow, "position:x", WorldWidth + 3f, 12f);
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
			new Vector3(WorldWidth * 0.5f, 0, WorldHeight * 0.5f),
			new Vector2(WorldWidth, WorldHeight),
			WorldMaterials.Instance.GrassBase);
		terrain.AddChild(grass);

		// Zone plates — one per zone from loaded data
		for (int i = 0; i < _zoneNames.Count; i++)
		{
			float zw = ZoneDefaultWidth, zh = ZoneDefaultHeight;
			if (_zoneNames[i] == "断崖台地") { zw = 4.5f; zh = 3.0f; }
			AddZonePlate(terrain, i, GetZoneMaterial(_zoneNames[i]), zw, zh);
		}

		// Paths between connected zones (only each pair once, j > i)
		int pathIdx = 0;
		for (int i = 0; i < _zoneConnections.Count; i++)
		{
			foreach (int j in _zoneConnections[i])
			{
				if (j > i)
				Draw3DPath(terrain, _zonePoses[i], _zonePoses[j], PathWidth,
					GetPathMaterial(pathIdx++));
			}
		}

		AddChild(terrain);
	}

	MeshInstance3D MakeGroundPlane(string name, Vector3 pos, Vector2 size, Material mat)
	{
		var mesh = new QuadMesh { Size = size };
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
		var plate = MakeGroundPlane($"Zone_{idx}",
			_zonePoses[idx] + new Vector3(0, 0.005f, 0),
			new Vector2(w, h), mat);
		parent.AddChild(plate);
	}

	void Draw3DPath(Node parent, Vector3 from, Vector3 to, float width, Material mat)
	{
		var dir = new Vector3(to.X - from.X, 0, to.Z - from.Z);
		var len = dir.Length();
		var mid = from + dir * 0.5f;
		var angle = Mathf.Atan2(dir.X, dir.Z);

		var mesh = new QuadMesh { Size = new Vector2(len, width) };
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
		rng.Seed = (ulong)DecorationSeed; // fixed seed → same layout every time; vary per cycle later

		var mats = WorldMaterials.Instance;

		// Trees — clusters near forest zone (zone 0)
		for (int i = 0; i < TreeCount; i++)
		{
			float dx = (rng.Randf() - 0.3f) * 8f;
			float dz = (rng.Randf() - 0.7f) * 8f;
			MakeTree(deco, new Vector3(4f + dx, 0, 12f + dz), rng.RandfRange(0.6f, 1.3f), mats.DecoTree);
		}

		// Rocks — near mine zone (zone 1)
		for (int i = 0; i < RockCount; i++)
		{
			float dx = (rng.Randf() - 0.5f) * 6f;
			float dz = (rng.Randf() - 0.5f) * 6f;
			MakeRock(deco, new Vector3(10f + dx, 0, 8f + dz), rng.RandfRange(0.3f, 0.8f), mats.DecoRock);
		}

		// Ruins — near cliff zone (zone 2)
		for (int i = 0; i < RuinCount; i++)
		{
			float dx = (rng.Randf() - 0.5f) * 5f;
			float dz = (rng.Randf() - 0.5f) * 5f;
			MakeRuin(deco, new Vector3(10f + dx, 0, 3.5f + dz), mats.DecoRuin);
		}

		// Scattered decorations across the world
		for (int i = 0; i < ScatterCount; i++)
		{
			float x = rng.RandfRange(1f, WorldWidth - 1f);
			float z = rng.RandfRange(1f, WorldHeight - 1f);
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
		for (int i = 0; i < _zonePoses.Count; i++)
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
		for (int i = 0; i < _zonePoses.Count; i++)
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
			GameManager.Instance.GoToScene(GameManager.SceneTemple);
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
