namespace No1.UI;

using Godot;

/// <summary>
/// Combat UI 色彩规范 — 战斗页面所有颜色从此派生。
/// 后续换主题只需改这一个文件。
/// </summary>
public static class CombatColors
{
	// ── 背景 ──
	public static Color Background => new(0.10f, 0.05f, 0.05f, 1f);        // 深墨底色

	// ── 面板 ──
	public static Color PanelBg => new(0.08f, 0.08f, 0.12f, 0.85f);        // 半透明炭黑
	public static Color EnemyPanelOutline => new(0.40f, 0.18f, 0.18f, 0.9f); // 敌方暗红边框
	public static Color AllyPanelOutline => new(0.25f, 0.35f, 0.50f, 0.8f);  // 我方蓝灰边框
	public static Color PlayerPanelOutline => new(0.45f, 0.45f, 0.55f, 0.9f); // 玩家面板边框（稍亮）

	// ── 血量 ──
	public static Color BruiseBarFill => new(0.35f, 0.60f, 0.30f, 0.9f);    // 擦伤绿
	public static Color BruiseBarBg => new(0.10f, 0.14f, 0.08f, 0.7f);
	public static Color SevereBarFill => new(0.60f, 0.28f, 0.48f, 0.9f);    // 重伤紫
	public static Color SevereBarBg => new(0.14f, 0.08f, 0.12f, 0.7f);

	// ── 精力 ──
	public static Color EnergyBarFill => new(0.27f, 0.53f, 0.73f, 0.9f);    // 精力蓝
	public static Color EnergyBarBg => new(0.08f, 0.12f, 0.18f, 0.7f);

	// ── 行动槽 ──
	public static Color GaugeBarFill => new(0.90f, 0.75f, 0.25f, 0.9f);     // 行动金
	public static Color GaugeBarBg => new(0.08f, 0.08f, 0.08f, 0.8f);

	// ── 文字 ──
	public static Color TextPrimary => new(0.83f, 0.78f, 0.72f, 1f);        // 暖灰白主色
	public static Color TextSecondary => new(0.55f, 0.52f, 0.48f, 1f);      // 灰副色
	public static Color TextGold => new(1.0f, 0.80f, 0.27f, 1f);            // 金色（奖励/系统）
	public static Color TextDanger => new(0.87f, 0.27f, 0.27f, 1f);         // 红色（伤害）
	public static Color TextHeal => new(0.27f, 0.80f, 0.53f, 1f);           // 绿色（治疗）
	public static Color TextEnemy => new(1.0f, 0.45f, 0.45f, 1f);           // 敌名红
	public static Color TextAlly => new(0.60f, 0.72f, 0.88f, 1f);           // 同伴蓝

	// ── 状态标签 ──
	public static Color BuffPillBg => new(0.27f, 0.53f, 0.80f, 0.85f);      // buff 蓝底
	public static Color DebuffPillBg => new(0.80f, 0.27f, 0.27f, 0.85f);    // debuff 红底
	public static Color NeutralPillBg => new(0.80f, 0.53f, 0.27f, 0.85f);   // 中立橙底

	// ── 反馈 ──
	public static Color DamageFlash => new(1.0f, 0.2f, 0.2f, 0.4f);         // 受击闪红
	public static Color HealFlash => new(0.2f, 1.0f, 0.4f, 0.4f);           // 治疗闪绿
	public static Color TargetHighlightEnemy => new(0.60f, 0.30f, 0.30f, 0.7f);
	public static Color TargetHighlightAlly => new(0.30f, 0.50f, 0.30f, 0.7f);

	// ── 快捷方法 ──

	/// <summary>创建通用面板底色的 StyleBoxFlat</summary>
	public static StyleBoxFlat PanelStyle(Color outlineColor)
	{
		return new StyleBoxFlat
		{
			BgColor = PanelBg,
			BorderColor = outlineColor,
			BorderWidthLeft = 1,
			BorderWidthRight = 1,
			BorderWidthTop = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
		};
	}

	/// <summary>创建血条填充 StyleBoxFlat</summary>
	public static StyleBoxFlat BarFillStyle(Color fillColor)
	{
		return new StyleBoxFlat
		{
			BgColor = fillColor,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomLeft = 2,
			CornerRadiusBottomRight = 2,
		};
	}
}
