namespace No1.Temple;

using Godot;
using No1.Core;
using No1.Data;

public partial class Temple3D : Node3D
{
	[Export] Camera3D _cam;
	[Export] CentralSlab _slab;
	[Export] GuideLight _guide;
	[Export] TempleEvolution _evolution;

	[Export] public float LookSensitivity = 0.02f;
	[Export] public float PitchMin = -60f;
	[Export] public float PitchMax = 60f;
	[Export] public float WakeUpDuration = 1.2f;
	[Export] public float WakeUpStartY = 0.3f;
	[Export] public float WakeUpEndY = 1.6f;

	float _yaw;
	float _pitch;
	bool _looking;
	WorldEnvironment _worldEnv;

	public override void _Ready()
	{
		_cam ??= GetNodeOrNull<Camera3D>("Camera3D");
		_slab ??= GetNodeOrNull<CentralSlab>("CentralSlab");
		_guide ??= GetNodeOrNull<GuideLight>("Guide");
		_evolution ??= GetNodeOrNull<TempleEvolution>("Evolution");

		if (_cam == null)
		{
			GD.PrintErr("[Temple3D] Camera3D not found.");
			return;
		}

		_cam.MakeCurrent();
		Input.MouseMode = Input.MouseModeEnum.Confined;
		BuildCrosshair();
		BuildGuideLight();

		if (_slab != null)
		{
			_slab.BlessingSelected += OnBlessingSelected;
			_slab.EnterWorld += OnEnterWorld;
		}

		StartWakeUp();
	}

	void BuildGuideLight()
	{
		if (_guide == null) return;

		var light = new OmniLight3D
		{
			LightColor = new Color(1f, 0.6f, 0.35f),
			LightEnergy = 0.4f,
			LightSize = 0.3f,
			Position = new Vector3(0, 0.1f, 0)
		};
		_guide.AddChild(light);
	}

	void BuildCrosshair()
	{
		var canvas = new CanvasLayer();
		AddChild(canvas);

		var size = DisplayServer.WindowGetSize();
		float cx = size.X / 2f;
		float cy = size.Y / 2f;
		const float gap = 12f;
		const float arm = 6f;
		const float thick = 2f;

		canvas.AddChild(MakeLine(new Vector2(cx - thick / 2f, cy - gap - arm), new Vector2(thick, arm)));
		canvas.AddChild(MakeLine(new Vector2(cx - thick / 2f, cy + gap), new Vector2(thick, arm)));
		canvas.AddChild(MakeLine(new Vector2(cx - gap - arm, cy - thick / 2f), new Vector2(arm, thick)));
		canvas.AddChild(MakeLine(new Vector2(cx + gap, cy - thick / 2f), new Vector2(arm, thick)));
	}

	static ColorRect MakeLine(Vector2 pos, Vector2 size) => new()
	{
		Color = new Color(1, 1, 1, 0.7f),
		Size = size,
		Position = pos
	};

	async void StartWakeUp()
	{
		_cam.Position = new Vector3(_cam.Position.X, WakeUpStartY, _cam.Position.Z);
		_pitch = -20f;
		ApplyRotation();

		await ToSignal(GetTree().CreateTimer(0.5f), "timeout");

		var tween = CreateTween();
		tween.TweenProperty(_cam, "position:y", WakeUpEndY, WakeUpDuration)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.Out);

		tween.Parallel().TweenMethod(
			Callable.From<float>(v => { _pitch = v; ApplyRotation(); }),
			_pitch, 0f, WakeUpDuration
		);

		await ToSignal(tween, "finished");

		GD.Print("[Temple3D] Wake-up complete.");
		_looking = true;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public override void _Input(InputEvent e)
	{
		if (!_looking || _cam == null) return;

		if (e is InputEventMouseMotion motion)
		{
			_yaw -= motion.Relative.X * LookSensitivity;
			_pitch -= motion.Relative.Y * LookSensitivity;
			_pitch = Mathf.Clamp(_pitch, PitchMin, PitchMax);
			ApplyRotation();
		}

		if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			HandleClick();
		}

