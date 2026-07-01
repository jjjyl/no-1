namespace No1;

using Godot;
using No1.Core;
using System;
using System.Collections.Generic;

/// <summary>
/// 敌人世界实体脚本 — 挂在 biped_small / quadruped / floater 原型模板上。
/// 职责：精灵动画、阴影射线、闲逛状态机、仇恨检测。
/// 战斗数值、视觉参数、状态转换规则全部从 JSON 注入。
/// </summary>
public partial class EnemyBase : Node3D
{
	// ── 注入后设置 ──
	public string EnemyId { get; private set; }
	public EnemyState Def { get; private set; }
	public CharacterStats Stats { get; private set; }

	// ── 内部节点引用 ──
	Sprite3D _sprite;
	Sprite3D _shadow;
	RayCast3D _groundRay;
	Area3D _bodyArea;
	Area3D _aggroArea;

	// ── 状态机 ──
	enum State { Idle, Wander, Rest, Aggro, Chase, Combat, Dead }
	State _state = State.Idle;
	float _stateTimer;
	float _wanderAngle;
	Vector3 _wanderTarget;
	Vector3 _spawnPos;
	float _wanderSpeed = 1.2f;
	float _wanderRadius = 6f;
	float _chaseSpeed = 2.5f;
	float _aggroRange = 3.5f;
	Node3D _chaseTarget;

	// ── 动画 ──
	int _hframes = 4, _vframes = 5;
	float _animTimer;
	float _animFps = 6;
	int _currentFrame;
	int _animStartFrame, _animFrameCount;
	int _animRow;
	bool _animLoop = true;
	string _currentAnim = "idle";
	Dictionary<string, AnimDef> _anims = new();
	Dictionary<string, StateDef> _stateDefs = new();

	// ── 计时器 ──
	float _idleMinSec = 3, _idleMaxSec = 8;
	float _wanderMinSec = 5, _wanderMaxSec = 15;
	float _restMinSec = 10, _restMaxSec = 25;
	float _restChance = 0.15f;

	class AnimDef { public int Row, StartCol, Frames, Fps; public bool Loop; }
	class StateDef { public string Anim; public float[] AfterSec;
		public float Speed, Radius, Range, Chance; public bool OnContact; }

	public override void _Ready()
	{
		_sprite    = GetNode<Sprite3D>("Sprite");
		_shadow    = GetNode<Sprite3D>("Shadow");
		_groundRay = GetNode<RayCast3D>("GroundRay");
		_bodyArea  = GetNode<Area3D>("BodyArea");
		_aggroArea = GetNodeOrNull<Area3D>("AggroArea");

		if (_aggroArea == null)
		{
			// 兼容用户尚未改名的 Area3D（模板可能仍命名 Area3D）
			_aggroArea = GetNodeOrNull<Area3D>("Area3D");
		}

		if (_aggroArea != null)
		{
			_aggroArea.BodyEntered += OnAggroBodyEntered;
			_aggroArea.BodyExited += OnAggroBodyExited;
		}

		_spawnPos = GlobalPosition;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		UpdateShadow(dt);
		UpdateAnimation(dt);
		UpdateFSM(dt);
	}

	/// <summary>从 JSON 数据注入敌人配置。</summary>
	public void Inject(EnemyState def)
	{
		Def = def;
		EnemyId = def.Id;
		Name = def.Name;

		// Load VisualDef
		var vis = LoadVisualDef(def.Id);
		if (vis != null)
		{
			if (!string.IsNullOrEmpty(vis.Spritesheet))
			{
				var tex = GD.Load<Texture2D>(vis.Spritesheet);
				if (tex != null)
					_sprite.Texture = tex;
			}
			Scale = Vector3.One * vis.Scale;
			_sprite.Position = new Vector3(vis.SpriteOffset[0], vis.SpriteOffset[1], vis.SpriteOffset[2]);
			_shadow.Scale = new Vector3(vis.ShadowScale[0], vis.ShadowScale[1], vis.ShadowScale[2]);
		}

		// Load FsmDef
		var fsm = LoadFsmDef(def.Id);
		if (fsm != null)
		{
			_sprite.Hframes = fsm.Hframes;
			_sprite.Vframes = fsm.Vframes;
			_hframes = fsm.Hframes;
			_vframes = fsm.Vframes;
			_anims = fsm.Anims;
			_stateDefs = fsm.States;
			ApplyStateDef("idle");
			_animFps = 6;
			_currentAnim = "idle";
		}

		Stats = def.SpawnStats(CycleManager.Instance?.CurrentCycle ?? 1);
	}

	// ═══════════════════════════════════════════════════════════════
	//  Visual loading
	// ═══════════════════════════════════════════════════════════════

	VisualDef LoadVisualDef(string id)
	{
		var file = FileAccess.Open("res://assets/data/enemy_visuals.json", FileAccess.ModeFlags.Read);
		if (file == null) return null;
		var root = Json.ParseString(file.GetAsText()).AsGodotDictionary();
		if (!root.ContainsKey(id)) return null;
		var d = root[id].AsGodotDictionary();
		return new VisualDef
		{
			Spritesheet = ReadStr(d, "spritesheet"),
			Scale       = (float)(d.ContainsKey("scale") ? d["scale"].AsDouble() : 1.0),
			SpriteOffset = ReadVec3(d, "sprite_offset"),
			ShadowScale  = ReadVec3(d, "shadow_scale"),
		};
	}

