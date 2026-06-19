namespace No1.World;

using Godot;
using No1.Core;
using No1.UI;

public partial class WorldMap : Node2D
{
	const float WorldW = 2000f;
	const float WorldH = 1500f;

	static readonly string[] _zoneNames = { "林地边缘", "废矿入口", "断崖台地" };
	static readonly Vector2[] _zonePoses =
	{
		new(400,  1200),   // 林地边缘 — 左下
		new(1000, 800),    // 废矿入口 — 中央
		new(1000, 350),    // 断崖台地 — 上方
	};

	PlayerController _player;
	int _currentZone = -1;
	bool _combatPending;
	bool _started;

	public override void _Ready()
	{
		// 先设当前区域，防止 BuildPlayer 后 BodyEntered 重复触发
		_currentZone = CycleManager.Instance.CurrentNodeIndex;

		BuildParallax();
		BuildTerrain();
		BuildZoneTriggers();
		BuildEnemyPlaceholders();
		BuildPlayer();
		BuildCamera();
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

		if (!_combatPending) return;
		if (DialogueManager.IsFullDialogueActive()) return;

		_combatPending = false;
		GameManager.Instance.GoToScene("res://scenes/combat/combat.tscn");
		CycleManager.Instance.PendingEnemyScene = null;
		CycleManager.Instance.PendingBattleEvents = "res://assets/data/battle_events.json";
	}

	void TriggerStartZone()
	{
		int start = CycleManager.Instance.CurrentNodeIndex;
		GD.Print($"[WorldMap] Start zone: {_zoneNames[start]}");
		EventManager.CheckEvents(_zoneNames[start], CycleManager.Instance, _ => { });
		if (!string.IsNullOrEmpty(CycleManager.Instance.PendingEnemyScene))
			_combatPending = true;
	}

	// ── 视差远景 ──

	void BuildParallax()
	{
		// 远景：天空（慢）
		var far = new Parallax2D { ScrollScale = new Vector2(0.15f, 0.15f) };
		var sky = new ColorRect
		{
			Color = new Color(0.18f, 0.28f, 0.45f),
			Size = new Vector2(WorldW + 600, WorldH + 400),
			Position = new Vector2(-300, -200)
		};
		far.AddChild(sky);

		var sun = new ColorRect
		{
			Color = new Color(1f, 0.85f, 0.5f, 0.6f),
			Size = new Vector2(90, 90),
			Position = new Vector2(1500, 80)
		};
		far.AddChild(sun);
		AddChild(far);

		// 中景：山脉剪影 + 龙影（中速）
		var mid = new Parallax2D { ScrollScale = new Vector2(0.45f, 0.45f) };
		for (int i = 0; i < 10; i++)
		{
			float mx = i * 230 + ((i * 137) % 60);
			float mh = 80 + ((i * 73) % 100);
			float mw = 80 + ((i * 53) % 60);
			var poly = new Polygon2D
			{
				Polygon = new Vector2[] { new(0, mh), new(mw * 0.5f, 0), new(mw, mh) },
				Color = new Color(0.1f, 0.12f, 0.18f, 0.8f),
				Position = new Vector2(mx, 180)
			};
			mid.AddChild(poly);
		}

		var dragon = new ColorRect
		{
			Color = new Color(0, 0, 0, 0.25f),
			Size = new Vector2(350, 50),
			Position = new Vector2(-500, 140)
		};
		dragon.Name = "DragonShadow";
		mid.AddChild(dragon);
		AnimateDragon(dragon);
		AddChild(mid);
	}

	async void AnimateDragon(ColorRect shadow)
	{
		while (IsInsideTree())
		{
			await ToSignal(GetTree().CreateTimer(8 + GD.Randf() * 10), "timeout");
			if (!IsInsideTree()) return;
			var tween = CreateTween();
			tween.TweenProperty(shadow, "position:x", WorldW + 200, 12f);
			tween.TweenProperty(shadow, "position:x", -600, 0f);
			await ToSignal(tween, "finished");
		}
	}

	// ── 地形 ──

