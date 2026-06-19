namespace No1.UI;

using Godot;
using No1.Core;
using No1.Data;

/// <summary>
/// Shop UI — you design shop.tscn.
/// Required nodes (all [Export]):
///   _stockList   — Container for item buttons
///   _moneyLabel  — Label showing current money
///   _refreshBtn  — Button to refresh stock
///   _closeBtn    — Button to close
/// </summary>
public partial class ShopUI : Control
{
	[Export] Container _stockList;
	[Export] Label _moneyLabel;
	[Export] Label _refreshLabel;
	[Export] Button _refreshBtn;
	[Export] Button _closeBtn;

	ShopData _shop;

	public override void _Ready()
	{
		_closeBtn.Pressed += QueueFree;
		_refreshBtn.Pressed += OnRefresh;
	}

	public void SetShopData(ShopData shop)
	{
		_shop = shop;
		if (_shop.Stock.Count == 0) _shop.Generate();
		Populate();
	}

	void Populate()
	{
		var cm = CycleManager.Instance;
		if (_moneyLabel != null)
			_moneyLabel.Text = $"钱: {cm?.Money ?? 0}";

		if (_refreshLabel != null)
			_refreshLabel.Text = $"刷新 ({_shop.RefreshCount}/{ShopData.MaxRefreshes})";
		if (_refreshBtn != null)
			_refreshBtn.Disabled = !_shop.CanRefresh;

		if (_stockList == null) return;
		foreach (var child in _stockList.GetChildren())
			child.QueueFree();

		foreach (var entry in _shop.Stock)
		{
			var def = ItemDef.Get(entry.ItemId);
			if (def == null) continue;

			var hbox = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.Fill };
			var nameLabel = new Label
			{
				Text = $"{def.Name}",
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			var priceLabel = new Label
			{
				Text = entry.Sold ? "[已售]" : $"{entry.Price}钱",
				CustomMinimumSize = new Vector2(60, 0)
			};
			priceLabel.AddThemeColorOverride("font_color",
				entry.Sold ? Colors.Gray : new Color(1f, 0.85f, 0.3f));

			hbox.AddChild(nameLabel);
			hbox.AddChild(priceLabel);

			if (!entry.Sold)
			{
				var buyBtn = new Button { Text = "购买" };
				var captured = entry;
				buyBtn.Pressed += () => OnBuy(captured);
				hbox.AddChild(buyBtn);
			}

			_stockList.AddChild(hbox);
		}
	}

	void OnBuy(ShopData.ShopEntry entry)
	{
		var cm = CycleManager.Instance;
		if (cm == null) return;
		var inv = cm.PlayerInventory;
		if (_shop.Buy(entry.ItemId, inv))
			Populate();
		else
			Populate(); // refresh money display even on failure
	}

	void OnRefresh()
	{
		_shop.Refresh();
		Populate();
	}
}
