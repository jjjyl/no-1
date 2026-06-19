namespace No1.Core;

using System.Collections.Generic;
using System.Linq;
using Godot;
using No1.Data;

public class ShopData
{
	public List<ShopEntry> Stock = new();
	public int RefreshCount;
	public const int MaxRefreshes = 5;
	const int StockSize = 5;

	public class ShopEntry
	{
		public string ItemId;
		public int Price;
		public bool Sold;
	}

	public void Generate()
	{
		Stock.Clear();
		var rng = new RandomNumberGenerator();
		var pool = ItemDef.All.ToList();
		for (int i = 0; i < StockSize && pool.Count > 0; i++)
		{
			int idx = rng.RandiRange(0, pool.Count - 1);
			var def = pool[idx];
			pool.RemoveAt(idx);
			int price = Mathf.Clamp(rng.RandiRange(1, 10), 1, 10);
			Stock.Add(new ShopEntry { ItemId = def.Id, Price = price });
		}
	}

	public bool CanRefresh => RefreshCount < MaxRefreshes;

	public void Refresh()
	{
		if (!CanRefresh) return;
		RefreshCount++;
		Generate();
	}

	public bool Buy(string itemId, Inventory inv)
	{
		var entry = Stock.FirstOrDefault(e => e.ItemId == itemId && !e.Sold);
		if (entry == null) return false;
		var cm = CycleManager.Instance;
		if (cm == null || cm.Money < entry.Price) return false;
		if (inv == null) return false;

		cm.Money -= entry.Price;
		inv.AddItem(itemId, 1);
		entry.Sold = true;
		return true;
	}
}
