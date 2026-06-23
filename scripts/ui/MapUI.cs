namespace No1.UI;

using Godot;
using No1.Core;

public partial class MapUI : Control
{
	[Export] Control _playerMarker;
	[Export] RichTextLabel _infoLabel, _statusLabel;
	[Export] Button _returnBtn;

	MapNodeData[] _nodes;
	Button[] _nodeBtns;
	int _current;

	public override void _Ready()
	{
		_returnBtn.Pressed += () =>
		{
			CycleManager.Instance.ReturnToTemple();
			GameManager.Instance.GoToScene(GameManager.SceneTemple);
		};

		_nodes = MapNodeData.LoadAll();
		_nodeBtns = new Button[_nodes.Length];
		float spacing = 1100f / (_nodes.Length + 1);

		for (int i = 0; i < _nodes.Length; i++)
		{
			var btn = new Button
			{
				Text = _nodes[i].Name,
				Position = new Vector2(spacing * (i + 1) - 60, 360),
				Size = new Vector2(120, 40)
			};
			btn.AddThemeFontSizeOverride("font_size", 16);
			AddChild(btn);
			int idx = i;
			btn.Pressed += () => OnClick(idx);
			_nodeBtns[i] = btn;
		}

		MoveTo(CycleManager.Instance.CurrentNodeIndex);
	}

	public override void _Process(double delta)
	{
		if (string.IsNullOrEmpty(CycleManager.Instance.PendingEnemyScene))
			return;

		// 全屏对话未关 → 暂不切战斗
		if (DialogueManager.IsFullDialogueActive())
			return;

		_statusLabel.Text = "[color=red]遭遇魔种！[/color]";
		GameManager.Instance.GoToScene("res://scenes/combat/combat.tscn");
		CycleManager.Instance.PendingEnemyScene = null;
	}

	void OnClick(int target)
	{
		if (target == _current) return;
		if (!_nodes[_current].Connections.Contains(target))
		{
			_statusLabel.Text = "[color=gray]太远了[/color]";
			return;
		}
		MoveTo(target);

		GD.Print($"[MapUI] Arrived at: {_nodes[target].Name}, checking events...");

		EventManager.CheckEvents(_nodes[target].Name, CycleManager.Instance, msg =>
		{
			if (!string.IsNullOrEmpty(msg)) _statusLabel.Text = msg;
		});

		// 战斗切场交给 _Process —— 若同时有对话则等对话关掉再切
	}

	void MoveTo(int idx)
	{
		_current = idx;
		_playerMarker.Position = _nodeBtns[idx].Position + new Vector2(52, 12);
		_infoLabel.Text = "[b]" + _nodes[idx].Name + "[/b]\n[font_size=12]" + _nodes[idx].Desc + "[/font_size]";
		CycleManager.Instance.CurrentNodeIndex = idx;
	}
}
