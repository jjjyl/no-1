namespace No1.World;
using Godot;

/// <summary>
/// Height queries for terrain vertices and decorations.
/// All methods are static – callers pass the data they need.
/// </summary>
public static class TerrainHeight
{
	/// <summary>
	/// Bilinear-smoothed vertex height at internal grid point (gx, gz).
	/// Averages up to 4 adjacent tile heights.
	/// </summary>
	public static float GetVertexHeight(ChunkData chunk, int gx, int gz, int dim, float heightScale)
	{
		bool allWater = true;
		bool anyTile = false;
		float heightSum = 0f;
		int heightCount = 0;

		for (int dz = -1; dz <= 0; dz++)
		{
			for (int dx = -1; dx <= 0; dx++)
			{
				int tx = gx + dx;
				int tz = gz + dz;
				if (tx >= 0 && tx < dim && tz >= 0 && tz < dim)
				{
					anyTile = true;
					var tile = chunk.Tiles[tz * dim + tx];
					if (tile.Type != TileType.Water)
						allWater = false;
					heightSum += tile.Height / 255f;
					heightCount++;
				}
			}
		}

		if (!anyTile)
			return 0f;

		if (allWater)
			return -0.5f;

		return (heightSum / heightCount) * heightScale;
	}

	/// <summary>
	/// Vertex height at a world-grid coordinate (cross-chunk safe).
	/// Falls back to neighboring chunks by translating wgx,wgz → chunk + local index.
	/// </summary>
	public static float WorldVertexHeight(ChunkData[] chunks, int wgx, int wgz, int dim, float scale)
	{
		float sum = 0;
		int count = 0;
		bool allWater = true;

		for (int dz = -1; dz <= 0; dz++)
		for (int dx = -1; dx <= 0; dx++)
		{
			int wx = wgx + dx;
			int wz = wgz + dz;
			if (wx < 0 || wz < 0 || wx >= WorldConstants.WorldWidth || wz >= WorldConstants.WorldHeight)
				continue;

			int ncx = wx / dim;
			int ncy = wz / dim;
			int ltx = wx % dim;
			int ltz = wz % dim;

			var nc = chunks[ncy * WorldConstants.ChunksX + ncx];
			if (nc.Tiles == null) continue;

			var tile = nc.Tiles[ltz * dim + ltx];
			count++;
			if (tile.Type != TileType.Water) allWater = false;
			sum += tile.Height / 255f;
		}

		if (count == 0) return 0;
		if (allWater) return -0.5f;
		return (sum / count) * scale;
	}

	/// <summary>
	/// Height at a grid corner, using chunk-local query for interior points
	/// and cross-chunk WorldVertexHeight for edge points.
	/// </summary>
	public static float CornerHeight(ChunkData chunk,
		int gx, int gz, int dim, float scale,
		int cx, int cy, ChunkData[] allChunks)
	{
		if (gx > 0 && gx < dim && gz > 0 && gz < dim)
			return GetVertexHeight(chunk, gx, gz, dim, scale);

		int wgx = cx * dim + gx;
		int wgz = cy * dim + gz;
		return WorldVertexHeight(allChunks, wgx, wgz, dim, scale);
	}

	/// <summary>
	/// Bilinear-smoothed world-space height query for player/enemy foot placement.
	/// </summary>
	public static float GetHeightAt(ChunkData[] chunks, float worldX, float worldZ)
	{
		int tileX = Mathf.Clamp((int)(worldX / WorldConstants.TileSizeMeters), 0, WorldConstants.WorldWidth - 1);
		int tileZ = Mathf.Clamp((int)(worldZ / WorldConstants.TileSizeMeters), 0, WorldConstants.WorldHeight - 1);

		int cx = tileX / WorldConstants.ChunkDim;
		int cy = tileZ / WorldConstants.ChunkDim;

		if (cx < 0 || cx >= WorldConstants.ChunksX || cy < 0 || cy >= WorldConstants.ChunksY)
			return 0f;

		var chunk = chunks[cy * WorldConstants.ChunksX + cx];
		if (chunk?.Tiles == null)
			return 0f;

		int lx = tileX - cx * WorldConstants.ChunkDim;
		int lz = tileZ - cy * WorldConstants.ChunkDim;

		if (lx < 0 || lx >= WorldConstants.ChunkDim || lz < 0 || lz >= WorldConstants.ChunkDim)
			return 0f;

		float heightSum = 0f;
		int count = 0;
		for (int dz = 0; dz <= 1 && lz + dz < WorldConstants.ChunkDim; dz++)
		{
			for (int dx = 0; dx <= 1 && lx + dx < WorldConstants.ChunkDim; dx++)
			{
				var tile = chunk.Tiles[(lz + dz) * WorldConstants.ChunkDim + (lx + dx)];
				heightSum += tile.Height / 255f;
				count++;
			}
		}

		return count > 0 ? heightSum / count * 5.0f : 0f;
	}

	/// <summary>
	/// Height variance (max - min) within a tile-radius square around (tx, ty).
	/// Uses CornerHeight so it matches the visible terrain surface.
	/// </summary>
	public static float SlopeVariance(ChunkData chunk,
		int tx, int ty, int radius, int dim,
		int cx, int cy, ChunkData[] allChunks, float heightScale)
	{
		float minH = float.MaxValue;
		float maxH = float.MinValue;

		for (int dz = -radius; dz <= radius; dz++)
		for (int dx = -radius; dx <= radius; dx++)
		{
			int gx = tx + dx;
			int gz = ty + dz;
			if (gx < 0 || gx > dim || gz < 0 || gz > dim)
				continue;

			float h = CornerHeight(chunk, gx, gz, dim, heightScale, cx, cy, allChunks);
			if (h < minH) minH = h;
			if (h > maxH) maxH = h;
		}

		return maxH - minH;
	}
}
