namespace No1.Temple;

using Godot;

/// <summary>
/// 神殿引导者 — 一团温暖粒子光，缓慢浮动。
/// 挂在 Guide (Node3D) 上。
/// </summary>
public partial class GuideLight : Node3D
{
	[Export] GpuParticles3D _particles;

	TempleEvolution _evolution;
	Vector3 _basePosition;
	Tween _floatTween;

	public override void _Ready()
	{
		_basePosition = Position;

		_evolution = GetNodeOrNull<TempleEvolution>("../Evolution");

		if (_particles == null)
		{
			_particles = GetNodeOrNull<GpuParticles3D>("Particles");
			if (_particles == null)
				GD.PrintErr("[GuideLight] Particles node not found. Set [Export] _particles in editor.");
		}

		StartFloating();
	}

	public override void _Process(double delta)
	{
		ApplyEvolution();
	}

	void StartFloating()
	{
		_floatTween?.Kill();

		var target = _basePosition + new Vector3(
			(float)GD.RandRange(-0.15, 0.15),
			(float)GD.RandRange(0, 0.15),
			(float)GD.RandRange(-0.15, 0.15));

		_floatTween = CreateTween();
		_floatTween.TweenProperty(this, "position", target, 2.5f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);

		var delay = (float)GD.RandRange(0.5, 2.0);
		_floatTween.TweenCallback(Callable.From(StartFloating)).SetDelay(delay);
	}

	void ApplyEvolution()
	{
		if (_evolution == null || _particles == null) return;

		float c = _evolution.Completeness;

		_particles.Amount = Mathf.RoundToInt(Mathf.Lerp(20, 60, c));
	}
}