	FsmDef LoadFsmDef(string id)
	{
		var file = FileAccess.Open("res://assets/data/enemy_fsm.json", FileAccess.ModeFlags.Read);
		if (file == null) return null;
		var root = Json.ParseString(file.GetAsText()).AsGodotDictionary();
		if (!root.ContainsKey(id)) return null;
		var d = root[id].AsGodotDictionary();

		var layout = d["sprite_layout"].AsGodotDictionary();
		var def = new FsmDef
		{
			Hframes = (int)layout["hframes"].AsDouble(),
			Vframes = (int)layout["vframes"].AsDouble(),
			Anims   = new(),
			States  = new(),
		};

		if (d.ContainsKey("anims"))
		{
			foreach (var (key, val) in d["anims"].AsGodotDictionary())
			{
				var a = val.AsGodotDictionary();
				def.Anims[key.AsString()] = new AnimDef
				{
					Row = (int)a["row"].AsDouble(),
					StartCol = (int)a["start_col"].AsDouble(),
					Frames = (int)a["frames"].AsDouble(),
					Fps = (int)a["fps"].AsDouble(),
					Loop = !a.ContainsKey("loop") || a["loop"].AsBool(),
				};
			}
		}

		if (d.ContainsKey("states"))
		{
			foreach (var (key, val) in d["states"].AsGodotDictionary())
			{
				var s = val.AsGodotDictionary();
				var sd = new StateDef { Anim = ReadStr(s, "anim") };
				if (s.ContainsKey("speed")) sd.Speed = (float)s["speed"].AsDouble();
				if (s.ContainsKey("range")) sd.Range = (float)s["range"].AsDouble();
				if (s.ContainsKey("wander_radius")) sd.Radius = (float)s["wander_radius"].AsDouble();
				if (s.ContainsKey("chance_from_idle")) sd.Chance = (float)s["chance_from_idle"].AsDouble();
				if (s.ContainsKey("on_contact")) sd.OnContact = s["on_contact"].AsBool();
				if (s.ContainsKey("after_sec"))
				{
					var arr = s["after_sec"].AsGodotArray();
					sd.AfterSec = new[] { (float)arr[0].AsDouble(), (float)arr[1].AsDouble() };
				}
				def.States[key.AsString()] = sd;
			}
		}

		return def;
	}

	class VisualDef
	{
		public string Spritesheet;
		public float Scale = 1f;
		public Vector3 SpriteOffset = Vector3.Zero;
		public Vector3 ShadowScale = new(0.6f, 0.3f, 1);
	}

	class FsmDef
	{
		public int Hframes, Vframes;
		public Dictionary<string, AnimDef> Anims = new();
		public Dictionary<string, StateDef> States = new();
	}

	// ═══════════════════════════════════════════════════════════════
	//  Shadow — 射线查地面高度
	// ═══════════════════════════════════════════════════════════════

	void UpdateShadow(float dt)
	{
		if (_groundRay == null || _shadow == null) return;
		_groundRay.ForceRaycastUpdate();
		if (_groundRay.IsColliding())
		{
			var hit = _groundRay.GetCollisionPoint();
			_shadow.GlobalPosition = new Vector3(
				GlobalPosition.X,
				hit.Y + 0.02f,
				GlobalPosition.Z
			);
			_shadow.Visible = true;
		}
		else
		{
			_shadow.Visible = false;
		}
	}

	// ═══════════════════════════════════════════════════════════════
	//  Spritesheet animation — 通过 Sprite3D.Frame 切帧
	// ═══════════════════════════════════════════════════════════════

	void UpdateAnimation(float dt)
	{
		if (_anims.Count == 0) return;

		_animTimer += dt;
		float interval = 1f / Mathf.Max(_animFps, 1);
		if (_animTimer < interval) return;
		_animTimer -= interval;

		_currentFrame++;
		if (_currentFrame >= _animFrameCount)
		{
			if (_animLoop)
				_currentFrame = 0;
			else
				_currentFrame = _animFrameCount - 1;
		}

		int frameIdx = _animStartFrame + _currentFrame;
		int col = frameIdx % _hframes;
		int row = _animRow + frameIdx / _hframes;
		_sprite.FrameCoords = new Vector2I(col, row);
	}

	void PlayAnim(string name)
	{
		if (name == _currentAnim) return;
		_currentAnim = name;
		if (!_anims.TryGetValue(name, out var a)) return;

		_animFps = a.Fps;
		_animRow = a.Row;
		_animStartFrame = a.StartCol;
		_animFrameCount = a.Frames;
		_animLoop = a.Loop;
		_currentFrame = 0;
		_animTimer = 0;
	}

	// ═══════════════════════════════════════════════════════════════
	//  FSM
	// ═══════════════════════════════════════════════════════════════

