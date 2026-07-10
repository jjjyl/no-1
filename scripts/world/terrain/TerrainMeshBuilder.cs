namespace No1.World;
using Godot;
using System.Collections.Generic;

/// <summary>
/// Builds terrain and dual-grid meshes for a single chunk.
/// Pure functions – input data, output meshes. No scene-tree side effects.
/// </summary>
public static class TerrainMeshBuilder
{
	public static float HeightScale = 5.0f;
	public static float EdgeOverlap = 0.05f;
	public static float YOffset = 0.03f;

	// ── Terrain mesh ──────────────────────────────────────────────────

	/// <summary>
	/// Build the base terrain ArrayMesh (quads + edge skirts).
	/// </summary>
	public static ArrayMesh BuildChunkMesh(ChunkData chunk, int dim, float tileSize)
	{
		float halfExtent = dim * tileSize * 0.5f + EdgeOverlap;
		int vertsPerRow = dim + 1;

		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		for (int z = 0; z <= dim; z++)
		{
			for (int x = 0; x <= dim; x++)
			{
				float height = TerrainHeight.GetVertexHeight(chunk, x, z, dim, HeightScale);
				float worldX = (float)x / dim * 2.0f * halfExtent - halfExtent;
				float worldZ = (float)z / dim * 2.0f * halfExtent - halfExtent;
				st.SetUV(new Vector2((float)x / dim, (float)z / dim));
				st.AddVertex(new Vector3(worldX, height, worldZ));
			}
		}

		for (int z = 0; z < dim; z++)
		{
			for (int x = 0; x < dim; x++)
			{
				int v00 = z * vertsPerRow + x;
				int v01 = z * vertsPerRow + (x + 1);
				int v10 = (z + 1) * vertsPerRow + x;
				int v11 = (z + 1) * vertsPerRow + (x + 1);

				st.AddIndex(v00);
				st.AddIndex(v10);
				st.AddIndex(v01);

				st.AddIndex(v10);
				st.AddIndex(v11);
				st.AddIndex(v01);
			}
		}

		// ── Skirt: vertical edge walls to hide chunk seams ──
		const float SKIRT_DEPTH = 4.0f;
		int baseIdx = vertsPerRow * vertsPerRow;

		void AddSkirtQuad(int ax, int az, int bx, int bz)
		{
			float ha = TerrainHeight.GetVertexHeight(chunk, ax, az, dim, HeightScale);
			float hb = TerrainHeight.GetVertexHeight(chunk, bx, bz, dim, HeightScale);
			float wax = (float)ax / dim * 2.0f * halfExtent - halfExtent;
			float waz = (float)az / dim * 2.0f * halfExtent - halfExtent;
			float wbx = (float)bx / dim * 2.0f * halfExtent - halfExtent;
			float wbz = (float)bz / dim * 2.0f * halfExtent - halfExtent;

			st.SetUV(Vector2.Zero);
			st.AddVertex(new Vector3(wax, ha, waz));
			st.SetUV(Vector2.Zero);
			st.AddVertex(new Vector3(wax, ha - SKIRT_DEPTH, waz));
			st.SetUV(Vector2.Zero);
			st.AddVertex(new Vector3(wbx, hb, wbz));
			st.SetUV(Vector2.Zero);
			st.AddVertex(new Vector3(wbx, hb - SKIRT_DEPTH, wbz));

			st.AddIndex(baseIdx); st.AddIndex(baseIdx + 1); st.AddIndex(baseIdx + 2);
			st.AddIndex(baseIdx + 1); st.AddIndex(baseIdx + 3); st.AddIndex(baseIdx + 2);
			baseIdx += 4;
		}

		for (int z = 0; z < dim; z++) AddSkirtQuad(0, z, 0, z + 1);
		for (int z = 0; z < dim; z++) AddSkirtQuad(dim, z, dim, z + 1);
		for (int x = 0; x < dim; x++) AddSkirtQuad(x, 0, x + 1, 0);
		for (int x = 0; x < dim; x++) AddSkirtQuad(x, dim, x + 1, dim);

		st.GenerateNormals();
		return st.Commit();
	}

	// ── Dual grid meshes ──────────────────────────────────────────────

	/// <summary>
	/// Build per-material quad meshes for the dual-grid tile overlay.
	/// Each source tile → one quad.  Quads are grouped by material key.
	/// </summary>
	public static Dictionary<string, ArrayMesh> BuildDualGridMeshes(
		ChunkData chunk, int cx, int cy, int dim, ChunkData[] allChunks)
	{
		float halfExtent = dim * WorldConstants.TileSizeMeters * 0.5f + EdgeOverlap;
		float cellWidth = 2.0f * halfExtent / dim;

		var sts = new Dictionary<string, SurfaceTool>();
		var vi = new Dictionary<string, int>();

		for (int tz = 0; tz < dim; tz++)
		{
			for (int tx = 0; tx < dim; tx++)
			{
				int idx = tz * dim + tx;

				var tl = chunk.Tiles[idx];
				var tr = chunk.Tiles[tz * dim + Math.Min(tx + 1, dim - 1)];
				var bl = chunk.Tiles[Math.Min(tz + 1, dim - 1) * dim + tx];
				var br = chunk.Tiles[Math.Min(tz + 1, dim - 1) * dim + Math.Min(tx + 1, dim - 1)];

				string key = DualGridEvaluator.GetMaterialKey(tl.Type, tr.Type, bl.Type, br.Type);

				if (!sts.TryGetValue(key, out var st))
				{
					st = new SurfaceTool();
					st.Begin(Mesh.PrimitiveType.Triangles);
					sts[key] = st;
					vi[key] = 0;
				}

				float h00 = TerrainHeight.CornerHeight(chunk, tx,     tz,     dim, HeightScale, cx, cy, allChunks) + YOffset;
				float h10 = TerrainHeight.CornerHeight(chunk, tx + 1, tz,     dim, HeightScale, cx, cy, allChunks) + YOffset;
				float h01 = TerrainHeight.CornerHeight(chunk, tx,     tz + 1, dim, HeightScale, cx, cy, allChunks) + YOffset;
				float h11 = TerrainHeight.CornerHeight(chunk, tx + 1, tz + 1, dim, HeightScale, cx, cy, allChunks) + YOffset;

				float x0 = tx * cellWidth - halfExtent;
				float x1 = x0 + cellWidth;
				float z0 = tz * cellWidth - halfExtent;
				float z1 = z0 + cellWidth;

				st.SetUV(new Vector2(0, 0));
				st.AddVertex(new Vector3(x0, h00, z0));
				st.SetUV(new Vector2(1, 0));
				st.AddVertex(new Vector3(x1, h10, z0));
				st.SetUV(new Vector2(0, 1));
				st.AddVertex(new Vector3(x0, h01, z1));
				st.SetUV(new Vector2(1, 1));
				st.AddVertex(new Vector3(x1, h11, z1));

				int v = vi[key];
				st.AddIndex(v);
				st.AddIndex(v + 1);
				st.AddIndex(v + 2);
				st.AddIndex(v + 1);
				st.AddIndex(v + 3);
				st.AddIndex(v + 2);
				vi[key] = v + 4;
			}
		}

		var result = new Dictionary<string, ArrayMesh>();
		foreach (var kvp in sts)
		{
			kvp.Value.GenerateNormals();
			result[kvp.Key] = kvp.Value.Commit();
		}
		return result;
	}
}
