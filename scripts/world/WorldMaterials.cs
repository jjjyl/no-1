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
	[Export]
	public StandardMaterial3D GrassBase    { get; private set; }
	[Export]
	public StandardMaterial3D ZoneForest   { get; private set; }
	[Export]
	public StandardMaterial3D ZoneMine     { get; private set; }
	[Export]
	public StandardMaterial3D ZoneCliff    { get; private set; }
	[Export]
	public StandardMaterial3D Path01       { get; private set; }
	[Export]
	public StandardMaterial3D Path12       { get; private set; }
	[Export]
	public StandardMaterial3D ZoneBattlefield { get; private set; }
	[Export]
	public StandardMaterial3D ZoneCrystal    { get; private set; }
	[Export]
	public StandardMaterial3D ZoneWasteland  { get; private set; }
	[Export]
	public StandardMaterial3D ZoneTower      { get; private set; }
	[Export]
	public StandardMaterial3D ZoneSpring     { get; private set; }
	[Export]
	public StandardMaterial3D Path34         { get; private set; }

	// ── Decorations ──
	[Export]
	public StandardMaterial3D DecoTree    { get; private set; }
	[Export]
	public StandardMaterial3D DecoRock    { get; private set; }
	[Export]
	public StandardMaterial3D DecoRuin    { get; private set; }

	// ── Background ──
	[Export]
	public StandardMaterial3D Sky         { get; private set; }
	[Export]
	public StandardMaterial3D Sun         { get; private set; }
	[Export]
	public StandardMaterial3D Mountain    { get; private set; }
	[Export]
	public StandardMaterial3D DragonShadow { get; private set; }

	// ── Decor ──
	[Export]
	public StandardMaterial3D EnemyDot   { get; private set; }
	[Export]
	public StandardMaterial3D PlayerBody { get; private set; }
	[Export]
	public StandardMaterial3D CompanionDot { get; private set; }
	[Export]
	public StandardMaterial3D ShopMarker   { get; private set; }

	public override void _EnterTree()
	{
		Instance = this;
		BuildAll();
	}

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;
	}

	// ═══════════════════════════════════════════════════════════════
	//  TEXTURE OVERRIDE SYSTEM
	//  Drop PNG files into res://assets/texture/world/ to replace
	//  procedural textures. File names: see table below.
	//  If file not found -> procedural fallback is used automatically.
	//  Paths: grass_base.png, zone_forest.png, zone_mine.png,
	//  zone_cliff.png, zone_battlefield.png, zone_crystal.png,
	//  zone_wasteland.png, zone_tower.png, zone_spring.png,
	//  path_dirt.png, sky_gradient.png, sun.png, mountain.png,
	//  dragon_shadow.png, marker_enemy.png, marker_player.png,
	//  marker_companion.png, marker_shop.png,
	//  deco_tree.png, deco_rock.png, deco_ruin.png
	// ═══════════════════════════════════════════════════════════════

	static Texture2D TryLoadTexture(string path)
	{
		if (ResourceLoader.Exists(path))
		{
			try { var res = ResourceLoader.Load<Texture2D>(path); if (res != null) return res; }
			catch { }
		}
		if (FileAccess.FileExists(path))
		{
			try { var img = Image.LoadFromFile(path); if (img != null && !img.IsEmpty()) return ImageTexture.CreateFromImage(img); }
			catch { }
		}
		return null;
	}

	void BuildAll()
	{
		// ── Ground (matte: Metallic=0, Roughness=0.9) ──

		GrassBase = MakeGroundMat("GrassBase",
			TryLoadTexture("res://assets/texture/world/grass_base.png") ?? MakeNoiseTexture(0.08f, new Color(0.18f, 0.35f, 0.10f), new Color(0.28f, 0.50f, 0.18f), 64));

		ZoneForest = MakeGroundMat("ZoneForest",
			TryLoadTexture("res://assets/texture/world/zone_forest.png") ?? MakeDitherTexture(new Color(0.14f, 0.33f, 0.14f), new Color(0.09f, 0.26f, 0.09f), 16, pattern: 0));

		ZoneMine = MakeGroundMat("ZoneMine",
			TryLoadTexture("res://assets/texture/world/zone_mine.png") ?? MakeDitherTexture(new Color(0.28f, 0.25f, 0.22f), new Color(0.22f, 0.19f, 0.16f), 16, pattern: 1));

		ZoneCliff = MakeGroundMat("ZoneCliff",
			TryLoadTexture("res://assets/texture/world/zone_cliff.png") ?? MakeDitherTexture(new Color(0.38f, 0.33f, 0.18f), new Color(0.31f, 0.27f, 0.13f), 16, pattern: 2));

		ZoneBattlefield = MakeGroundMat("ZoneBattlefield",
			TryLoadTexture("res://assets/texture/world/zone_battlefield.png") ?? MakeDitherTexture(new Color(0.35f, 0.30f, 0.22f), new Color(0.28f, 0.24f, 0.16f), 16, pattern: 3));

		ZoneCrystal = MakeGroundMat("ZoneCrystal",
			TryLoadTexture("res://assets/texture/world/zone_crystal.png") ?? MakeDitherTexture(new Color(0.40f, 0.45f, 0.55f), new Color(0.33f, 0.38f, 0.48f), 16, pattern: 0));

		ZoneWasteland = MakeGroundMat("ZoneWasteland",
			TryLoadTexture("res://assets/texture/world/zone_wasteland.png") ?? MakeDitherTexture(new Color(0.30f, 0.20f, 0.15f), new Color(0.24f, 0.15f, 0.10f), 16, pattern: 1));

		ZoneTower = MakeGroundMat("ZoneTower",
			TryLoadTexture("res://assets/texture/world/zone_tower.png") ?? MakeDitherTexture(new Color(0.18f, 0.16f, 0.24f), new Color(0.13f, 0.11f, 0.18f), 16, pattern: 2));

		ZoneSpring = MakeGroundMat("ZoneSpring",
			TryLoadTexture("res://assets/texture/world/zone_spring.png") ?? MakeDitherTexture(new Color(0.18f, 0.38f, 0.35f), new Color(0.13f, 0.31f, 0.28f), 16, pattern: 3));

		// Paths -- lighter second color for cobblestone/dirt path look
		Path01 = MakeGroundMat("Path01",
			TryLoadTexture("res://assets/texture/world/path_dirt.png") ?? MakeDitherTexture(new Color(0.42f, 0.33f, 0.18f), new Color(0.54f, 0.44f, 0.26f), 16, pattern: 0));

		Path12 = MakeGroundMat("Path12",
			TryLoadTexture("res://assets/texture/world/path_dirt.png") ?? MakeDitherTexture(new Color(0.38f, 0.30f, 0.15f), new Color(0.50f, 0.41f, 0.23f), 16, pattern: 1));

		Path34 = MakeGroundMat("Path34",
			TryLoadTexture("res://assets/texture/world/path_dirt.png") ?? MakeDitherTexture(new Color(0.40f, 0.32f, 0.20f), new Color(0.52f, 0.43f, 0.28f), 16, pattern: 3));

		// ── Background ──

		Sky = MakeBgMat("Sky",
			TryLoadTexture("res://assets/texture/world/sky_gradient.png") ?? MakePixelGradient(64, 32, new Color(0.10f, 0.18f, 0.35f), new Color(0.25f, 0.40f, 0.58f)));

		Sun = MakeBgTransparentMat("Sun",
			TryLoadTexture("res://assets/texture/world/sun.png") ?? MakeSunTexture(32));

		Mountain = MakeBgTransparentMat("Mountain",
			TryLoadTexture("res://assets/texture/world/mountain.png") ?? MakeMountainSilhouette(64, 32, new Color(0.10f, 0.12f, 0.18f)));

		DragonShadow = MakeBgTransparentMat("DragonShadow",
			TryLoadTexture("res://assets/texture/world/dragon_shadow.png") ?? MakeWingTexture(32, 32));

		// ── Markers (slight shine: Metallic=0, Roughness=0.5) ──

		EnemyDot = MakeMarkerMat("EnemyDot",
			TryLoadTexture("res://assets/texture/world/marker_enemy.png") ?? MakeSkullTexture(16, new Color(0.8f, 0.3f, 0.3f)));

		PlayerBody = MakeMarkerMat("PlayerBody",
			TryLoadTexture("res://assets/texture/world/marker_player.png") ?? MakeDiamondTexture(16, new Color(0.3f, 0.5f, 0.9f)));

		CompanionDot = MakeMarkerMat("CompanionDot",
			TryLoadTexture("res://assets/texture/world/marker_companion.png") ?? MakeHeartTexture(16, new Color(0.15f, 0.55f, 0.50f)));

		ShopMarker = MakeMarkerMat("ShopMarker",
			TryLoadTexture("res://assets/texture/world/marker_shop.png") ?? MakeCoinTexture(16, new Color(0.9f, 0.75f, 0.2f)));

		// ── Decorations (matte ground-style) ──

		DecoTree = MakeGroundMat("DecoTree",
			TryLoadTexture("res://assets/texture/world/deco_tree.png") ?? MakeCrossHatchTexture(16, new Color(0.15f, 0.40f, 0.10f)));

		DecoRock = MakeGroundMat("DecoRock",
			TryLoadTexture("res://assets/texture/world/deco_rock.png") ?? MakeSpeckleTexture(16, new Color(0.35f, 0.33f, 0.30f)));

		DecoRuin = MakeGroundMat("DecoRuin",
			TryLoadTexture("res://assets/texture/world/deco_ruin.png") ?? MakeBrickTexture(16, new Color(0.28f, 0.24f, 0.20f)));
	}

	// ═══════════════════════════════════════════════════════════════
	//  Preserved factory methods (may be used elsewhere)
	// ═══════════════════════════════════════════════════════════════

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
			Frequency = 0.06f,
			FractalOctaves = 3,
			FractalLacunarity = 2.5f,
			FractalGain = 0.5f
		};

		var img = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
		int levels = 6;
		for (int y = 0; y < 64; y++)
		for (int x = 0; x < 64; x++)
		{
			float n = noise.GetNoise2D(x, y) * 0.5f + 0.5f;
			// Quantize to visible colour bands for pixel-art look
			float q = Mathf.Round(n * (levels - 1)) / (levels - 1);
			float g = 0.33f + q * 0.18f;
			float r = 0.17f + q * 0.12f;
			float b = 0.09f + q * 0.10f;
			img.SetPixel(x, y, new Color(r, g, b));
		}

		var tex = ImageTexture.CreateFromImage(img);
		var mat = new StandardMaterial3D
		{
			AlbedoTexture = tex,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
			Metallic = 0f,
			Roughness = 0.9f,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest
		};
		mat.ResourceName = "GrassBase";
		return mat;
	}

	// ═══════════════════════════════════════════════════════════════
	//  Material wrapper helpers (construct StandardMaterial3D)
	// ═══════════════════════════════════════════════════════════════

	static StandardMaterial3D MakeGroundMat(string name, Texture2D tex)
	{
		var mat = new StandardMaterial3D
		{
			AlbedoTexture = tex,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
			Metallic = 0f,
			Roughness = 0.9f,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest
		};
		mat.ResourceName = name;
		return mat;
	}

	static StandardMaterial3D MakeBgMat(string name, Texture2D tex)
	{
		var mat = new StandardMaterial3D
		{
			AlbedoTexture = tex,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
			Metallic = 0f,
			Roughness = 1.0f,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest
		};
		mat.ResourceName = name;
		return mat;
	}

	static StandardMaterial3D MakeBgTransparentMat(string name, Texture2D tex)
	{
		var mat = new StandardMaterial3D
		{
			AlbedoTexture = tex,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			Metallic = 0f,
			Roughness = 1.0f,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest
		};
		mat.ResourceName = name;
		return mat;
	}

	static StandardMaterial3D MakeMarkerMat(string name, Texture2D tex)
	{
		var mat = new StandardMaterial3D
		{
			AlbedoTexture = tex,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
			Metallic = 0f,
			Roughness = 0.5f,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest
		};
		mat.ResourceName = name;
		return mat;
	}

	// ═══════════════════════════════════════════════════════════════
	//  Procedural texture generators (return ImageTexture)
	// ═══════════════════════════════════════════════════════════════

	/// <summary>
	/// 50 % ordered-dither pattern between two colours.
	/// pattern: 0=checker, 1=horiz-stripes, 2=vert-stripes, 3=diagonal-stripes.
	/// </summary>
	static ImageTexture MakeDitherTexture(Color a, Color b, int size, int pattern = 0)
	{
		var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		for (int y = 0; y < size; y++)
		for (int x = 0; x < size; x++)
		{
			bool on;
			switch (pattern)
			{
				case 1: on = (y & 1) == 0; break;              // horizontal 1 px stripes
				case 2: on = (x & 1) == 0; break;              // vertical 1 px stripes
				case 3: on = ((x + y) & 2) == 0; break;        // 2 px diagonal blocks
				default: on = ((x + y) & 1) == 0; break;       // classic checker
			}
			img.SetPixel(x, y, on ? a : b);
		}
		return ImageTexture.CreateFromImage(img);
	}

	/// <summary>
	/// FastNoiseLite-based noise texture with colour quantisation for pixel-art banding.
	/// </summary>
	static ImageTexture MakeNoiseTexture(float freq, Color baseCol, Color varCol, int size)
	{
		var noise = new FastNoiseLite
		{
			Frequency = freq,
			FractalOctaves = 3,
			FractalLacunarity = 2.5f,
			FractalGain = 0.5f
		};
		var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		int levels = 5;
		for (int y = 0; y < size; y++)
		for (int x = 0; x < size; x++)
		{
			float n = noise.GetNoise2D(x, y) * 0.5f + 0.5f;
			float q = Mathf.Round(n * (levels - 1)) / (levels - 1);
			img.SetPixel(x, y, baseCol.Lerp(varCol, q));
		}
		return ImageTexture.CreateFromImage(img);
	}

	/// <summary>
	/// Vertical pixel-band gradient (4–8 visible bands).
	/// </summary>
	static ImageTexture MakePixelGradient(int w, int h, Color top, Color bottom)
	{
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		int bands = 8;
		for (int y = 0; y < h; y++)
		{
			int band = y * bands / h;
			float t = (float)band / (bands - 1);
			Color c = top.Lerp(bottom, t);
			for (int x = 0; x < w; x++)
				img.SetPixel(x, y, c);
		}
		return ImageTexture.CreateFromImage(img);
	}

	/// <summary>
	/// Pixel-art sun: yellow circle with 8-ray dithered edge on transparent background.
	/// </summary>
	static ImageTexture MakeSunTexture(int size)
	{
		var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		int cx = size / 2;
		int cy = size / 2;
		float radius = size * 0.34f;
		Color sunCol = new Color(1f, 0.85f, 0.5f);
		Color rayCol = new Color(1f, 0.85f, 0.5f, 0.55f);
		Color edgeCol = new Color(1f, 0.90f, 0.60f, 0.20f);
		Color clear = new Color(0, 0, 0, 0);

		for (int y = 0; y < size; y++)
		for (int x = 0; x < size; x++)
		{
			float dx = x - cx;
			float dy = y - cy;
			float dist = Mathf.Sqrt(dx * dx + dy * dy);
			float angle = Mathf.Atan2(dy, dx);
			// 8-ray dither: rays point in 8 cardinal directions
			int ray = Mathf.RoundToInt((angle + Mathf.Pi) / (Mathf.Pi * 0.25f)) % 8;

			if (dist <= radius)
			{
				img.SetPixel(x, y, sunCol);
			}
			else if (dist <= radius + 4f)
			{
				// Dithered edge: checkerboard for soft transition
				bool dither = ((x + y) & 1) == 0;
				if (dist <= radius + 2f)
					img.SetPixel(x, y, dither ? rayCol : clear);
				else
					img.SetPixel(x, y, dither ? edgeCol : clear);
			}
			else
			{
				img.SetPixel(x, y, clear);
			}
		}
		return ImageTexture.CreateFromImage(img);
	}

	/// <summary>
	/// Jagged pixel-art mountain silhouette on transparent background.
	/// </summary>
	static ImageTexture MakeMountainSilhouette(int w, int h, Color c)
	{
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		Color clear = new Color(0, 0, 0, 0);

		// Build random peak heights with fixed seed for determinism
		int numPeaks = 8;
		int[] peaks = new int[numPeaks];
		var rng = new System.Random(42);
		for (int i = 0; i < numPeaks; i++)
			peaks[i] = rng.Next(h / 3, h - 2);   // peaks range from middle to near top

		for (int x = 0; x < w; x++)
		{
			// Which two peaks are we between?
			float seg = (float)x / w * (numPeaks - 1);
			int i0 = Mathf.FloorToInt(seg);
			int i1 = Mathf.Min(i0 + 1, numPeaks - 1);
			float t = seg - i0;

			float peakH = Mathf.Lerp(peaks[i0], peaks[i1], t);
			int topY = Mathf.RoundToInt(h - peakH);

			// Add pixel-art jitter to the silhouette edge
			int jitter = ((x >> 1) & 1) == 0 ? 0 : 1;
			int edgeY = topY + jitter;

			for (int y = 0; y < h; y++)
			{
				if (y >= edgeY)
					img.SetPixel(x, y, c);
				else
					img.SetPixel(x, y, clear);
			}
		}
		return ImageTexture.CreateFromImage(img);
	}

	/// <summary>
	/// White pixel clusters on transparent background (for potential cloud use).
	/// </summary>
	static ImageTexture MakeCloudTexture(int w, int h)
	{
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		Color clear = new Color(0, 0, 0, 0);
		Color white = new Color(1, 1, 1, 0.85f);
		Color softWhite = new Color(1, 1, 1, 0.35f);

		// Fill with transparent first
		for (int y = 0; y < h; y++)
		for (int x = 0; x < w; x++)
			img.SetPixel(x, y, clear);

		// Place random cloud clusters
		var rng = new System.Random(77);
		int clusters = rng.Next(4, 7);
		for (int i = 0; i < clusters; i++)
		{
			int cx = rng.Next(w / 4, 3 * w / 4);
			int cy = rng.Next(h / 4, 3 * h / 4);
			int cr = rng.Next(3, 6);
			for (int y = Mathf.Max(0, cy - cr); y < Mathf.Min(h, cy + cr); y++)
			for (int x = Mathf.Max(0, cx - cr); x < Mathf.Min(w, cx + cr); x++)
			{
				float dx = x - cx;
				float dy = y - cy;
				float dist = Mathf.Sqrt(dx * dx + dy * dy);
				if (dist <= cr)
					img.SetPixel(x, y, white);
				else if (dist <= cr + 1.5f && ((x + y) & 1) == 0)
					img.SetPixel(x, y, softWhite);
			}
		}
		return ImageTexture.CreateFromImage(img);
	}

	// ═══════════════════════════════════════════════════════════════
	//  Small icon generators for markers & decor
	// ═══════════════════════════════════════════════════════════════

	/// <summary>Pixel-art bat/dragon wing silhouette for DragonShadow.</summary>
	static ImageTexture MakeWingTexture(int w, int h)
	{
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		Color clear = new Color(0, 0, 0, 0);
		Color shadow = new Color(0, 0, 0, 0.30f);
		Color dark = new Color(0, 0, 0, 0.15f);

		int cx = w / 3;        // wing attaches near left-center
		int cy = h / 2;
		int wingSpan = w * 3 / 4;

		for (int y = 0; y < h; y++)
		for (int x = 0; x < w; x++)
		{
			// Wing membrane: triangular shape from body outward and upward/downward
			int dx = x - cx;
			int dy = y - cy;
			if (dx < 0 || dx > wingSpan)
			{
				img.SetPixel(x, y, clear);
				continue;
			}
			// Top wing edge (curves up)
			int topEdge = cy - (dx * h / 2) / wingSpan + dx / 2;
			// Bottom wing edge (curves down)
			int botEdge = cy + (dx * h / 2) / wingSpan - dx / 3;

			// Add pixel-art scalloped trailing edge
			int scallop = ((dx >> 2) & 1) == 0 ? 2 : 0;
			int actualTop = topEdge + scallop;
			int actualBot = botEdge - scallop;

			if (y >= actualTop && y <= actualBot)
			{
				// Wing rib lines for structure
				bool rib = (dx % 7 == 0 || dx % 7 == 1);
				// Body attachment area (solid)
				bool body = dx < 4;
				// Dithered fill
				bool dither = ((x + y) & 1) == 0;
				if (body)
					img.SetPixel(x, y, shadow);
				else if (rib)
					img.SetPixel(x, y, shadow);
				else if (dither)
					img.SetPixel(x, y, dark);
				else
					img.SetPixel(x, y, clear);
			}
			else
			{
				img.SetPixel(x, y, clear);
			}
		}
		return ImageTexture.CreateFromImage(img);
	}

	/// <summary>Pixel-art skull icon for enemy marker.</summary>
	static ImageTexture MakeSkullTexture(int size, Color c)
	{
		var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		int cx = size / 2;
		int cy = size / 2;
		int r = size / 2 - 1;
		Color dark = new Color(c.R * 0.3f, c.G * 0.3f, c.B * 0.3f);
		Color light = new Color(
			Mathf.Min(c.R * 1.3f, 1f),
			Mathf.Min(c.G * 1.3f, 1f),
			Mathf.Min(c.B * 1.3f, 1f));
		Color clear = new Color(0, 0, 0, 0);

		for (int y = 0; y < size; y++)
		for (int x = 0; x < size; x++)
		{
			int dx = x - cx;
			int dy = y - cy;
			float dist = Mathf.Sqrt(dx * dx + dy * dy);

			if (dist > r)
			{
				img.SetPixel(x, y, clear);
				continue;
			}

			// Skull features inside the circle
			bool eyeLeft  = dx > -5 && dx < -1 && dy > -3 && dy < 1;    // left eye socket
			bool eyeRight = dx > 1  && dx < 5  && dy > -3 && dy < 1;    // right eye socket
			bool nose     = dx > -1 && dx < 2  && dy > 1  && dy < 3;    // nose hole
			bool mouthTop = dy > 4 && dy < 7  && dx > -4 && dx < 5;     // mouth area
			bool tooth    = mouthTop && (dx & 1) == 0;                   // teeth

			if (eyeLeft || eyeRight || nose)
				img.SetPixel(x, y, dark);
			else if (tooth)
				img.SetPixel(x, y, light);
			else if (mouthTop)
				img.SetPixel(x, y, dark);
			else
				img.SetPixel(x, y, c);
		}
		return ImageTexture.CreateFromImage(img);
	}

	/// <summary>Pixel-art diamond shape for player marker.</summary>
	static ImageTexture MakeDiamondTexture(int size, Color c)
	{
		var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		int cx = size / 2;
		int cy = size / 2;
		Color dark = new Color(c.R * 0.6f, c.G * 0.6f, c.B * 0.6f);
		Color light = new Color(
			Mathf.Min(c.R * 1.4f, 1f),
			Mathf.Min(c.G * 1.4f, 1f),
			Mathf.Min(c.B * 1.4f, 1f));
		Color clear = new Color(0, 0, 0, 0);

		for (int y = 0; y < size; y++)
		for (int x = 0; x < size; x++)
		{
			int dx = Mathf.Abs(x - cx);
			int dy = Mathf.Abs(y - cy);
			// Diamond: |dx| + |dy| <= radius
			float md = dx + dy;
			int outer = size / 2 + 1;
			int inner = size / 2 - 1;
			int core = size / 3;

			if (md > outer)
				img.SetPixel(x, y, clear);
			else if (md == outer)
				img.SetPixel(x, y, dark);               // outline
			else if (md <= core)
				img.SetPixel(x, y, light);              // bright center
			else if (((x + y) & 1) == 0)
				img.SetPixel(x, y, c);                  // dither fill
			else
				img.SetPixel(x, y, dark);
		}
		return ImageTexture.CreateFromImage(img);
	}

	/// <summary>Pixel-art heart shape for companion marker.</summary>
	static ImageTexture MakeHeartTexture(int size, Color c)
	{
		var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		int cx = size / 2;
		int cy = size / 2;
		Color light = new Color(
			Mathf.Min(c.R * 1.3f, 1f),
			Mathf.Min(c.G * 1.3f, 1f),
			Mathf.Min(c.B * 1.3f, 1f));
		Color dark = new Color(c.R * 0.5f, c.G * 0.5f, c.B * 0.5f);
		Color clear = new Color(0, 0, 0, 0);

		for (int y = 0; y < size; y++)
		for (int x = 0; x < size; x++)
		{
			float fx = (float)(x - cx) / (size * 0.45f);
			float fy = (float)(cy - y) / (size * 0.45f);
			// Heart equation: (x^2 + y^2 - 1)^3 - x^2 * y^3 <= 0
			float val = fx * fx + fy * fy - 1f;
			float heart = val * val * val - fx * fx * fy * fy * fy;

			if (heart <= 0.05f)
			{
				if (heart <= -0.3f)
					img.SetPixel(x, y, light);          // bright center-left lobe
				else if (((x + y) & 1) == 0)
					img.SetPixel(x, y, c);              // dither fill
				else
					img.SetPixel(x, y, dark);           // dither shadow
			}
			else
			{
				img.SetPixel(x, y, clear);
			}
		}
		return ImageTexture.CreateFromImage(img);
	}

	/// <summary>Pixel-art coin icon for shop marker.</summary>
	static ImageTexture MakeCoinTexture(int size, Color c)
	{
		var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		int cx = size / 2;
		int cy = size / 2;
		int r = size / 2 - 1;
		Color dark = new Color(c.R * 0.55f, c.G * 0.55f, c.B * 0.55f);
		Color light = new Color(
			Mathf.Min(c.R * 1.3f, 1f),
			Mathf.Min(c.G * 1.3f, 1f),
			Mathf.Min(c.B * 1.3f, 1f));
		Color clear = new Color(0, 0, 0, 0);

		for (int y = 0; y < size; y++)
		for (int x = 0; x < size; x++)
		{
			int dx = x - cx;
			int dy = y - cy;
			float dist = Mathf.Sqrt(dx * dx + dy * dy);

			if (dist > r + 0.5f)
			{
				img.SetPixel(x, y, clear);
			}
			else if (dist >= r - 0.5f && ((x + y) & 1) == 0)
			{
				img.SetPixel(x, y, dark);               // dithered outline
			}
			else if (y == cy - 1 || y == cy || y == cy + 1)
			{
				// Horizontal band across middle for coin detail
				if (dx > -4 && dx < 4)
					img.SetPixel(x, y, dark);
				else
					img.SetPixel(x, y, (y & 1) == 0 ? c : light);
			}
			else
			{
				// Fill with checker-dither for metallic sheen
				img.SetPixel(x, y, ((x + y) & 1) == 0 ? light : c);
			}
		}
		return ImageTexture.CreateFromImage(img);
	}

	/// <summary>Cross-hatch pattern for tree canopy decoration.</summary>
	static ImageTexture MakeCrossHatchTexture(int size, Color c)
	{
		var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		Color dark = new Color(c.R * 0.45f, c.G * 0.45f, c.B * 0.45f);
		Color light = new Color(
			Mathf.Min(c.R * 1.25f, 1f),
			Mathf.Min(c.G * 1.25f, 1f),
			Mathf.Min(c.B * 1.25f, 1f));

		for (int y = 0; y < size; y++)
		for (int x = 0; x < size; x++)
		{
			// Two diagonal line families crossing each other
			bool diag1 = ((x + y) % 5) < 2;
			bool diag2 = ((x - y + size) % 5) < 2;

			if (diag1 && diag2)
				img.SetPixel(x, y, light);           // crossing point = highlight
			else if (diag1 || diag2)
				img.SetPixel(x, y, c);               // single line
			else
				img.SetPixel(x, y, dark);            // gap
		}
		return ImageTexture.CreateFromImage(img);
	}

	/// <summary>Speckle / noise-like pattern for rock decoration.</summary>
	static ImageTexture MakeSpeckleTexture(int size, Color c)
	{
		var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		var rng = new System.Random(1337);

		for (int y = 0; y < size; y++)
		for (int x = 0; x < size; x++)
		{
			float v = (float)rng.NextDouble();
			// 4-level speckle for pixel-art banding
			float t;
			if (v < 0.25f)      t = -0.12f;
			else if (v < 0.55f) t = -0.04f;
			else if (v < 0.80f) t = 0.04f;
			else                t = 0.10f;

			Color col = new Color(
				Mathf.Clamp(c.R + t, 0f, 1f),
				Mathf.Clamp(c.G + t, 0f, 1f),
				Mathf.Clamp(c.B + t, 0f, 1f));
			img.SetPixel(x, y, col);
		}
		return ImageTexture.CreateFromImage(img);
	}

	/// <summary>Brick-like pattern for ruin decoration.</summary>
	static ImageTexture MakeBrickTexture(int size, Color c)
	{
		var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		Color mortar = new Color(c.R * 0.55f, c.G * 0.55f, c.B * 0.55f);
		Color light = new Color(
			Mathf.Min(c.R * 1.2f, 1f),
			Mathf.Min(c.G * 1.2f, 1f),
			Mathf.Min(c.B * 1.2f, 1f));
		int brickH = 4;   // brick height in pixels
		int mortarW = 1;  // mortar width

		for (int y = 0; y < size; y++)
		for (int x = 0; x < size; x++)
		{
			int row = y / brickH;
			bool isMortarH = (y % brickH) < mortarW;  // horizontal mortar line
			// Vertical mortar: offset every other row
			int offset = (row % 2) * (size / 3);       // half-brick offset
			int xAdj = (x + offset) % size;
			bool isMortarV = (xAdj % (size / 3)) < mortarW;

			if (isMortarH || isMortarV)
				img.SetPixel(x, y, mortar);
			else if (((x + y) & 2) == 0)
				img.SetPixel(x, y, c);
			else
				img.SetPixel(x, y, light);
		}
		return ImageTexture.CreateFromImage(img);
	}
}
