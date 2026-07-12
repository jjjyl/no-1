namespace No1.World;
using Godot;

/// <summary>
/// Scatters decorations across a chunk with per-definition spawn condition checks.
/// Uses CornerHeight for ground placement – matches the visible dual-grid surface.
/// </summary>
public static class DecorationPlacer
{
	private const float HeightScale = 5.0f;
	private const float GroundOffset = 0.02f;

	// ── Public entry ──────────────────────────────────────────────────

	public static void Scatter(ChunkData chunk, int cx, int cy, Node parent, ChunkData[] allChunks, ulong worldSeed)
	{
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(cx * 1000 + cy) + worldSeed % 1000;

		int dim = WorldConstants.ChunkDim;
		const int STEP = 4;
		float tileSize = WorldConstants.TileSizeMeters;
		float half = dim * tileSize * 0.5f;

		for (int ty = 0; ty < dim; ty += STEP)
		for (int tx = 0; tx < dim; tx += STEP)
		{
			int ti = ty * dim + tx;
			TileType tileType = chunk.Tiles[ti].Type;
			if (tileType == TileType.Water || tileType == TileType.Path)
				continue;

			float groundH = TerrainHeight.CornerHeight(chunk, tx, ty, dim, HeightScale, cx, cy, allChunks) + GroundOffset;
			float localX = (tx + 0.5f) * tileSize - half;
			float localZ = (ty + 0.5f) * tileSize - half;
			var pos = new Vector3(localX, groundH, localZ);

			float roll = rng.Randf();
			DecorationDef? pick = null;

			switch (tileType)
			{
				case TileType.Grass:
					if (roll < 0.05f)       pick = DecorationPresets.Find("Tree");
					else if (roll < 0.08f)  pick = DecorationPresets.Find("Rock");
					else if (roll < 0.10f)  pick = DecorationPresets.Find("Bush");
					else if (roll < 0.14f)  pick = DecorationPresets.Find("Tuft");
					break;

				case TileType.Dirt:
					if (roll < 0.04f)       pick = DecorationPresets.Find("Rock");
					else if (roll < 0.05f)  pick = DecorationPresets.Find("Ruin");
					else if (roll < 0.07f)  pick = DecorationPresets.Find("Tuft");
					else if (roll < 0.09f)  pick = DecorationPresets.Find("Tree");
					break;

				case TileType.Rock:
					if (roll < 0.05f)       pick = DecorationPresets.Find("Rock");
					else if (roll < 0.08f)  pick = DecorationPresets.Find("Tree");
					break;

				case TileType.Sand:
				case TileType.Swamp:
					if (roll < 0.03f)       pick = DecorationPresets.Find("Rock");
					else if (roll < 0.04f)  pick = DecorationPresets.Find("Tuft");
					break;

				case TileType.Snow:
					if (roll < 0.03f)       pick = DecorationPresets.Find("RockSnow");
					break;
			}

			if (pick != null && CheckConditions(pick.Value, chunk, tx, ty, dim, cx, cy, allChunks))
		{
			Vector3 normal = ComputeNormal(chunk, tx, ty, dim, cx, cy, allChunks);
			Spawn(pick.Value, parent, pos, normal, rng);
		}
		}
	}

	// ── Terrain normal ────────────────────────────────────────────────

	static Vector3 ComputeNormal(ChunkData chunk, int tx, int ty, int dim, int cx, int cy, ChunkData[] allChunks)
	{
		float ts = WorldConstants.TileSizeMeters;
		float h00 = TerrainHeight.CornerHeight(chunk, tx,     ty,     dim, HeightScale, cx, cy, allChunks);
		float h10 = TerrainHeight.CornerHeight(chunk, tx + 1, ty,     dim, HeightScale, cx, cy, allChunks);
		float h01 = TerrainHeight.CornerHeight(chunk, tx,     ty + 1, dim, HeightScale, cx, cy, allChunks);
		float h11 = TerrainHeight.CornerHeight(chunk, tx + 1, ty + 1, dim, HeightScale, cx, cy, allChunks);
		float dx = ((h10 + h11) - (h00 + h01)) * 0.5f / ts;
		float dz = ((h01 + h11) - (h00 + h10)) * 0.5f / ts;
		return new Vector3(-dx, 1f, -dz).Normalized();
	}

