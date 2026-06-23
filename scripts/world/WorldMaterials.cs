namespace No1.World;
using Godot;

/// <summary>
/// Central material repository for the 3D world map.
/// All Build* methods reference materials from here.
/// Swap textures later without touching any Build* code.
/// </summary>
[GlobalClass]
public partial class WorldMaterials : Node
{
	public static WorldMaterials Instance { get; private set; }

	// ── Ground ──
	public StandardMaterial3D GrassBase    { get; private set; }
	public StandardMaterial3D ZoneForest   { get; private set; }
	public StandardMaterial3D ZoneMine     { get; private set; }
	public StandardMaterial3D ZoneCliff    { get; private set; }
	public StandardMaterial3D Path01       { get; private set; }
	public StandardMaterial3D Path12       { get; private set; }

	// ── Decorations ──
	public StandardMaterial3D DecoTree    { get; private set; }
	public StandardMaterial3D DecoRock    { get; private set; }
	public StandardMaterial3D DecoRuin    { get; private set; }

	// ── Background ──
	public StandardMaterial3D Sky         { get; private set; }
	public StandardMaterial3D Sun         { get; private set; }
	public StandardMaterial3D Mountain    { get; private set; }
	public StandardMaterial3D DragonShadow { get; private set; }

	// ── Decor ──
	public StandardMaterial3D EnemyDot   { get; private set; }
	public StandardMaterial3D PlayerBody { get; private set; }

	public override void _EnterTree()
	{
		Instance = this;
		BuildAll();
	}

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;
	}

	void BuildAll()
	{
		// Ground
		GrassBase  = MakeGroundWithNoise();
		ZoneForest = MakeFlat("ZoneForest", 0.14f, 0.33f, 0.14f);
		ZoneMine   = MakeFlat("ZoneMine", 0.28f, 0.25f, 0.22f);
		ZoneCliff  = MakeFlat("ZoneCliff", 0.38f, 0.33f, 0.18f);
		Path01     = MakeFlat("Path01", 0.42f, 0.33f, 0.18f);
		Path12     = MakeFlat("Path12", 0.38f, 0.30f, 0.15f);

		// Background
		Sky         = MakeFlat("Sky", 0.18f, 0.28f, 0.45f);
		Sun         = MakeFlat("Sun", 1f, 0.85f, 0.5f);
		Mountain    = MakeFlat("Mountain", 0.1f, 0.12f, 0.18f);
		DragonShadow = MakeTransparent("DragonShadow", 0, 0, 0, 0.25f);

		// Decor
		EnemyDot   = MakeFlat("EnemyDot", 0.8f, 0.3f, 0.3f);
		PlayerBody = MakeFlat("PlayerBody", 0.3f, 0.5f, 0.9f);

		// Decorations
		DecoTree = MakeFlat("DecoTree", 0.15f, 0.40f, 0.10f);
		DecoRock = MakeFlat("DecoRock", 0.35f, 0.33f, 0.30f);
		DecoRuin = MakeFlat("DecoRuin", 0.28f, 0.24f, 0.20f);
	}

	static StandardMaterial3D MakeFlat(string name, float r, float g, float b)
	{
		var mat = new StandardMaterial3D
		{
			AlbedoColor = new Color(r, g, b),
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};
		mat.ResourceName = name;
		return mat;
	}

	static StandardMaterial3D MakeTextured(string name, string path)
	{
		var tex = ResourceLoader.Load<Texture2D>(path);
		var mat = new StandardMaterial3D
		{
			AlbedoTexture = tex,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};
		mat.ResourceName = name;
		return mat;
	}

	static StandardMaterial3D MakeTransparent(string name, float r, float g, float b, float a)
	{
		var mat = new StandardMaterial3D
		{
			AlbedoColor = new Color(r, g, b, a),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};
		mat.ResourceName = name;
		return mat;
	}

	static StandardMaterial3D MakeGroundWithNoise()
	{
		var noise = new FastNoiseLite
		{
			Frequency = 0.04f,
			FractalOctaves = 3,
			FractalLacunarity = 2.5f,
			FractalGain = 0.5f
		};

		var img = Image.CreateEmpty(512, 512, false, Image.Format.Rgba8);
		for (int y = 0; y < 512; y++)
		for (int x = 0; x < 512; x++)
		{
			float n = noise.GetNoise2D(x, y) * 0.5f + 0.5f; // [0, 1]
			float g = 0.35f + n * 0.15f;   // green channel varies
			float r = 0.18f + n * 0.10f;
			float b = 0.10f + n * 0.08f;
			img.SetPixel(x, y, new Color(r, g, b));
		}

		var tex = ImageTexture.CreateFromImage(img);
		var mat = new StandardMaterial3D
		{
			AlbedoTexture = tex,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};
		mat.ResourceName = "GrassBase";
		return mat;
	}
}