	void BuildTerrain()
	{
		var terrain = new Node2D { Name = "Terrain" };

		// 底色草地
		terrain.AddChild(new ColorRect
		{
			Color = new Color(0.22f, 0.4f, 0.18f),
			Size = new Vector2(WorldW, WorldH)
		});

		// 三个区域色块
		AddZoneRect(terrain, 0, new Color(0.14f, 0.33f, 0.14f), 420, 320);
		AddZoneRect(terrain, 1, new Color(0.28f, 0.25f, 0.22f), 420, 320);
		AddZoneRect(terrain, 2, new Color(0.38f, 0.33f, 0.18f), 450, 300);

		// 小路
		DrawPath(terrain, _zonePoses[0], _zonePoses[1], 60, new Color(0.42f, 0.33f, 0.18f));
		DrawPath(terrain, _zonePoses[1], _zonePoses[2], 50, new Color(0.38f, 0.3f, 0.15f));

		AddChild(terrain);
	}

	void AddZoneRect(Node parent, int idx, Color c, float w, float h)
	{
		parent.AddChild(new ColorRect
		{
			Color = c,
			Size = new Vector2(w, h),
			Position = _zonePoses[idx] - new Vector2(w / 2, h / 2)
		});
	}

	void DrawPath(Node parent, Vector2 from, Vector2 to, float width, Color color)
	{
		var dir = to - from;
		var len = dir.Length();
		var mid = from + dir * 0.5f;
		var angle = dir.Angle();

		var rect = new ColorRect { Color = color };
		rect.Size = new Vector2(len, width);
		rect.PivotOffset = new Vector2(0, width / 2);
		rect.Position = from;
		rect.Rotation = angle;
		parent.AddChild(rect);
	}

	// ── 区域触发器 ──

	void BuildZoneTriggers()
	{
		var triggers = new Node { Name = "Triggers" };
		for (int i = 0; i < _zonePoses.Length; i++)
		{
			var area = new Area2D { Position = _zonePoses[i], Name = _zoneNames[i] };
			var shape = new CollisionShape2D();
			shape.Shape = new CircleShape2D { Radius = 130 };
			area.AddChild(shape);
			int idx = i;
			area.BodyEntered += (body) =>
			{
				GD.Print($"[WorldMap] BodyEntered zone {_zoneNames[idx]}, body={body.Name}, _player={_player.Name}, match={body == _player}");
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
		GD.Print($"[WorldMap] Entered: {_zoneNames[idx]}");
		CycleManager.Instance.CurrentNodeIndex = idx;

		EventManager.CheckEvents(_zoneNames[idx], CycleManager.Instance, _ => { });

		if (!string.IsNullOrEmpty(CycleManager.Instance.PendingEnemyScene))
			_combatPending = true;
	}

	// ── 明雷占位 ──

	void BuildEnemyPlaceholders()
	{
		var enemies = new Node { Name = "Enemies" };
		// 2-3 个霜精占位，在各区域外围游荡
		AddEnemy(enemies, new Vector2(600, 1000));
		AddEnemy(enemies, new Vector2(1300, 950));
		AddEnemy(enemies, new Vector2(900, 500));
		AddChild(enemies);
	}

	void AddEnemy(Node parent, Vector2 pos)
	{
		var enemy = new ColorRect
		{
			Color = new Color(0.8f, 0.3f, 0.3f),
			Size = new Vector2(16, 16),
			Position = pos
		};
		enemy.Name = "EnemyDot";
		parent.AddChild(enemy);
	}

	// ── 玩家 ──

	void BuildPlayer()
	{
		_player = new PlayerController { Name = "Player" };
		_player.Position = _zonePoses[CycleManager.Instance.CurrentNodeIndex];
		AddChild(_player);
	}

	// ── 摄像机 ──

	void BuildCamera()
	{
		var cam = new Camera2D { Zoom = new Vector2(1.6f, 1.6f) };
		cam.AddChild(new CameraFollow { Target = _player, FollowSpeed = 5f, LookAhead = 100f });
		AddChild(cam);
		cam.MakeCurrent();
	}

	// ── 返回神殿按钮 ──

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
		invBtn.Pressed += () =>
		{
			var panel = DialogueManager.Instance.ShowInventory();
			var cm = CycleManager.Instance;
			if (panel != null && cm != null)
				panel.SetInventory(cm.PlayerInventory, cm.PlayerStats?.DisplayName ?? "玩家");
		};
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

	// ── 区域名称标签 ──

	void BuildZoneLabels()
	{
		for (int i = 0; i < _zonePoses.Length; i++)
		{
			var label = new Label
			{
				Text = _zoneNames[i],
				Position = _zonePoses[i] + new Vector2(-30, -70),
				Modulate = new Color(1, 1, 1, 0.5f)
			};
			AddChild(label);
		}
	}
}
