namespace No1.UI;

using Godot;

public partial class BannerPopup : Control
{
	public enum BannerPosition { Top, Middle, Bottom, TopLeft, TopRight, BottomLeft, BottomRight }

	[Export] ColorRect _bg;
	[Export] ColorRect _portrait;
	[Export] RichTextLabel _initialLabel, _nameLabel, _textLabel, _hintLabel;
	[Export] Control _bannerBox;

	bool _showing;
	BannerPosition _currentPos = BannerPosition.Top;

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		Visible = false;

		// Transparent full-screen backdrop catches dismiss clicks without blocking children.
		// Button sits on top with MouseFilter.Stop so its Pressed fires before backdrop's GuiInput.
		var backdrop = new ColorRect
		{
			Color = new Color(0, 0, 0, 0),
			Size = Size,
			MouseFilter = MouseFilterEnum.Stop
		};
		backdrop.GuiInput += e =>
		{
			if (e is InputEventMouseButton mb && mb.Pressed)
			{
				GD.Print($"[BannerPopup] Backdrop dismiss at {mb.Position}");
				Hide();
				AcceptEvent();
			}
		};
		AddChild(backdrop);
		MoveChild(backdrop, 0);

		GD.Print($"[BannerPopup] _Ready — size={Size} _bg={_bg != null}");
	}

	public async void Show(string speaker, string text, string positionStr = "top", float autoHide = 0f)
	{
		var pos = Enum.TryParse<BannerPosition>(positionStr, ignoreCase: true, out var p)
			? p : BannerPosition.Top;
		LayoutAt(pos);

		if (_nameLabel != null)
			_nameLabel.Text = string.IsNullOrEmpty(speaker) ? "" : $"[b]{speaker}[/b]";
		if (_textLabel != null)
			_textLabel.Text = text;
		if (_initialLabel != null)
			_initialLabel.Text = string.IsNullOrEmpty(speaker) ? "?" : speaker[..1];
		if (_portrait != null)
			_portrait.Color = GetColor(speaker);
		Visible = true;
		if (_bg != null)
			_bg.Visible = true;

		_showing = true;

		if (_hintLabel != null)
			_hintLabel.Visible = autoHide <= 0;

		GD.Print($"[BannerPopup] Show done: visible={(_bg != null && _bg.Visible)} position={pos}");

		if (autoHide > 0)
		{
			GD.Print($"[BannerPopup] Starting auto-hide timer: {autoHide}s");
			await ToSignal(GetTree().CreateTimer(autoHide), "timeout");
			GD.Print($"[BannerPopup] Timer fired, hiding...");
			Hide();
		}
	}

	void LayoutAt(BannerPosition pos)
	{
		_currentPos = pos;
		if (_bannerBox == null) return;

		float ax, ay, ox, oy;
		switch (pos)
		{
			case BannerPosition.Top:         ax = 0.5f; ay = 0;   ox = -320; oy = 20;  break;
			case BannerPosition.Middle:      ax = 0.5f; ay = 0.5f; ox = -320; oy = -40; break;
			case BannerPosition.Bottom:      ax = 0.5f; ay = 1;   ox = -320; oy = -100; break;
			case BannerPosition.TopLeft:     ax = 0;    ay = 0;   ox = 20;   oy = 20;  break;
			case BannerPosition.TopRight:    ax = 1;    ay = 0;   ox = -660; oy = 20;  break;
			case BannerPosition.BottomLeft:  ax = 0;    ay = 1;   ox = 20;   oy = -100; break;
			case BannerPosition.BottomRight: ax = 1;    ay = 1;   ox = -660; oy = -100; break;
			default:                         ax = 0.5f; ay = 0;   ox = -320; oy = 20;  break;
		}

		_bannerBox.AnchorLeft = ax; _bannerBox.AnchorRight = ax;
		_bannerBox.AnchorTop = ay; _bannerBox.AnchorBottom = ay;
		_bannerBox.OffsetLeft = ox; _bannerBox.OffsetRight = ox + 640;
		_bannerBox.OffsetTop = oy; _bannerBox.OffsetBottom = oy + 80;
	}

	Color GetColor(string name) => name switch
	{
		"艾薇" => new Color(0.3f, 0.7f, 0.4f),
		"天一" => new Color(0.6f, 0.6f, 0.9f),
		_      => new Color(0.5f, 0.5f, 0.5f),
	};

	public new void Hide()
	{
		GD.Print("[BannerPopup] Hide()");
		_showing = false;
		Visible = false;
		QueueFree();
	}

	public bool IsActive => _showing;

	public void AddButton(string text, System.Action onPressed)
	{
		var btn = new Button
		{
			Text = text,
			Position = new Vector2(540, 5),
			CustomMinimumSize = new Vector2(80, 30),
			MouseFilter = MouseFilterEnum.Stop
		};
		btn.Pressed += () =>
		{
			_showing = false;
			Visible = false;
			onPressed();
			QueueFree();
		};
		if (_bannerBox != null) _bannerBox.AddChild(btn);
		else AddChild(btn);
	}
}
