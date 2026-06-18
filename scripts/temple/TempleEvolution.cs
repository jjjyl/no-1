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

	/// <summary>石板刻痕是否可见</summary>
	public bool SlabGlyphsVisible => Completeness > 0.2f;

	/// <summary>后墙是否出现门轮廓（通往静室）</summary>
	public bool BackWallDoorHint => Completeness > 0.4f;

	/// <summary>引导者是否有人形轮廓（而非纯粒子）</summary>
	public bool GuideHasForm => Completeness > 0.2f;

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
		// 50 碎片 = 完全由碎片驱动
		// 10 轮回 = 完全由轮回驱动
		float fragmentFactor = Mathf.Clamp(cm.FragmentCount / 50f, 0f, 1f);
		float cycleFactor = Mathf.Clamp(cm.CurrentCycle / 10f, 0f, 1f);

		Completeness = fragmentFactor * 0.7f + cycleFactor * 0.3f;

		GD.Print(
			$"[TempleEvolution] fragment={cm.FragmentCount} cycle={cm.CurrentCycle} " +
			$"fFactor={fragmentFactor:F2} cFactor={cycleFactor:F2} completeness={Completeness:F3}");
	}
}
