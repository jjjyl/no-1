namespace No1.UI;

using Godot;

public partial class BannerPopup : Control
{
	public enum BannerPosition { Top, Middle, Bottom, TopLeft, TopRight, BottomLeft, BottomRight }

	[Export] ColorRect _bg;
	[Export] ColorRect _portrait;
	[Export] RichTextLabel _initialLabel, _nameLabel, _textLabel, _hintLabel;

	bool _showing;
	BannerPosition _currentPos = BannerPosition.Top;

	static readonly Dictionary<BannerPosition, Vector2> _anchors = new()
	{
		[BannerPosition.Top]         = new(320, 20),
		[BannerPosition.Middle]      = new(320, 320),
		[BannerPosition.Bottom]      = new(320, 600),
		[BannerPosition.TopLeft]     = new(20, 20),
		[BannerPosition.TopRight]    = new(620, 20),    // 1280 - 640 - 20 margin
		[BannerPosition.BottomLeft]  = new(20, 600),
		[BannerPosition.BottomRight] = new(620, 600),
	};

	public override void _Ready()
	{
		AnchorLeft = 0; AnchorTop = 0; AnchorRight = 0; AnchorBottom = 0;
		Size = new Vector2(1280, 720);
		Position = Vector2.Zero;
		Visible = false;
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
		Visible = true;       // 根节点 → 所有子节点可见
		if (_bg != null)
			_bg.Visible = true;   // 覆盖 tscn 里的 visible=false

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
		var anchor = _anchors[pos];

		if (_bg != null)          { _bg.Position = anchor; _bg.Size = new Vector2(640, 80); }
		if (_portrait != null)    { _portrait.Position = anchor + new Vector2(15, 0); _portrait.Size = new Vector2(56, 56); }
		if (_initialLabel != null){ _initialLabel.Position = anchor + new Vector2(15, 12); _initialLabel.Size = new Vector2(56, 56); }
		if (_nameLabel != null)   { _nameLabel.Position = anchor + new Vector2(85, 8); _nameLabel.Size = new Vector2(400, 22); }
		if (_textLabel != null)   { _textLabel.Position = anchor + new Vector2(85, 33); _textLabel.Size = new Vector2(520, 40); }
		if (_hintLabel != null)   { _hintLabel.Position = anchor + new Vector2(600, 55); _hintLabel.Size = new Vector2(40, 18); }
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
	}

	public bool IsActive => _showing;

	public override void _Input(InputEvent e)
	{
		if (!_showing) return;
		if (e is InputEventMouseButton mb && mb.Pressed)
		{
			GD.Print($"[BannerPopup] Click dismiss at {mb.Position}");
			Hide();
			return;
		}
		if (e is InputEventKey k && k.Pressed && (k.Keycode == Key.Enter || k.Keycode == Key.Space))
		{
			GD.Print("[BannerPopup] Key dismiss");
			Hide();
			return;
		}
	}
}
