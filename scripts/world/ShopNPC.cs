namespace No1.World;

using Godot;
using No1.UI;

public partial class ShopNPC : Node3D
{
	Area3D _trigger;
	BannerPopup _banner;

	public override void _Ready()
	{
		_trigger = GetNodeOrNull<Area3D>("Trigger");
		if (_trigger == null)
		{
			foreach (var child in GetChildren())
				if (child is Area3D a) { _trigger = a; break; }
		}
		if (_trigger == null) return;

		_trigger.BodyEntered += OnBodyEntered;
		_trigger.BodyExited += OnBodyExited;
	}

	void OnBodyEntered(Node3D body)
	{
		if (body is not CharacterBody3D) return;
		if (_banner != null && _banner.IsActive) return;

		var dm = DialogueManager.Instance;
		if (dm == null) return;

		_banner = dm.ShowBanner("商人", "欢迎！要看看货吗？", "center");
		if (_banner != null)
		{
			_banner.AddButton("交易", () =>
		{
			dm.ShowShop();
		});
			_banner.TreeExited += () => _banner = null;
		}
	}

	void OnBodyExited(Node3D body)
	{
		if (body is not CharacterBody3D) return;
		if (_banner != null)
		{
			_banner.Hide();
			_banner = null;
		}
	}
}