	// ── Spawn condition checks ────────────────────────────────────────

	static bool CheckConditions(DecorationDef def, ChunkData chunk,
		int tx, int ty, int dim, int cx, int cy, ChunkData[] allChunks)
	{
		// Flatness check
		if (def.MaxSlope > 0 && def.SlopeRadius > 0)
		{
			float variance = TerrainHeight.SlopeVariance(
				chunk, tx, ty, def.SlopeRadius, dim, cx, cy, allChunks, HeightScale);
			if (variance > def.MaxSlope)
				return false;
		}

		// Height range check (uses tile center height)
		if (def.MinHeight.HasValue || def.MaxHeight.HasValue)
		{
			float h = TerrainHeight.CornerHeight(chunk, tx, ty, dim, HeightScale, cx, cy, allChunks);
			if (def.MinHeight.HasValue && h < def.MinHeight.Value) return false;
			if (def.MaxHeight.HasValue && h > def.MaxHeight.Value) return false;
		}

		return true;
	}

	// ── Spawn ─────────────────────────────────────────────────────────

	/// <summary>
	/// Instantiates a decoration sprite or cross-mesh at groundPos.
	/// BaseYFrac pivot aligns the bottom contact point with groundPos.Y.
	/// </summary>
	static void Spawn(DecorationDef def, Node parent, Vector3 groundPos, Vector3 terrainNormal, RandomNumberGenerator rng)
	{
		var tex = def.Texture;
		if (tex == null) return;

		float scale = rng.RandfRange(def.ScaleRange.X, def.ScaleRange.Y);
		float texH = tex.GetHeight();
		float texW = tex.GetWidth();
		float worldW = texW * def.PixelScaleBase * scale;
		float worldH = texH * def.PixelScaleBase * scale;

		float yOffset = worldH * (def.BaseYFrac - 0.5f);
		float yRot = rng.RandfRange(-12, 12);

		const float TILT_DEG = 45f;

		if (def.PanelCount > 0)
		{
			var mat = def.HardAlpha
				? WorldTextures.MakeSolidDecoMaterial(tex)
				: WorldTextures.MakeAlphaMaterial(tex);
			var mesh = WorldTextures.BuildCrossMesh(worldW, worldH, def.PanelCount);
			var mi = new MeshInstance3D
			{
				Mesh = mesh,
				MaterialOverride = mat,
				Position = groundPos + new Vector3(0, yOffset, 0),
				RotationDegrees = new Vector3(TILT_DEG, 0, 0),
				SortingUseAabbCenter = false,
			};
			parent.AddChild(mi);
		}
		else
		{
			float offsetY = texH * (def.BaseYFrac - 0.5f);
			var mat = def.HardAlpha
				? WorldTextures.MakeSolidDecoMaterial(tex)
				: WorldTextures.MakeAlphaMaterial(tex);
			mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Disabled;
			var sprite = new Sprite3D
			{
				Texture = tex,
				Billboard = BaseMaterial3D.BillboardModeEnum.FixedY,
				Position = groundPos,
				PixelSize = def.PixelScaleBase * scale,
				Offset = new Vector2(0, offsetY),
				Modulate = Colors.White,
				MaterialOverride = mat,
				SortingUseAabbCenter = false,
				AlphaCut = def.HardAlpha
					? SpriteBase3D.AlphaCutMode.Discard
					: SpriteBase3D.AlphaCutMode.Disabled,
				AlphaScissorThreshold = def.HardAlpha ? 0.1f : 0.5f,
				RotationDegrees = new Vector3(TILT_DEG, 0, 0),
			};
			parent.AddChild(sprite);
		}
	}
}
