namespace No1.UI;
using Godot;
using No1.Core;
using No1.Data;

/// <summary>
/// Combat item selection popup. User designs combat_item_menu.tscn.
/// Required nodes: ItemList (Container for item buttons), CloseBtn (Button)
/// </summary>
public partial class CombatItemMenu : Control
{
	[Export] Container _itemList;
	[Export] Button _closeBtn;

	public System.Action<string> OnItemSelected;

	public Inventory SourceInventory;
	public bool GiveMode;
	public System.Action<string> OnItemGiven;

	public override void _Ready()
	{
		if (_closeBtn != null) _closeBtn.Pressed += QueueFree;
		PopulateItems();
	}

	public void PopulateItems()
	{
		if (_itemList == null) return;
		foreach (var child in _itemList.GetChildren()) child.QueueFree();

		var toggleRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.Fill };
		var activeGreen = new Color(0.3f, 1.0f, 0.3f);

		var useBtn = new Button { Text = "使用", SizeFlagsHorizontal = SizeFlags.Fill };
		useBtn.Modulate = GiveMode ? Colors.White : activeGreen;
		useBtn.Pressed += () => { GiveMode = false; PopulateItems(); };

		var giveBtn = new Button { Text = "给予", SizeFlagsHorizontal = SizeFlags.Fill };
		giveBtn.Modulate = GiveMode ? activeGreen : Colors.White;
		giveBtn.Pressed += () => { GiveMode = true; PopulateItems(); };

		toggleRow.AddChild(useBtn);
		toggleRow.AddChild(giveBtn);
		_itemList.AddChild(toggleRow);

		var inv = SourceInventory ?? CycleManager.Instance.PlayerInventory;
		if (inv == null) return;

		var consumables = inv.GetAllItemIds()
			.Where(id => ItemDef.Get(id)?.Type == ItemType.Consumable)
			.Where(id => inv.GetCount(id) > 0)
			.ToList();

		if (consumables.Count == 0)
		{
			_itemList.AddChild(new Label { Text = "没有可用的物品" });
			return;
		}

		foreach (var itemId in consumables)
		{
			if (GiveMode && inv.IsEquipped(itemId)) continue;

			var def = ItemDef.Get(itemId);
			var count = inv.GetCount(itemId);
			var btn = new Button
			{
				Text = $"{def?.Name ?? itemId} ×{count}",
				SizeFlagsHorizontal = SizeFlags.Fill
			};
			var captured = itemId;
			btn.Pressed += () =>
			{
				if (GiveMode)
					OnItemGiven?.Invoke(captured);
				else
				{
					OnItemSelected?.Invoke(captured);
					QueueFree();
				}
			};
			_itemList.AddChild(btn);
		}
	}
}
