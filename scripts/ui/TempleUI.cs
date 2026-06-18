namespace No1.UI;

using Godot;
using No1.Core;
using No1.Data;

public partial class TempleUI : Control
{
	[Export] Label _cycleLabel;
	[Export] Button _insightBtn, _valorBtn, _wandererBtn, _enterBtn;
	[Export] RichTextLabel _blessingDesc;

	BlessingType? _selected;

	public override void _Ready()
	{
		_enterBtn.Disabled = true;
		_insightBtn.Pressed += () => Select(BlessingType.察知);
		_valorBtn.Pressed += () => Select(BlessingType.战意);
		_wandererBtn.Pressed += () => Select(BlessingType.旅人);
		_enterBtn.Pressed += Enter;

		_cycleLabel.Text = $"周目: {CycleManager.Instance.CurrentCycle}";
		var b = BlessingData.All;
		_insightBtn.Text = b[0].Name;
		_valorBtn.Text = b[1].Name;
		_wandererBtn.Text = b[2].Name;
	}

	void Select(BlessingType t)
	{
		_selected = t;
		var d = BlessingData.Get(t);
		_blessingDesc.Text = $"[b]{d.Name}[/b]\n{d.Description}";
		_enterBtn.Disabled = false;
	}

	void Enter()
	{
		if (_selected == null) return;
		CycleManager.Instance.SelectBlessing(_selected.Value);
		CycleManager.Instance.EnterWorld();
		GameManager.Instance.GoToScene("res://scenes/world/world_map.tscn");
	}
}
