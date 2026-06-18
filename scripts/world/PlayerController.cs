namespace No1.World;

using Godot;

public partial class PlayerController : CharacterBody2D
{
	[Export] public float Speed = 200f;
	[Export] public float ArriveThreshold = 8f;

	Vector2 _target;
	bool _moving;

	ColorRect _sprite;
	ColorRect _shadow;

	public override void _Ready()
	{
		// 碰撞体——BodyEntered 靠它
		var col = new CollisionShape2D();
		col.Shape = new CircleShape2D { Radius = 12 };
		AddChild(col);

		// 角色主体 — 纯色菱形占位
		_sprite = new ColorRect
		{
			Color = new Color(0.3f, 0.5f, 0.9f),
			Size = new Vector2(24, 40),
			Position = new Vector2(-12, -40)
		};
		AddChild(_sprite);

		// 脚下阴影
		_shadow = new ColorRect
		{
			Color = new Color(0, 0, 0, 0.3f),
			Size = new Vector2(20, 8),
			Position = new Vector2(-10, -2)
		};
		AddChild(_shadow);
	}

	public override void _Input(InputEvent e)
	{
		if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			_target = GetGlobalMousePosition();
			_moving = true;
			GD.Print($"[Player] click target: {_target}");
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_moving) return;

		var dir = _target - GlobalPosition;
		if (dir.Length() < ArriveThreshold)
		{
			_moving = false;
			Velocity = Vector2.Zero;
			return;
		}

		Velocity = dir.Normalized() * Speed;
		MoveAndSlide();
	}

	public bool IsMoving => _moving;
	public Vector2 MoveDirection => _moving ? (_target - GlobalPosition).Normalized() : Vector2.Zero;
}
