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
		// Collision
		var col = new CollisionShape3D();
		col.Shape = new CylinderShape3D { Height = 1f, Radius = 0.35f };
		AddChild(col);

		// Player sprite — 12×24 px pixel-art character, ~1.5m tall
		var body = new Sprite3D
		{
			Texture = MakeCharacterTexture(),
			Billboard = BaseMaterial3D.BillboardModeEnum.FixedY,
			Position = Vector3.Zero,
			PixelSize = 0.0625f,
			Offset = new Vector2(0, 24 * (0.917f - 0.5f)),  // feet at ground
			Name = "Body"
		};
		AddChild(body);

		// Shadow
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

	static ImageTexture MakeCharacterTexture()
	{
		const int W = 12, H = 24;
		var img = Image.CreateEmpty(W, H, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color skin  = new Color(0.96f, 0.80f, 0.65f);
		Color hair  = new Color(0.65f, 0.55f, 0.40f);
		Color shirt = new Color(0.25f, 0.45f, 0.75f);
		Color pants = new Color(0.30f, 0.30f, 0.38f);
		Color shoe  = new Color(0.20f, 0.15f, 0.10f);

		void Set(int x, int y, Color c) { if (x >= 0 && x < W && y >= 0 && y < H) img.SetPixel(x, y, c); }

		// Hair / head  (rows 0-3)
		for (int y = 0; y <= 2; y++)
		for (int x = 3; x <= 8; x++) Set(x, y, hair);
		Set(2, 2, hair); Set(9, 2, hair);

		// Face  (row 3)
		for (int x = 4; x <= 7; x++) Set(x, 3, skin);
		Set(5, 3, new Color(0.2f, 0.2f, 0.2f));  // left eye
		Set(7, 3, new Color(0.4f, 0.25f, 0.15f)); // right eye

		// Neck  (row 4)
		Set(5, 4, skin); Set(6, 4, skin);

		// Torso  (rows 5-9)
		for (int y = 5; y <= 9; y++)
		for (int x = 3; x <= 8; x++) Set(x, y, shirt);
		// belt
		for (int x = 3; x <= 8; x++) Set(x, 9, pants);

		// Arms  (rows 5-8)
		for (int y = 5; y <= 8; y++) { Set(2, y, skin); Set(9, y, skin); }

		// Hands
		Set(2, 8, skin); Set(9, 8, skin);

		// Legs  (rows 10-17)
		for (int y = 10; y <= 17; y++) { Set(4, y, pants); Set(5, y, pants); Set(6, y, pants); Set(7, y, pants); }

		// Feet  (rows 18-19)
		for (int y = 18; y <= 19; y++)
		for (int x = 4; x <= 7; x++) Set(x, y, shoe);

		// Foot detail — wider toe
		Set(4, 18, shoe); Set(7, 18, shoe);
		Set(3, 19, shoe); Set(8, 19, shoe);

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
