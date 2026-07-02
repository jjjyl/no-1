namespace No1.UI;

using Godot;

public static class CombatFeedback
{
	public static void Flash(Control target, Color color, float dur = 0.15f)
	{
		if (target == null) return;
		var t = target.CreateTween();
		t.TweenProperty(target, "modulate", color, dur * 0.3f);
		t.TweenProperty(target, "modulate", Colors.White, dur * 0.7f);
	}

	public static void FloatNum(Control parent, int amount, bool heal)
	{
		if (parent == null) return;
		var label = new Label
		{
			Text = heal ? $"+{amount}" : $"-{amount}",
			HorizontalAlignment = HorizontalAlignment.Center,
			Modulate = heal ? CombatColors.TextHeal : CombatColors.TextDanger,
			Scale = Vector2.One * 0.8f,
		};
		label.Position = new Vector2(parent.Size.X / 2f - 24, parent.Size.Y * 0.3f);
		parent.AddChild(label);

		var t = parent.CreateTween();
		t.SetParallel();
		t.TweenProperty(label, "position:y", label.Position.Y - 35f, 0.7f);
		t.TweenProperty(label, "scale", Vector2.One * 1.2f, 0.15f);
		t.TweenProperty(label, "modulate:a", 0f, 0.5f).SetDelay(0.2f);
		t.TweenCallback(Callable.From(() => label.QueueFree())).SetDelay(0.75f);
	}

	public static void TweenBar(ProgressBar bar, float target)
	{
		if (bar == null) return;
		// Only animate if value actually changed and bar is visible
		if (Mathf.Abs((float)bar.Value - target) < 0.5f) return;
		var t = bar.CreateTween();
		t.TweenProperty(bar, "value", target, 0.2f).SetEase(Tween.EaseType.Out);
	}

	public static void ShrinkDead(Control card)
	{
		if (card == null) return;
		var t = card.CreateTween();
		t.SetParallel();
		t.TweenProperty(card, "scale", Vector2.One * 0.75f, 0.35f).SetEase(Tween.EaseType.In);
		t.TweenProperty(card, "modulate", new Color(0.5f, 0.5f, 0.5f, 0.7f), 0.35f);
	}
}