		if (e is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
		{
			_looking = !_looking;
			Input.MouseMode = _looking ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
		}
	}

	void HandleClick()
	{
		if (_slab == null) return;

		// 先检测凹槽（层 1），命中则选加护；否则检测石板中央（层 2）进入世界
		var grooveHit = RaycastLayer(1);
		if (grooveHit != null)
		{
			if (grooveHit == _slab.GrooveInsight) _slab.ClickGroove(BlessingType.察知);
			else if (grooveHit == _slab.GrooveValor) _slab.ClickGroove(BlessingType.战意);
			else if (grooveHit == _slab.GrooveWanderer) _slab.ClickGroove(BlessingType.旅人);
			return;
		}

		var centerHit = RaycastLayer(2);
		if (centerHit != null && centerHit == _slab.SlabCenter)
			_slab.TryEnterWorld();
	}

	GodotObject RaycastLayer(uint layer)
	{
		var spaceState = GetWorld3D().DirectSpaceState;
		var from = _cam.GlobalPosition;
		var to = from - _cam.GlobalTransform.Basis.Z * 5f;

		var query = new PhysicsRayQueryParameters3D
		{
			From = from,
			To = to,
			CollisionMask = layer,
			CollideWithAreas = true,
			CollideWithBodies = false
		};

		var result = spaceState.IntersectRay(query);
		return result.Count > 0 ? result["collider"].AsGodotObject() : null;
	}

	public override void _Process(double delta)
	{
		ApplyEvolutionParams();
		UpdateHover();
	}

	int _hoverMissFrames;

	void UpdateHover()
	{
		if (!_looking || _slab == null) return;

		var hit = RaycastLayer(1);
		if (hit != null)
		{
			_hoverMissFrames = 0;
			if (hit == _slab.GrooveInsight) _slab.HoverGroove(BlessingType.察知);
			else if (hit == _slab.GrooveValor) _slab.HoverGroove(BlessingType.战意);
			else if (hit == _slab.GrooveWanderer) _slab.HoverGroove(BlessingType.旅人);
			else { _hoverMissFrames++; if (_hoverMissFrames >= 3) _slab.HoverGroove(null); }
			return;
		}

		_hoverMissFrames++;
		if (_hoverMissFrames >= 3)
			_slab.HoverGroove(null);
	}

	void ApplyRotation()
	{
		_cam.Rotation = new Vector3(
			Mathf.DegToRad(_pitch),
			Mathf.DegToRad(_yaw),
			0);
	}

	void ApplyEvolutionParams()
	{
		if (_evolution == null) return;

		_worldEnv ??= GetNodeOrNull<WorldEnvironment>("WorldEnvironment");
		if (_worldEnv?.Environment == null) return;

		var env = _worldEnv.Environment;
		env.FogDensity = _evolution.FogDensity;
		env.AmbientLightColor = _evolution.AmbientLight;

		// 演化视觉效果检查（随完整度推进）
		float comp = _evolution.Completeness;
		_evolution.UpdateSlabGlyphs(comp);
		_evolution.UpdateBackWall(comp);
		_evolution.UpdateGuideForm(comp);
	}

	void OnBlessingSelected(BlessingType type)
	{
		GD.Print($"[Temple3D] Blessing: {type}");
	}

	void OnEnterWorld()
	{
		GD.Print("[Temple3D] Entering world...");

		var cm = CycleManager.Instance;
		if (cm == null) return;

		var blessing = _slab?.SelectedBlessing;
		if (blessing == null) return;

		cm.SelectBlessing(blessing.Value);
		cm.EnterWorld();

		Input.MouseMode = Input.MouseModeEnum.Visible;
		GameManager.Instance.GoToScene(GameManager.SceneWorld);
	}
}
