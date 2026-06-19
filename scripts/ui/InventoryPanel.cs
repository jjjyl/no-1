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
	[Export] Button _giveBtn;
	[Export] Label _ownerLabel;
	[Export] Container _characterList;

	string _selectedItemId;
	Inventory _inv;
	string _ownerName;

	// Set this to handle consumable usage with your own target selector.
	// Called with (itemId, inventory). Your handler should:
	//   1. Show your target selector UI
	//   2. On target picked: inv.UseItemOn(itemId, target.CharacterStats)
	//   3. Handle companion HP sync via target.CompanionState?.SyncFromStats(stats)
	//   4. Call RefreshAll() when done
	public System.Action<string, Inventory> OnUseConsumable;
	public System.Action<string, Inventory> OnGiveItem;

	public override void _Ready()
	{
		_closeBtn.Pressed += QueueFree;
		_actionBtn.Pressed += OnAction;
		_actionBtn.Disabled = true;
		if (_giveBtn != null) _giveBtn.Pressed += OnGive;
	}

	public void SetInventory(Inventory inv, string ownerName)
	{
		_inv = inv;
		_ownerName = ownerName;
		_selectedItemId = null;
		_actionBtn.Disabled = true;
		if (_itemDesc != null) _itemDesc.Text = "";
		RefreshAll();
	}

	public void RefreshAll()
	{
		if (_ownerLabel != null)
			_ownerLabel.Text = $"背包 — {_ownerName ?? ""}";
		RefreshCharacterList();
		if (_itemGrid != null)
		{
			foreach (var child in _itemGrid.GetChildren())
				child.QueueFree();

			if (_inv == null) return;

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
			var showEquip = _inv != null && _inv.Owner != null;
			if (!showEquip)
			{
				_equipDisplay.Text = "";
				return;
			}

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

	void RefreshCharacterList()
	{
		if (_characterList == null) return;
		foreach (var child in _characterList.GetChildren())
			child.QueueFree();

		var cm = CycleManager.Instance;
		if (cm == null) return;

		AddCharButton("玩家", () => SetInventory(cm.PlayerInventory, "玩家"));

		foreach (var comp in cm.ActiveCompanions)
		{
			if (!comp.Alive) continue;
			var cap = comp;
			AddCharButton(comp.Name, () => SetInventory(cap.Inventory, cap.Name));
		}
	}

	void AddCharButton(string label, System.Action onPress)
	{
		var btn = new Button
		{
			Text = label,
			CustomMinimumSize = new Vector2(60, 28),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
		};
		bool isActive = (label == _ownerName) || (label == "玩家" && _inv == CycleManager.Instance?.PlayerInventory);
		if (isActive)
			btn.AddThemeColorOverride("font_color", new Color(0.3f, 1.0f, 0.3f));
		btn.Pressed += onPress;
		_characterList.AddChild(btn);
	}

	void SelectItem(string itemId)
	{
		_selectedItemId = itemId;
		if (_giveBtn != null) _giveBtn.Disabled = true;
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
			var canEquip = _inv.Owner != null;
			desc += canEquip ? "\n[color=#8888ff]\u70b9\u51fb\u88c5\u5907[/color]" : "\n[color=gray]\u65e0\u6cd5\u88c5\u5907[/color]";
			if (_actionBtn != null)
			{
				_actionBtn.Text = "\u88c5\u5907";
				_actionBtn.Disabled = !canEquip;
			}
		}
		else
		{
			desc += "\n[color=#ffff88]\u70b9\u51fb\u4f7f\u7528[/color]";
			if (_actionBtn != null)
			{
				_actionBtn.Text = "\u4f7f\u7528";
				_actionBtn.Disabled = false;
			}
			if (_giveBtn != null) { _giveBtn.Text = "\u7ed9\u4e88"; _giveBtn.Disabled = false; }
		}

		if (_itemDesc != null) _itemDesc.Text = desc;
	}

	void OnAction()
	{
		if (string.IsNullOrEmpty(_selectedItemId) || _inv == null) return;
		var def = ItemDef.Get(_selectedItemId);
		if (def == null) return;

		if (def.Type == ItemType.Equipment)
		{
			string result;
			if (_inv.IsEquipped(_selectedItemId))
				result = _inv.UnequipItem(def.Slot);
			else
				result = _inv.EquipItem(_selectedItemId);

			if (result != null && _itemDesc != null)
				_itemDesc.Text += $"\n[color=red]{result}[/color]";

			_selectedItemId = null;
			_actionBtn.Disabled = true;
			RefreshAll();
		}
		else
		{
			OnUseConsumable?.Invoke(_selectedItemId, _inv);
		}
	}

	void OnGive()
	{
		if (string.IsNullOrEmpty(_selectedItemId) || _inv == null) return;
		OnGiveItem?.Invoke(_selectedItemId, _inv);
	}
}
