namespace No1.UI;

using Godot;
using No1.Core;

public partial class DialogueManager : Control
{
	public static DialogueManager Instance { get; private set; } = null!;
	public bool IsOverlayOpen { get; set; }
	int _overlayCount;
	public int OverlayCountDebug => _overlayCount;

	PackedScene _bannerScene;
	PackedScene _fullDialogueScene;
	PackedScene _inventoryScene;
	PackedScene _characterPanelScene;
	PackedScene _itemTargetScene;
	PackedScene _shopScene;

	ShopData _currentShop;
	PackedScene _giftTargetScene;

	public override void _Ready()
	{
		Instance = this;
		_bannerScene = GD.Load<PackedScene>("res://scenes/ui/banner_popup.tscn");
		_fullDialogueScene = GD.Load<PackedScene>("res://scenes/ui/full_dialogue.tscn");
		_inventoryScene = GD.Load<PackedScene>("res://scenes/ui/inventory.tscn");
		_characterPanelScene = GD.Load<PackedScene>("res://scenes/ui/character_panel.tscn");
		_itemTargetScene = GD.Load<PackedScene>("res://scenes/ui/item_target_select.tscn");
		_shopScene = GD.Load<PackedScene>("res://scenes/ui/shop.tscn");
		_giftTargetScene = GD.Load<PackedScene>("res://scenes/ui/gift_target_select.tscn");
		GD.Print($"[DM] _Ready — banner={_bannerScene != null} dialogue={_fullDialogueScene != null}");
	}

	public BannerPopup ShowBanner(string speaker, string text, string position = "top", float autoHide = 0f)
	{
		GD.Print($"[DM] ShowBanner: '{speaker}' pos={position} auto={autoHide}");
		if (_bannerScene == null) { GD.PrintErr("[DM] bannerScene null"); return null!; }
		var node = _bannerScene.Instantiate();
		var banner = node as BannerPopup;
		if (banner == null) { GD.PrintErr("[DM] banner not BannerPopup"); node.QueueFree(); return null!; }

		AddToOverlay(banner);
		banner.Show(speaker, text, position, autoHide);
		return banner;
	}

	public FullDialogue ShowFullDialogue(string speaker, string text, string position = "center",
		string portraitExpression = null, string entry = "slide_bottom", string effect = null)
	{
		GD.Print($"[DM] ShowFullDialogue: '{speaker}' pos={position} expr={portraitExpression} entry={entry} fx={effect}");
		if (_fullDialogueScene == null) { GD.PrintErr("[DM] fullDialogueScene null"); return null!; }
		var node = _fullDialogueScene.Instantiate();
		var dlg = node as FullDialogue;
		if (dlg == null) { GD.PrintErr("[DM] dlg not FullDialogue"); node.QueueFree(); return null!; }

		AddToOverlay(dlg);
		dlg.Show(speaker, text, position, portraitExpression, entry, effect);
		return dlg;
	}

	public InventoryPanel ShowInventory()
	{
		if (_inventoryScene == null) return null!;
		var node = _inventoryScene.Instantiate();
		var panel = node as InventoryPanel;
		if (panel == null) { node.QueueFree(); return null!; }

		panel.OnUseConsumable = (itemId, inv) =>
		{
			if (_itemTargetScene == null) return;
			var selNode = _itemTargetScene.Instantiate();
			var sel = selNode as ItemTargetSelect;
			if (sel == null) { selNode.QueueFree(); return; }
			sel.Setup(itemId, inv);
			sel.TreeExited += () => panel.RefreshAll();
			AddToOverlay(sel);
		};

		panel.OnGiveItem = (itemId, fromInv) =>
		{
			GiftTargetSelect gSel;
			if (_giftTargetScene != null)
			{
				var gNode = _giftTargetScene.Instantiate();
				gSel = gNode as GiftTargetSelect;
				if (gSel == null) { gNode.QueueFree(); return; }
			}
			else
			{
				gSel = new GiftTargetSelect();
			}
			gSel.Setup(itemId, fromInv);
			gSel.TreeExited += () => panel.RefreshAll();
			AddToOverlay(gSel);
		};

		AddToOverlay(panel);
		return panel;
	}

	public ShopUI ShowShop()
	{
		if (_shopScene == null) return null!;
		var node = _shopScene.Instantiate();
		var shop = node as ShopUI;
		if (shop == null) { node.QueueFree(); return null!; }
		if (_currentShop == null) _currentShop = new ShopData();
		shop.SetShopData(_currentShop);
		AddToOverlay(shop);
		return shop;
	}

	public void ResetShop() => _currentShop = null;

	public CharacterPanel ShowCharacterPanel()
	{
		if (_characterPanelScene == null) return null!;
		var node = _characterPanelScene.Instantiate();
		var panel = node as CharacterPanel;
		if (panel == null) { node.QueueFree(); return null!; }
		AddToOverlay(panel);
		return panel;
	}

	void AddToOverlay(Control ctrl)
	{
		_overlayCount++;
		IsOverlayOpen = true;
		ctrl.TreeExited += () =>
		{
			_overlayCount--;
			GD.Print($"[DM] overlay -=1 → {_overlayCount}");
			if (_overlayCount <= 0) { _overlayCount = 0; IsOverlayOpen = false; GD.Print("[DM] All overlays closed"); }
		};

		var layer = new CanvasLayer { Layer = 10 };
		layer.AddChild(ctrl);
		GetTree().CurrentScene.AddChild(layer);
		GD.Print($"[DM] overlay +=1 → {_overlayCount} ({ctrl.GetType().Name})");
	}

	public static bool IsFullDialogueActive()
	{
		if (Instance == null || !Instance.IsInsideTree()) return false;
		var scene = Instance.GetTree().CurrentScene;
		if (scene == null) return false;
		foreach (var child in scene.GetChildren())
		{
			if (child is CanvasLayer layer)
				foreach (var c in layer.GetChildren())
					if (c is FullDialogue dlg && dlg.Visible)
						return true;
		}
		return false;
	}
}
