namespace No1.World;
using Godot;
using System;

/// <summary>
/// Static texture and mesh generation helpers for world rendering.
/// Shared by WorldMap3D (parallax layers, detailed pixel art) and ChunkManager (decoration scattering).
/// </summary>
public static class WorldTextures
{
	// ═══════════════════════════════════════════════════════════════
	//  Shared helpers
	// ═══════════════════════════════════════════════════════════════

	public static Texture2D TryLoadTexture(string path)
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

	// ═══════════════════════════════════════════════════════════════
	//  Basic shape textures
	// ═══════════════════════════════════════════════════════════════

	public static ImageTexture MakeColorTexture(Color c, int w = 4, int h = 4)
	{
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(c);
		return ImageTexture.CreateFromImage(img);
	}

	public static ImageTexture MakeCircleTexture(Color c, int size = 32)
	{
		var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));
		float half = size * 0.5f;
		for (int y = 0; y < size; y++)
		for (int x = 0; x < size; x++)
		{
			float dx = (x - half) / half;
			float dy = (y - half) / half;
			if (dx * dx + dy * dy <= 1f)
				img.SetPixel(x, y, c);
		}
		return ImageTexture.CreateFromImage(img);
	}

	// ═══════════════════════════════════════════════════════════════
	//  Pixel-art textures — detailed parallax sprites (WorldMap3D)
	// ═══════════════════════════════════════════════════════════════

	public static ImageTexture MakePixelTreeTexture(Color canopyColor)
	{
		int w = 24, h = 32;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color trunk = new Color(0.30f, 0.20f, 0.10f);
		Color darkGreen = new Color(canopyColor.R * 0.60f, canopyColor.G * 0.60f, canopyColor.B * 0.60f);
		Color darkerGreen = new Color(canopyColor.R * 0.35f, canopyColor.G * 0.35f, canopyColor.B * 0.35f);

		for (int y = 2; y <= 17; y++)
		{
			int halfW;
			if (y <= 3) halfW = 2;
			else if (y <= 4) halfW = 3;
			else if (y <= 5) halfW = 4;
			else if (y <= 6) halfW = 5;
			else if (y <= 8) halfW = 6;
			else if (y <= 12) halfW = 7;
			else if (y <= 14) halfW = 6;
			else if (y <= 15) halfW = 5;
			else if (y <= 16) halfW = 3;
			else halfW = 2;

			for (int x = w / 2 - halfW; x <= w / 2 + halfW; x++)
			{
				if (x < 0 || x >= w) continue;
				Color c = canopyColor;
				int shade = (x * 7 + y * 13) % 8;
				if (shade == 0) c = darkerGreen;
				else if (shade == 1 || shade == 2) c = darkGreen;
				img.SetPixel(x, y, c);
			}
		}

		for (int y = 15; y <= 31; y++)
		{
			if (y >= 24)
			{
				img.SetPixel(10, y, trunk);
				img.SetPixel(11, y, trunk);
				img.SetPixel(12, y, trunk);
			}
			else if (y >= 18)
			{
				Color tc = (y % 2 == 0) ? trunk : new Color(trunk.R * 0.85f, trunk.G * 0.85f, trunk.B * 0.85f);
				img.SetPixel(11, y, tc);
				img.SetPixel(12, y, tc);
			}
			else
			{
				img.SetPixel(11, y, trunk);
			}
		}

		return ImageTexture.CreateFromImage(img);
	}

	public static ImageTexture MakePixelRockTexture(Color baseColor, int variant)
	{
		int w = 16, h = 16;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color highlight = new Color(
			Mathf.Min(baseColor.R * 1.4f, 1f),
			Mathf.Min(baseColor.G * 1.4f, 1f),
			Mathf.Min(baseColor.B * 1.4f, 1f));
		Color shadow = new Color(baseColor.R * 0.55f, baseColor.G * 0.55f, baseColor.B * 0.55f);

		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(variant * 313 + baseColor.GetHashCode() & 0x7FFFFFFF);

		int cx = w / 2;
		int cy = h / 2;
		for (int y = 3; y <= 13; y++)
		{
			int maxHalf = 5 - Mathf.Abs(y - cy) / 2;
			if (variant == 1) maxHalf += (y % 3 == 0 ? 1 : 0);
			if (variant == 2) maxHalf += (y > cy ? 0 : 1);

			int left = cx - maxHalf;
			int right = cx + maxHalf;
			if (variant == 0) { left += (y - 3) / 4; right -= (y - 3) / 5; }

			for (int x = left; x <= right; x++)
			{
				if (x < 0 || x >= w || y < 0 || y >= h) continue;
				if (x == left && y < 11)
					img.SetPixel(x, y, highlight);
				else if (x >= right - 1 && y > 4)
					img.SetPixel(x, y, shadow);
				else
				{
					float jit = (rng.Randf() - 0.5f) * 0.15f;
					Color c = new Color(
						baseColor.R + jit,
						baseColor.G + jit,
						baseColor.B + jit);
					img.SetPixel(x, y, c);
				}
			}
		}

		return ImageTexture.CreateFromImage(img);
	}

	public static ImageTexture MakePixelRuinTexture(Color baseColor, int variant)
	{
		int w = 16, h = 24;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color brick = new Color(baseColor.R * 1.15f, baseColor.G * 1.15f, baseColor.B * 1.15f);
		Color dark = new Color(baseColor.R * 0.55f, baseColor.G * 0.55f, baseColor.B * 0.55f);
		Color moss = new Color(0.10f, 0.28f, 0.08f);

		if (variant == 0)
		{
			for (int y = 0; y < h; y++)
			{
				int left = 4 + (y / 4);
				int right = 11 - (y / 5);
				if (y > 18) { left = 17; right = 16; }

				for (int x = left; x <= right && x < w; x++)
				{
					Color c = baseColor;
					if ((x + y) % 3 == 0) c = brick;
					if ((x == left || x == right) && (y % 6 > 3)) c = dark;
					if (y > 16 && (x + y) % 4 == 0) c = moss;
					img.SetPixel(x, y, c);
				}
			}
		}
		else
		{
			for (int y = 0; y < h; y++)
			{
				int pillarLeft = 3 + (y / 6);
				int pillarRight = 5;
				int archLeft = 5;
				int archRight = 11 - (y / 5);

				for (int x = pillarLeft; x <= pillarRight && x < w; x++)
				{
					if (y > 17) continue;
					Color c = baseColor;
					if ((x + y) % 3 == 0) c = brick;
					img.SetPixel(x, y, c);
				}
				for (int x = archLeft; x <= archRight && x < w; x++)
				{
					if (y > 17) continue;
					Color c = baseColor;
					if ((x + y) % 4 == 0) c = brick;
					if (x == archRight && (y % 5 > 2)) c = dark;
					if (y > 14 && (x + y) % 5 == 0) c = moss;
					img.SetPixel(x, y, c);
				}
			}
		}

		return ImageTexture.CreateFromImage(img);
	}

	public static ImageTexture MakePixelBushTexture(Color baseColor)
	{
		int w = 8, h = 8;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color darkGreen = new Color(baseColor.R * 0.55f, baseColor.G * 0.55f, baseColor.B * 0.55f);
		Color highlight = new Color(
			Mathf.Min(baseColor.R * 1.3f, 1f),
			Mathf.Min(baseColor.G * 1.3f, 1f),
			Mathf.Min(baseColor.B * 1.3f, 1f));

		for (int y = 1; y <= 6; y++)
		{
			int halfW;
			if (y == 1) halfW = 1;
			else if (y == 2) halfW = 2;
			else if (y <= 4) halfW = 3;
			else if (y == 5) halfW = 2;
			else halfW = 1;

			for (int x = w / 2 - halfW; x <= w / 2 + halfW; x++)
			{
				if (x < 0 || x >= w) continue;
				if ((x + y) % 3 == 0)
					img.SetPixel(x, y, darkGreen);
				else if (y == 3 && x == w / 2 + halfW - 1)
					img.SetPixel(x, y, highlight);
				else
					img.SetPixel(x, y, baseColor);
			}
		}

		return ImageTexture.CreateFromImage(img);
	}

	// ═══════════════════════════════════════════════════════════════
	//  Parallax background textures
	// ═══════════════════════════════════════════════════════════════

	public static ImageTexture MakeSkyGradientTexture(int w, int h)
	{
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);

		Color topColor = new Color(0.08f, 0.12f, 0.30f);
		Color midColor = new Color(0.18f, 0.30f, 0.55f);
		Color lowColor = new Color(0.45f, 0.62f, 0.85f);
		Color horizonColor = new Color(0.78f, 0.88f, 0.98f);

		for (int y = 0; y < h; y++)
		{
			float t = (float)y / h;
			Color c;
			if (t < 0.25f)
				c = topColor.Lerp(midColor, t / 0.25f);
			else if (t < 0.60f)
				c = midColor.Lerp(lowColor, (t - 0.25f) / 0.35f);
			else
				c = lowColor.Lerp(horizonColor, (t - 0.60f) / 0.40f);

			for (int x = 0; x < w; x++)
			{
				float dither = ((x + y) % 8 < 4) ? 0f : 0.02f;
				Color px = new Color(
					Mathf.Min(c.R + dither, 1f),
					Mathf.Min(c.G + dither, 1f),
					Mathf.Min(c.B + dither, 1f));
				img.SetPixel(x, y, px);
			}
		}

		return ImageTexture.CreateFromImage(img);
	}

	public static ImageTexture MakePixelCloudTexture()
	{
		int w = 48, h = 24;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color white = new Color(1, 1, 1, 0.95f);
		Color offWhite = new Color(0.88f, 0.90f, 0.94f, 0.78f);
		Color edgeWhite = new Color(0.78f, 0.80f, 0.88f, 0.45f);

		(int x, int y, int r)[] blobs = new (int, int, int)[]
		{
			(18, 12, 8), (28, 10, 9), (22, 14, 7), (32, 13, 5)
		};

		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				float maxOverlap = 0;
				foreach (var (bx, by, br) in blobs)
				{
					float dx = x - bx;
					float dy = y - by;
					float dist = Mathf.Sqrt(dx * dx + dy * dy) / br;
					float overlap = 1f - dist;
					if (overlap > maxOverlap) maxOverlap = overlap;
				}

				if (maxOverlap > 0.72f)
					img.SetPixel(x, y, white);
				else if (maxOverlap > 0.40f)
					img.SetPixel(x, y, offWhite);
				else if (maxOverlap > 0.12f)
					img.SetPixel(x, y, edgeWhite);
			}
		}

		return ImageTexture.CreateFromImage(img);
	}

	public static ImageTexture MakeMountainRangeTexture(int w, int h, Color bodyColor, Color snowColor, int seed)
	{
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)seed;

		int stepSize = w / 16;
		int[] heights = new int[w];
		int prevH = h / 3;

		for (int x = 0; x < w; x++)
		{
			if (x % stepSize == 0)
				prevH = rng.RandiRange(h / 4, h);
			heights[x] = prevH;
		}

		for (int x = 0; x < w; x++)
		{
			int mh = heights[x];
			mh += (x * seed + x * x * 3) % 5 - 2;
			mh = Mathf.Clamp(mh, 0, h);

			int snowStart = mh - h / 8;
			if (snowStart < 0) snowStart = 0;

			for (int y = 0; y < mh; y++)
			{
				int ry = h - 1 - y;
				if (y >= snowStart && mh > h / 2)
					img.SetPixel(x, ry, snowColor);
				else
					img.SetPixel(x, ry, bodyColor);
			}
		}

		return ImageTexture.CreateFromImage(img);
	}

	public static ImageTexture MakePixelSunTexture(int size)
	{
		var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color sunYellow = new Color(1, 0.88f, 0.35f);
		Color ditherYellow = new Color(1, 0.82f, 0.30f, 0.6f);
		Color outerYellow = new Color(1, 0.75f, 0.25f, 0.30f);

		float half = size * 0.5f;
		int sunRadius = size / 2 - 2;

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float dx = x - half + 0.5f;
				float dy = y - half + 0.5f;
				float dist = Mathf.Sqrt(dx * dx + dy * dy);

				if (dist < sunRadius - 1)
				{
					img.SetPixel(x, y, sunYellow);
				}
				else if (dist < sunRadius + 1)
				{
					if ((x + y) % 2 == 0)
						img.SetPixel(x, y, ditherYellow);
				}
				else if (dist < sunRadius + 3)
				{
					if ((x + y) % 3 == 0)
						img.SetPixel(x, y, outerYellow);
				}
			}
		}

		return ImageTexture.CreateFromImage(img);
	}

	public static ImageTexture MakeDragonSilhouetteTexture(int w, int h)
	{
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color body = new Color(0, 0, 0, 0.5f);
		Color wing = new Color(0, 0, 0, 0.2f);

		int center = h / 2;
		int bodyThickness = 2;

		int[] wingPositions = { 8, 22, 36, 50 };
		int[] wingSizes = { 7, 8, 7, 5 };

		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				bool pixel = false;

				if (y >= center - bodyThickness && y <= center + bodyThickness)
					pixel = true;

				for (int i = 0; i < wingPositions.Length; i++)
				{
					int wx = wingPositions[i];
					int ws = wingSizes[i];
					int dist = Mathf.Abs(x - wx);
					if (dist < ws)
					{
						int wingSpan = ws - dist;
						if (y <= center && y >= center - wingSpan * 2)
							pixel = true;
						if (y >= center && y <= center + wingSpan)
							pixel = true;
					}
				}

				if (pixel)
				{
					bool isBody = y >= center - bodyThickness && y <= center + bodyThickness;
					img.SetPixel(x, y, isBody ? body : wing);
				}
			}
		}

		return ImageTexture.CreateFromImage(img);
	}

	// ═══════════════════════════════════════════════════════════════
	//  Mesh generation
	// ═══════════════════════════════════════════════════════════════

	public static Mesh MakeParticleQuadMesh(Color color, float size)
	{
		var quad = new QuadMesh { Size = new Vector2(size, size) };
		var mat = new StandardMaterial3D
		{
			AlbedoColor = color,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};
		quad.Material = mat;
		return quad;
	}

	// ═══════════════════════════════════════════════════════════════
	//  Simple textures — decoration scattering (ChunkManager)
	// ═══════════════════════════════════════════════════════════════

	public static ImageTexture MakeSimpleTreeTexture(Color canopyColor)
	{
		int w = 16, h = 20;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color trunk = new Color(0.30f, 0.20f, 0.10f);
		Color darkCanopy = new Color(canopyColor.R * 0.7f, canopyColor.G * 0.7f, canopyColor.B * 0.7f);

		for (int y = 0; y <= 12; y++)
		{
			int halfW = 1 + (12 - y) / 2;
			for (int x = w / 2 - halfW; x <= w / 2 + halfW; x++)
			{
				if (x < 0 || x >= w) continue;
				Color c = ((x + y) % 3 == 0) ? darkCanopy : canopyColor;
				img.SetPixel(x, y, c);
			}
		}

		for (int y = 13; y < h; y++)
		{
			img.SetPixel(w / 2 - 1, y, trunk);
			img.SetPixel(w / 2, y, trunk);
		}

		return ImageTexture.CreateFromImage(img);
	}

	public static ImageTexture MakeSimpleRockTexture(Color baseColor)
	{
		int w = 12, h = 10;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color dark = new Color(baseColor.R * 0.7f, baseColor.G * 0.7f, baseColor.B * 0.7f);
		Color light = new Color(
			Mathf.Min(baseColor.R * 1.2f, 1f),
			Mathf.Min(baseColor.G * 1.2f, 1f),
			Mathf.Min(baseColor.B * 1.2f, 1f));

		int cy = h / 2;
		for (int y = 1; y < h - 1; y++)
		{
			int halfW = 3 + Mathf.Abs(y - cy) / 2;
			for (int x = w / 2 - halfW; x <= w / 2 + halfW; x++)
			{
				if (x < 0 || x >= w) continue;
				if ((x + y) % 3 == 0)
					img.SetPixel(x, y, light);
				else if ((x + y) % 4 == 0)
					img.SetPixel(x, y, dark);
				else
					img.SetPixel(x, y, baseColor);
			}
		}

		return ImageTexture.CreateFromImage(img);
	}

	public static ImageTexture MakeSimpleBushTexture(Color baseColor)
	{
		int w = 8, h = 6;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color dark = new Color(baseColor.R * 0.7f, baseColor.G * 0.7f, baseColor.B * 0.7f);
		int cy = h / 2;

		for (int y = 0; y < h; y++)
		{
			int dy = Mathf.Abs(y - cy);
			int halfW = 2 - dy / 2;
			if (y == 0 || y == h - 1) halfW = 1;
			for (int x = w / 2 - halfW; x <= w / 2 + halfW; x++)
			{
				if (x < 0 || x >= w) continue;
				Color c = ((x + y) % 3 == 0) ? dark : baseColor;
				img.SetPixel(x, y, c);
			}
		}

		return ImageTexture.CreateFromImage(img);
	}

	public static ImageTexture MakeSimpleGrassTuftTexture()
	{
		int w = 4, h = 6;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color grass = new Color(0.16f, 0.40f, 0.12f);
		Color light = new Color(0.22f, 0.48f, 0.18f);

		img.SetPixel(0, 0, light);
		img.SetPixel(0, 1, grass);

		img.SetPixel(2, 0, light);
		img.SetPixel(2, 1, grass);
		img.SetPixel(2, 2, grass);

		img.SetPixel(1, 0, grass);
		img.SetPixel(1, 1, light);

		return ImageTexture.CreateFromImage(img);
	}

	public static ImageTexture MakeSimpleRuinTexture(Color baseColor)
	{
		int w = 8, h = 16;
		var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0, 0, 0, 0));

		Color dark = new Color(baseColor.R * 0.6f, baseColor.G * 0.6f, baseColor.B * 0.6f);
		Color light = new Color(
			Mathf.Min(baseColor.R * 1.15f, 1f),
			Mathf.Min(baseColor.G * 1.15f, 1f),
			Mathf.Min(baseColor.B * 1.15f, 1f));

		for (int y = 2; y < h; y++)
		{
			int halfW = (y < 6) ? 2 : 3;
			for (int x = w / 2 - halfW; x <= w / 2 + halfW; x++)
			{
				if (x < 0 || x >= w) continue;
				if ((x + y) % 3 == 0)
					img.SetPixel(x, y, light);
				else if ((x == w / 2 - halfW || x == w / 2 + halfW) && y > 8)
					img.SetPixel(x, y, dark);
				else
					img.SetPixel(x, y, baseColor);
			}
		}

		return ImageTexture.CreateFromImage(img);
	}

	// ═══════════════════════════════════════════════════════════════
	//  Material and mesh helpers for decorations
	// ═══════════════════════════════════════════════════════════════

	public static StandardMaterial3D MakeAlphaMaterial(Texture2D tex)
	{
		return new StandardMaterial3D
		{
			AlbedoTexture = tex,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			NoDepthTest = true,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
		};
	}

	public static StandardMaterial3D MakeSolidDecoMaterial(Texture2D tex)
	{
		return new StandardMaterial3D
		{
			AlbedoTexture = tex,
			Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor,
			AlphaScissorThreshold = 0.1f,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
		};
	}

	/// <summary>
	/// Build a single ArrayMesh containing N crossed quads equally spaced around Y axis.
	/// Shares a single texture — used for trees to avoid flat-paper look.
	/// </summary>
	public static ArrayMesh BuildCrossMesh(float w, float h, int panels)
	{
		float hw = w * 0.5f;
		float hh = h * 0.5f;

		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		for (int i = 0; i < panels; i++)
		{
			float angle = Mathf.Pi * 2f * i / panels;
			float cos = Mathf.Cos(angle);
			float sin = Mathf.Sin(angle);

			Vector3 RotY(float lx, float ly) => new Vector3(lx * cos, ly, lx * sin);

			var bl = RotY(-hw, -hh); var br = RotY(hw, -hh);
			var tl = RotY(-hw,  hh); var tr = RotY(hw,  hh);

			st.SetUV(new Vector2(0, 1)); st.AddVertex(bl);
			st.SetUV(new Vector2(1, 1)); st.AddVertex(br);
			st.SetUV(new Vector2(0, 0)); st.AddVertex(tl);
			st.SetUV(new Vector2(1, 0)); st.AddVertex(tr);

			int b = i * 4;
			st.AddIndex(b); st.AddIndex(b + 1); st.AddIndex(b + 2);
			st.AddIndex(b + 1); st.AddIndex(b + 3); st.AddIndex(b + 2);
		}

		st.GenerateNormals();
		return st.Commit();
	}
}
