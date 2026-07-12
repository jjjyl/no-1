#nullable enable
namespace No1.World;
using Godot;
using System.Collections.Generic;

/// <summary>
/// Preset decoration definitions with spawn conditions.
/// </summary>
public static class DecorationPresets
{
	private static List<DecorationDef>? _defs;

	public static List<DecorationDef> Defs => _defs ??= BuildDefs();

	static List<DecorationDef> BuildDefs()
	{
		var list = new List<DecorationDef>();

		void Add(string name, Texture2D? tex, float yFrac, float pxPerM,
			float sMin, float sMax, BaseMaterial3D.BillboardModeEnum bb,
			int panels = 0, bool solid = false, float tiltDeg = 0f,
			bool collision = true, string colShape = "Box",
			float cwFrac = 0.5f, float chFrac = 0.5f, float cdFrac = 0.5f,
			float maxSlope = 0, int slopeRadius = 0,
			float? minHeight = null, float? maxHeight = null)
		{
			if (tex == null) return;
			list.Add(new DecorationDef
			{
				Name = name,
				Texture = tex,
				BaseYFrac = yFrac,
				PixelScaleBase = pxPerM,
				ScaleRange = new Vector2(sMin, sMax),
				Billboard = bb,
				PanelCount = panels,
				HardAlpha = solid,
				BaseTiltDeg = tiltDeg,
				Collision = collision,
				CollisionShape = colShape,
				CollisionWFrac = cwFrac,
				CollisionHFrac = chFrac,
				CollisionDFrac = cdFrac,
				MaxSlope = maxSlope,
				SlopeRadius = slopeRadius,
				MinHeight = minHeight,
				MaxHeight = maxHeight,
			});
		}

		var treeTex = WorldTextures.TryLoadTexture("res://assets/texture/world/deco_tree.png")
			?? WorldTextures.MakeSimpleTreeTexture(new Color(0.15f, 0.40f, 0.10f));

		// Trees: only on flat ground
		Add("Tree", treeTex,
			0.88f, 0.007f, 0.6f, 1.0f,
			BaseMaterial3D.BillboardModeEnum.Enabled,
			collision: true, colShape: "Cylinder", cwFrac: 0.35f, chFrac: 0.6f, cdFrac: 0.35f,
			maxSlope: 0.25f, slopeRadius: 2);

		// Rocks: anywhere
		Add("Rock",
			WorldTextures.TryLoadTexture("res://assets/texture/world/stone1_1.png")
				?? WorldTextures.MakeSimpleRockTexture(new Color(0.35f, 0.33f, 0.30f)),
			0.90f, 0.02f, 0.7f, 1.05f,
			BaseMaterial3D.BillboardModeEnum.Enabled,
			tiltDeg: -60f,
			collision: true, colShape: "Box", cwFrac: 0.6f, chFrac: 0.4f, cdFrac: 0.6f);

		// Bushes: mildly flat, hard alpha
		Add("Bush",
			WorldTextures.MakeSimpleBushTexture(new Color(0.15f, 0.40f, 0.10f)),
			0.85f, 0.08f, 0.6f, 0.9f,
			BaseMaterial3D.BillboardModeEnum.Enabled,
			solid: true,
			collision: true, colShape: "Capsule", cwFrac: 0.45f, chFrac: 0.5f, cdFrac: 0.45f,
			maxSlope: 0.4f, slopeRadius: 1);

		// Grass tufts: anywhere, hard alpha
		Add("Tuft",
			WorldTextures.TryLoadTexture("res://assets/texture/world/grass1_1.png")
				?? WorldTextures.MakeSimpleGrassTuftTexture(),
			0.80f, 0.08f, 0.5f, 0.8f,
			BaseMaterial3D.BillboardModeEnum.Enabled,
			solid: true,
			collision: true, colShape: "Capsule", cwFrac: 0.35f, chFrac: 0.3f, cdFrac: 0.35f);

		// Ruins: very flat, moderate elevation
		Add("Ruin",
			WorldTextures.MakeSimpleRuinTexture(new Color(0.28f, 0.24f, 0.20f)),
			0.95f, 0.10f, 0.8f, 1.0f,
			BaseMaterial3D.BillboardModeEnum.Enabled,
			collision: true, colShape: "Box", cwFrac: 0.7f, chFrac: 0.8f, cdFrac: 0.3f,
			maxSlope: 0.05f, slopeRadius: 1,
			minHeight: 1.5f, maxHeight: 4.0f);

		// Snow rocks: high altitude only
		Add("RockSnow",
			WorldTextures.MakeSimpleRockTexture(new Color(0.55f, 0.55f, 0.58f)),
			0.90f, 0.08f, 0.7f, 1.05f,
			BaseMaterial3D.BillboardModeEnum.Enabled,
			collision: true, colShape: "Box", cwFrac: 0.6f, chFrac: 0.4f, cdFrac: 0.6f,
			minHeight: 2.5f);

		return list;
	}

	public static DecorationDef? Find(string name)
	{
		foreach (var d in Defs)
			if (d.Name == name)
				return d;
		return null;
	}
}
