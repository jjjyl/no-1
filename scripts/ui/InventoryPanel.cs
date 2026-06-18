namespace No1.UI;

using Godot;
using No1.Core;
using No1.Data;

public partial class InventoryPanel : Control
{
	[Export] Container _itemGrid;
	[Export] RichTextLabel _equipDisplay;
	[Export] RichTextLabel _itemDesc;
	[Export] Button _actionBtn;
	[Export] Button _closeBtn;

	string _selectedItemId;
	Inventory _inv;

	public override void _Ready()
	{
		_inv = CycleManager.Instance.PlayerInventory;
		if (_inv == null) { QueueFree(); return; }

		_closeBtn.Pressed += () =>
		{
			var parent = GetParent();
			QueueFree();
			parent?.QueueFree();
		};
		_actionBtn.Pressed += OnAction;
		_actionBtn.Disabled = true;
		RefreshAll();
	}

	void RefreshAll()
	{
		if (_itemGrid != null)
		{
			foreach (var child in _itemGrid.GetChildren())
				child.QueueFree();

			foreach (var itemId in _inv.GetAllItemIds())
			{
				var count = _inv.GetCount(itemId);
				if (count <= 0) continue;
				var def = ItemDef.Get(itemId);
				if (def == null) continue;

				var btn = new Button();
				btn.Text = $"{def.Name} \u00d7{count}";
				btn.CustomMinimumSize = new Vector2(180, 30);
				btn.SizeFlagsHorizontal = Control.SizeFlags.Fill;

				if (_inv.IsEquipped(itemId))
					btn.AddThemeColorOverride("font_color", new Color(0.5f, 1.0f, 0.5f));
				else if (def.Type == ItemType.Equipment)
					btn.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 1.0f));
				else
					btn.AddThemeColorOverride("font_color", new Color(1.0f, 1.0f, 0.8f));

				var captured = itemId;
				btn.Pressed += () => SelectItem(captured);
				_itemGrid.AddChild(btn);
			}
		}

		if (_equipDisplay != null)
		{
			var text = "[b]\u88c5\u5907\u680f[/b]\n";
			foreach (EquipmentSlot slot in System.Enum.GetValues<EquipmentSlot>())
			{
				var slotName = slot switch
				{
					EquipmentSlot.Weapon => "\u6b66\u5668",
					EquipmentSlot.Armor => "\u9632\u5177",
					EquipmentSlot.Accessory => "\u9970\u54c1",
					_ => slot.ToString()
				};
				var equippedId = _inv.GetEquipped(slot);
				if (equippedId != null)
				{
					var def = ItemDef.Get(equippedId);
					text += $"  {slotName}: [color=#88ff88]{def?.Name ?? equippedId}[/color]\n";
				}
				else
				{
					text += $"  {slotName}: [color=gray]\u7a7a[/color]\n";
				}
			}
			_equipDisplay.Text = text;
		}
	}

	void SelectItem(string itemId)
	{
		_selectedItemId = itemId;
		var def = ItemDef.Get(itemId);
		if (def == null) return;

		var count = _inv.GetCount(itemId);
		var equipped = _inv.IsEquipped(itemId);

		var desc = $"[b]{def.Name}[/b] \u00d7{count}\n{def.Description}";
		if (equipped)
		{
			desc += "\n[color=#88ff88][\u5df2\u88c5\u5907][/color]";
			if (_actionBtn != null) _actionBtn.Text = "\u5378\u4e0b";
		}
		else if (def.Type == ItemType.Equipment)
		{
			desc += "\n[color=#8888ff]\u70b9\u51fb\u88c5\u5907[/color]";
			if (_actionBtn != null) _actionBtn.Text = "\u88c5\u5907";
		}
		else
		{
			desc += "\n[color=#ffff88]\u70b9\u51fb\u4f7f\u7528[/color]";
			if (_actionBtn != null) _actionBtn.Text = "\u4f7f\u7528";
		}

		if (_itemDesc != null) _itemDesc.Text = desc;
		if (_actionBtn != null) _actionBtn.Disabled = false;
	}

	void OnAction()
	{
		if (string.IsNullOrEmpty(_selectedItemId)) return;
		var def = ItemDef.Get(_selectedItemId);
		if (def == null) return;

		string result;
		if (def.Type == ItemType.Equipment)
		{
			if (_inv.IsEquipped(_selectedItemId))
				result = _inv.UnequipItem(def.Slot);
			else
				result = _inv.EquipItem(_selectedItemId);
		}
		else
		{
			result = _inv.UseItem(_selectedItemId);
		}

		if (result != null)
		{
			if (_itemDesc != null) _itemDesc.Text += $"\n[color=red]{result}[/color]";
		}
		else
		{
			_selectedItemId = null;
			if (_actionBtn != null) _actionBtn.Disabled = true;
		}

		RefreshAll();
	}
}
