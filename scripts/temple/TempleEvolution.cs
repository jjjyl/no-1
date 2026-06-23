namespace No1.Temple;

using Godot;
using No1.Core;

/// <summary>
/// 神殿演化管线。
/// 挂在场景中作为纯数据节点，读 CycleManager 计算"神殿完整度"，
/// 暴露参数供 Temple3D / GuideLight / CentralSlab 等组件读取。
/// </summary>
public partial class TempleEvolution : Node
{
	// ── 输入 ──

	int _lastFragmentCount = -1;
	int _lastCycleCount = -1;

	// ── 输出（其他组件只读）──

	/// <summary>神殿完整度 0.0（初始废墟）~ 1.0（完全恢复）</summary>
	public float Completeness { get; private set; }

	/// <summary>雾浓度 1.0（浓雾）~ 0.0（无雾）</summary>
	public float FogDensity => 1f - Completeness;

	/// <summary>环境光色，从冷暗渐变到暖亮</summary>
	public Color AmbientLight => new Color(0.12f, 0.12f, 0.16f).Lerp(
		new Color(0.30f, 0.26f, 0.20f), Completeness);

	/// <summary>窗外亮度，冷白 → 暖白</summary>
	public float WindowBrightness => 0.35f + Completeness * 0.65f;

	// ── 可导出演化阈值 ──

	/// <summary>石板刻痕出现的完整度阈值</summary>
	[Export] public float GlyphsThreshold = 0.2f;

	/// <summary>后墙门轮廓出现的完整度阈值</summary>
	[Export] public float BackWallThreshold = 0.4f;

	/// <summary>引导者人形轮廓出现的完整度阈值</summary>
	[Export] public float GuideThreshold = 0.2f;

	/// <summary>碎片计数上限（完全由碎片驱动）</summary>
	[Export] public int MaxFragmentCount = 50;

	/// <summary>轮回计数上限（完全由轮回驱动）</summary>
	[Export] public int MaxEffectiveCycle = 10;

	/// <summary>石板刻痕是否可见</summary>
	public bool SlabGlyphsVisible => Completeness > GlyphsThreshold;

	/// <summary>后墙是否出现门轮廓（通往静室）</summary>
	public bool BackWallDoorHint => Completeness > BackWallThreshold;

	/// <summary>引导者是否有人形轮廓（而非纯粒子）</summary>
	public bool GuideHasForm => Completeness > GuideThreshold;

	// ── 计算 ──

	public override void _Ready()
	{
		Recalculate();
	}

	public override void _Process(double delta)
	{
		var cm = CycleManager.Instance;
		if (cm == null) return;

		if (cm.FragmentCount != _lastFragmentCount || cm.CurrentCycle != _lastCycleCount)
			Recalculate();
	}

	void Recalculate()
	{
		var cm = CycleManager.Instance;
		if (cm == null) return;

		_lastFragmentCount = cm.FragmentCount;
		_lastCycleCount = cm.CurrentCycle;

		// 碎片权重 0.7，轮回权重 0.3
		float fragmentFactor = Mathf.Clamp(cm.FragmentCount / (float)MaxFragmentCount, 0f, 1f);
		float cycleFactor = Mathf.Clamp(cm.CurrentCycle / (float)MaxEffectiveCycle, 0f, 1f);

		Completeness = fragmentFactor * 0.7f + cycleFactor * 0.3f;

		GD.Print(
			$"[TempleEvolution] fragment={cm.FragmentCount} cycle={cm.CurrentCycle} " +
			$"fFactor={fragmentFactor:F2} cFactor={cycleFactor:F2} completeness={Completeness:F3}");
	}

	// ── 演化挂钩（由 Temple3D 每帧触发）──

	/// <summary>检查石板刻痕演化。在完整度超过 GlyphsThreshold 时激活。</summary>
	public void UpdateSlabGlyphs(float completeness)
	{
		// TODO: Replace slab glyphs with evolved patterns at completeness threshold
		// GD.Print($"[TempleEvolution] Glyphs check: {completeness:F2} >= {GlyphsThreshold}");
	}

	/// <summary>检查后墙演化。在完整度超过 BackWallThreshold 时淡化后墙。</summary>
	public void UpdateBackWall(float completeness)
	{
		// TODO: Fade back wall to reveal deeper temple areas
	}

	/// <summary>检查引导者形态演化。在完整度超过 GuideThreshold 时推进粒子→灵体→人形。</summary>
	public void UpdateGuideForm(float completeness)
	{
		// TODO: Evolve guide light form (particle → wisp → figure)
	}
}
