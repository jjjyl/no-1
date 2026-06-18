namespace No1.UI;

using Godot;

public partial class CharacterHpBars : Control
{
	[Export] ProgressBar _bruiseBar;
	[Export] ProgressBar _severeBar;
	[Export] RichTextLabel _nameLabel;

	Label _bruiseLabel, _severeLabel;

	public override void _Ready()
	{
		AddBarLabel(_bruiseBar, out _bruiseLabel);
		_bruiseBar.CustomMinimumSize = new(_bruiseBar.CustomMinimumSize.X, 20);
		AddBarLabel(_severeBar, out _severeLabel);
		_severeBar.CustomMinimumSize = new(_severeBar.CustomMinimumSize.X, 20);
	}

	public void Refresh(CharacterStats stats)
	{
		if (stats == null) return;

		_bruiseBar.MaxValue = stats.MaxBruiseHP; _bruiseBar.Value = stats.BruiseHP;
		_severeBar.MaxValue = stats.MaxSevereHP; _severeBar.Value = stats.SevereHP;
		_bruiseLabel.Text = $"擦: {stats.BruiseHP}/{stats.MaxBruiseHP}";
		_severeLabel.Text = $"重: {stats.SevereHP}/{stats.MaxSevereHP}";

		if (_nameLabel != null)
			_nameLabel.Text = $"[b]{stats.DisplayName}[/b]";
	}

	void AddBarLabel(ProgressBar bar, out Label lbl)
	{
		bar.ShowPercentage = false;
		lbl = new Label();
		lbl.HorizontalAlignment = HorizontalAlignment.Center;
		lbl.VerticalAlignment = VerticalAlignment.Center;
		lbl.MouseFilter = Control.MouseFilterEnum.Ignore;
		lbl.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		bar.AddChild(lbl);
	}
}