	void UpdateFSM(float dt)
	{
		// 如果已死，不再处理状态
		if (_state == State.Dead) return;

		switch (_state)
		{
			case State.Idle:    IdleTick(dt);    break;
			case State.Wander:  WanderTick(dt);  break;
			case State.Rest:    RestTick(dt);    break;
			case State.Aggro:   AggroTick(dt);   break;
			case State.Chase:   ChaseTick(dt);   break;
		}
	}

	void ApplyStateDef(string stateKey)
	{
		if (!_stateDefs.TryGetValue(stateKey, out var sd)) return;
		PlayAnim(sd.Anim);
		if (sd.AfterSec != null && sd.AfterSec.Length == 2)
			_stateTimer = (float)GD.RandRange(sd.AfterSec[0], sd.AfterSec[1]);
		_wanderSpeed   = sd.Speed > 0 ? sd.Speed : 1.2f;
		_wanderRadius  = sd.Radius > 0 ? sd.Radius : 6f;
		_aggroRange    = sd.Range > 0 ? sd.Range : 3.5f;
		_chaseSpeed    = sd.Speed > 0 ? sd.Speed * 2f : 2.5f;
		_restChance    = sd.Chance > 0 ? sd.Chance : 0.15f;
	}

	void TransitionTo(State next)
	{
		_state = next;
		string key = next.ToString().ToLower();
		ApplyStateDef(key);

		if (next == State.Wander)
		{
			_wanderAngle = (float)GD.RandRange(0, Mathf.Pi * 2);
			_wanderTarget = _spawnPos + new Vector3(
				Mathf.Cos(_wanderAngle) * _wanderRadius * 0.5f,
				0,
				Mathf.Sin(_wanderAngle) * _wanderRadius * 0.5f
			);
		}
	}

	void IdleTick(float dt)
	{
		_stateTimer -= dt;
		if (_stateTimer <= 0)
		{
			// 有几率进入休息而非闲逛
			if (GD.Randf() < _restChance && _stateDefs.ContainsKey("rest"))
				TransitionTo(State.Rest);
			else
				TransitionTo(State.Wander);
		}
	}

	void WanderTick(float dt)
	{
		var to = _wanderTarget - GlobalPosition;
		to.Y = 0;
		if (to.Length() < 0.15f)
		{
			_stateTimer -= dt;
			if (_stateTimer <= 0)
				TransitionTo(State.Idle);
			return;
		}

		var dir = to.Normalized();
		GlobalPosition += dir * _wanderSpeed * dt;

		// 翻转精灵朝向
		if (dir.X != 0)
			_sprite.FlipH = dir.X < 0;
	}

	void RestTick(float dt)
	{
		_stateTimer -= dt;
		if (_stateTimer <= 0)
			TransitionTo(State.Idle);
	}

	void AggroTick(float dt)
	{
		_stateTimer -= dt;
		if (_chaseTarget == null && _stateTimer <= 0)
		{
			TransitionTo(State.Idle);
			return;
		}
		if (_chaseTarget != null)
			TransitionTo(State.Chase);
	}

	void ChaseTick(float dt)
	{
		if (_chaseTarget == null)
		{
			TransitionTo(State.Idle);
			return;
		}

		var to = _chaseTarget.GlobalPosition - GlobalPosition;
		to.Y = 0;
		float dist = to.Length();

		// 超出追击范围或目标消失则返回闲逛
		if (dist > _aggroRange * 3f)
		{
			_chaseTarget = null;
			TransitionTo(State.Idle);
			return;
		}

		var dir = to.Normalized();
		GlobalPosition += dir * _chaseSpeed * dt;
		if (dir.X != 0)
			_sprite.FlipH = dir.X < 0;
	}

	// ═══════════════════════════════════════════════════════════════
	//  Aggro signals
	// ═══════════════════════════════════════════════════════════════

	void OnAggroBodyEntered(Node body)
	{
		var parent = body;
		while (parent != null)
		{
			if (parent.Name == "Player")
			{
				_chaseTarget = parent as Node3D;
				TransitionTo(State.Aggro);
				_stateTimer = 1.5f;
				return;
			}
			parent = parent.GetParentOrNull<Node>();
		}
	}

	void OnAggroBodyExited(Node body)
	{
		var parent = body;
		while (parent != null)
		{
			if (parent.Name == "Player")
			{
				_chaseTarget = null;
				return;
			}
			parent = parent.GetParentOrNull<Node>();
		}
	}

	// ═══════════════════════════════════════════════════════════════
	//  Helpers
	// ═══════════════════════════════════════════════════════════════

	static string ReadStr(Godot.Collections.Dictionary d, string key)
		=> d.ContainsKey(key) ? d[key].AsString() : "";

	static Vector3 ReadVec3(Godot.Collections.Dictionary d, string key)
	{
		if (!d.ContainsKey(key)) return Vector3.Zero;
		var arr = d[key].AsGodotArray();
		return new Vector3(
			(float)arr[0].AsDouble(),
			(float)arr[1].AsDouble(),
			(float)arr[2].AsDouble()
		);
	}
}
