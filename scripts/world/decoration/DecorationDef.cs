namespace No1.World;
using Godot;

/// <summary>
/// Defines a type of decoration: texture, scale, billboard, and spawn conditions.
/// 0 / null conditions are skipped (no check).
/// </summary>
public struct DecorationDef
{
	public string Name;
	public Texture2D Texture;
	public float BaseYFrac;
	public float PixelScaleBase;   // target meters per pixel, multiplied by scale factor
	public Vector2 ScaleRange;
	public BaseMaterial3D.BillboardModeEnum Billboard;
	public int PanelCount;         // 0=sprite, N=crossed quads around Y
	public bool HardAlpha;          // true=AlphaCut Discard (binary), false=Alpha blend
	public float BaseTiltDeg;       // fixed X tilt override (default 0 → use -45)

	// ── Collision ──
	public bool Collision;           // create StaticBody3D with CollisionShape3D
	public string CollisionShape;    // "Box" | "Cylinder" | "Capsule"
	public float CollisionWFrac;     // width fraction of worldW
	public float CollisionHFrac;     // height fraction of worldH
	public float CollisionDFrac;     // depth fraction of worldW

	// ── Spawn conditions (0 / null = no check) ──
	public float MaxSlope;         // max height variance (meters) within SlopeRadius. 0=no check
	public int SlopeRadius;        // tile radius for flatness check. 0=no check
	public float? MinHeight;       // world-height lower bound (meters). null=no check
	public float? MaxHeight;       // world-height upper bound (meters). null=no check
}
