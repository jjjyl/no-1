namespace No1.UI;

using Godot;
using No1.Core;

/// <summary>
/// Out-of-combat item target selector. Lists player + alive companions as clickable buttons.
/// </summary>
public partial class TargetSelectPopup : Control
{
	public System.Action<CharacterStats> OnTargetSelected;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.FullRect);

		// Semi-transparent backdrop to block clicks behind
		var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.4f) };
		backdrop.SetAnchorsPreset(LayoutPreset.FullRect);
		backdrop.GuiInput += (e) =>
		{
			if (e is InputEventMouseButton { Pressed: true })
				QueueFree();
		};
		AddChild(backdrop);

		// Panel
		var panel = new Panel { Size = new Vector2(240, 0) };
		panel.SetAnchorsPreset(LayoutPreset.Center);
		panel.Position = new Vector2(-120, -100);
		AddChild(panel);

		var vbox = new VBoxContainer { Position = new Vector2(12, 12), Size = new Vector2(216, 0) };
		panel.AddChild(vbox);

		var titleLabel = new Label { Text = "对谁使用？" };
		vbox.AddChild(titleLabel);

		var cm = CycleManager.Instance;
		if (cm == null) { QueueFree(); return; }

		AddTargetButton(vbox, cm.PlayerStats.DisplayName, cm.PlayerStats);

		foreach (var comp in cm.ActiveCompanions)
		{
			if (!comp.Alive) continue;
			var st = comp.CurrentStats();
			var captured = comp;
			AddTargetButton(vbox, comp.Name, st, () => captured.SyncFromStats(st));
		}

		var cancelBtn = new Button { Text = "取消", SizeFlagsHorizontal = SizeFlags.Fill };
		cancelBtn.Pressed += QueueFree;
		vbox.AddChild(cancelBtn);
	}

	void AddTargetButton(Container parent, string label, CharacterStats stats, System.Action afterUse = null)
	{
		var btn = new Button
		{
			Text = label,
			SizeFlagsHorizontal = SizeFlags.Fill
		};
		btn.Pressed += () =>
		{
			OnTargetSelected?.Invoke(stats);
			afterUse?.Invoke();
			QueueFree();
		};
		parent.AddChild(btn);
	}
}
