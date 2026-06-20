namespace No1.World;
using Godot;

/// <summary>
/// 3D player character. WASD movement on XZ plane.
/// MoveDirection exposed for camera tracking and future mobile joystick.
/// </summary>
public partial class Player3D : CharacterBody3D
{
	[Export] public float Speed = 5f;
	public Vector3 MoveDirection { get; private set; }

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

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		var input = Vector3.Zero;

		var dm = No1.UI.DialogueManager.Instance;
		if (dm == null || !dm.IsOverlayOpen)
		{
			if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))    input.Z -= 1;
			if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))  input.Z += 1;
			if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))  input.X -= 1;
			if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) input.X += 1;
		}

		MoveDirection = input.Normalized();
		Translate(MoveDirection * Speed * dt);
	}

	// ── Dummy textures (replace with real sprites later) ──

	static ImageTexture MakeDiamondTexture(float r, float g, float b)
	{
		var img = Image.CreateEmpty(12, 20, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));
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
