namespace No1.UI;

using Godot;
using No1.Core;
using No1.Data;

/// <summary>
/// Item target selector scene — you design item_target_select.tscn.
///
/// Required nodes (all [Export]):
///   _targetList   — Container for target buttons
///   _titleLabel   — Label to show "对谁使用 {itemName}?"
///   _cancelBtn    — Button to close
///
/// Called by InventoryPanel.OnUseConsumable.
///
/// Your buttons should call SelectTarget(playerStats, null) for player
/// or SelectTarget(compStats, companionState) for companions.
/// </summary>
public partial class ItemTargetSelect : Control
{
	[Export] Container _targetList;
	[Export] Label _titleLabel;
	[Export] Button _cancelBtn;

	string _itemId;
	Inventory _inv;

	public void Setup(string itemId, Inventory inv)
	{
		_itemId = itemId;
		_inv = inv;

		var def = ItemDef.Get(itemId);
		if (_titleLabel != null)
			_titleLabel.Text = $"对谁使用 {def?.Name ?? itemId}？";

		if (_cancelBtn != null)
			_cancelBtn.Pressed += QueueFree;

		var cm = CycleManager.Instance;
		if (cm == null) { QueueFree(); return; }

		AddTargetButton(cm.PlayerStats.DisplayName, () =>
		{
			ApplyAndClose(cm.PlayerStats);
		});

		foreach (var comp in cm.ActiveCompanions)
		{
			if (!comp.Alive) continue;
			var captured = comp;
			AddTargetButton(comp.Name, () =>
			{
				var st = captured.CurrentStats();
				ApplyAndClose(st);
				captured.SyncFromStats(st);
			});
		}
	}

	void AddTargetButton(string label, System.Action onPressed)
	{
		var btn = new Button { Text = label, SizeFlagsHorizontal = SizeFlags.Fill };
		btn.Pressed += onPressed;
		_targetList?.AddChild(btn);
	}

	void ApplyAndClose(CharacterStats target)
	{
		var result = _inv.UseItemOn(_itemId, target);
		if (result != null)
			GD.PrintErr($"[ItemTargetSelect] UseItemOn failed: {result}");
		QueueFree();
	}
}
