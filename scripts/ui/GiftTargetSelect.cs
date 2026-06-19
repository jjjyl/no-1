namespace No1.UI;

using Godot;
using No1.Core;
using No1.Data;

public partial class GiftTargetSelect : Control
{
	[Export] Container _targetList;
	[Export] Label _titleLabel;
	[Export] Button _cancelBtn;

	string _itemId;
	Inventory _fromInv;
	Dictionary<Button, Inventory> _targetMap = new();

	public void Setup(string itemId, Inventory fromInv)
	{
		_itemId = itemId;
		_fromInv = fromInv;

		// Programmatic fallback: create a VBoxContainer layout if any export is missing
		bool needsLayout = _targetList == null || _titleLabel == null || _cancelBtn == null;
		if (needsLayout)
		{
			var vbox = new VBoxContainer();
			vbox.SetAnchorsPreset(LayoutPreset.Center);
			vbox.SetSize(new Vector2(300, 200));
			AddChild(vbox);

			if (_titleLabel == null)
			{
				_titleLabel = new Label();
				vbox.AddChild(_titleLabel);
			}

			if (_targetList == null)
			{
				_targetList = new VBoxContainer();
				vbox.AddChild(_targetList);
			}

			if (_cancelBtn == null)
			{
				_cancelBtn = new Button { Text = "取消" };
				vbox.AddChild(_cancelBtn);
			}
		}

		var def = ItemDef.Get(itemId);
		if (_titleLabel != null)
			_titleLabel.Text = $"将 {def?.Name ?? itemId} 给谁？";

		if (_cancelBtn != null)
			_cancelBtn.Pressed += QueueFree;

		var cm = CycleManager.Instance;
		if (cm == null) { QueueFree(); return; }

		// Player button
		AddTargetButton("玩家", cm.PlayerInventory);

		// Companion buttons
		foreach (var comp in cm.ActiveCompanions)
		{
			if (!comp.Alive) continue;
			if (comp.Inventory == null) continue;
			if (comp.Inventory == fromInv) continue; // can't give to self

			AddTargetButton(comp.Name, comp.Inventory);
		}
	}

	void AddTargetButton(string label, Inventory targetInv)
	{
		var btn = new Button { Text = label, SizeFlagsHorizontal = SizeFlags.Fill };
		btn.Pressed += () => PickTarget(targetInv);
		_targetMap[btn] = targetInv;
		_targetList?.AddChild(btn);
	}

	void PickTarget(Inventory targetInv)
	{
		// Clear target list
		foreach (var child in _targetList.GetChildren())
			child.QueueFree();

		var maxAmount = Math.Min(_fromInv.GetCount(_itemId), 3);

		var spinBox = new SpinBox
		{
			MinValue = 1,
			MaxValue = maxAmount,
			Value = 1,
			SizeFlagsHorizontal = SizeFlags.Fill
		};
		_targetList.AddChild(spinBox);

		var confirmBtn = new Button
		{
			Text = "确认",
			SizeFlagsHorizontal = SizeFlags.Fill
		};
		confirmBtn.Pressed += () => ConfirmGive(targetInv, (int)spinBox.Value);
		_targetList.AddChild(confirmBtn);
	}

	void ConfirmGive(Inventory targetInv, int amount)
	{
		var result = _fromInv.TransferTo(targetInv, _itemId, amount);
		if (result != null)
		{
			if (_titleLabel != null)
				_titleLabel.Text = $"[color=red]{result}[/color]";
			return;
		}
		QueueFree();
	}
}
