namespace No1.UI;

using Godot;

public partial class DialogueManager : Control
{
	public static DialogueManager Instance { get; private set; } = null!;

	PackedScene _bannerScene;
	PackedScene _fullDialogueScene;
	PackedScene _inventoryScene;

	public override void _Ready()
	{
		Instance = this;
		_bannerScene = GD.Load<PackedScene>("res://scenes/ui/banner_popup.tscn");
		_fullDialogueScene = GD.Load<PackedScene>("res://scenes/ui/full_dialogue.tscn");
		_inventoryScene = GD.Load<PackedScene>("res://scenes/ui/inventory.tscn");
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

	public FullDialogue ShowFullDialogue(string speaker, string text, string position = "center")
	{
		GD.Print($"[DM] ShowFullDialogue: '{speaker}' pos={position}");
		if (_fullDialogueScene == null) { GD.PrintErr("[DM] fullDialogueScene null"); return null!; }
		var node = _fullDialogueScene.Instantiate();
		var dlg = node as FullDialogue;
		if (dlg == null) { GD.PrintErr("[DM] dlg not FullDialogue"); node.QueueFree(); return null!; }

		AddToOverlay(dlg);
		dlg.Show(speaker, text, position);
		return dlg;
	}

	public InventoryPanel ShowInventory()
	{
		if (_inventoryScene == null) return null!;
		var node = _inventoryScene.Instantiate();
		var panel = node as InventoryPanel;
		if (panel == null) { node.QueueFree(); return null!; }
		AddToOverlay(panel);
		return panel;
	}

	void AddToOverlay(Control ctrl)
	{
		var layer = new CanvasLayer { Layer = 10 };
		layer.AddChild(ctrl);
		GetTree().CurrentScene.AddChild(layer);
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
