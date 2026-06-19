namespace No1.World;
using Godot;

/// <summary>
/// 3D player character. XZ-plane click-to-move via Camera3D raycast.
/// Renders as a colored Sprite3D billboard with shadow.
/// </summary>
public partial class Player3D : CharacterBody3D
{
	[Export] public float Speed = 5f;
	[Export] public float ArriveThreshold = 0.3f;

	Camera3D _camera;
	Vector3 _target;
	bool _moving;

	public bool IsMoving => _moving;
	public Vector3 MoveDirection => _moving ? (_target - GlobalPosition).Normalized() : Vector3.Zero;

	public override void _Ready()
	{
		// Collision — needed for Area3D BodyEntered detection
		var col = new CollisionShape3D();
		col.Shape = new CylinderShape3D { Height = 1f, Radius = 0.35f };
		AddChild(col);

		// Body — colored diamond sprite, always faces camera
		var body = new Sprite3D
		{
			Texture = MakeDiamondTexture(0.3f, 0.5f, 0.9f),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			Position = new Vector3(0, 0.8f, 0),
			PixelSize = 0.005f,
			Name = "Body"
		};
		AddChild(body);

		// Shadow — dark circle on ground
		var shadow = new Sprite3D
		{
			Texture = MakeCircleTexture(new Color(0, 0, 0, 0.35f)),
			Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
			RotationDegrees = new Vector3(-90, 0, 0),
			Position = new Vector3(0, 0.02f, 0),
			PixelSize = 0.005f,
			Name = "Shadow"
		};
		AddChild(shadow);
	}

	public void SetCamera(Camera3D cam) => _camera = cam;

	public override void _Input(InputEvent e)
	{
		if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			if (_camera == null) return;

			var from = _camera.ProjectRayOrigin(mb.Position);
			var dir = _camera.ProjectRayNormal(mb.Position);

			// Raycast to Y=0 plane (ground)
			if (dir.Y >= 0) return; // pointing upward, won't hit ground
			float t = -from.Y / dir.Y;
			if (t <= 0) return;

			_target = from + dir * t;
			_target.Y = 0;
			_moving = true;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_moving) return;

		var toTarget = _target - GlobalPosition;
		toTarget.Y = 0; // stay on ground plane
		if (toTarget.Length() < ArriveThreshold)
		{
			_moving = false;
			Velocity = Vector3.Zero;
			return;
		}

		Velocity = toTarget.Normalized() * Speed;
		MoveAndSlide();
	}

	// ── Dummy textures (replace with real sprites later) ──

	static ImageTexture MakeDiamondTexture(float r, float g, float b)
	{
		var img = Image.CreateEmpty(12, 20, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0)); // transparent bg
		for (int y = 0; y < 20; y++)
		for (int x = 0; x < 12; x++)
		{
			float dy = Mathf.Abs(y - 10f) / 10f;
			float dx = Mathf.Abs(x - 6f) / 6f;
			if (dx + dy <= 1f)
				img.SetPixel(x, y, new Color(r, g, b));
		}
		return ImageTexture.CreateFromImage(img);
	}

	static ImageTexture MakeCircleTexture(Color c)
	{
		var img = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));
		for (int y = 0; y < 16; y++)
		for (int x = 0; x < 16; x++)
		{
			float dx = (x - 8f) / 8f;
			float dy = (y - 8f) / 8f;
			if (dx * dx + dy * dy <= 1f)
				img.SetPixel(x, y, c);
		}
		return ImageTexture.CreateFromImage(img);
	}
}
