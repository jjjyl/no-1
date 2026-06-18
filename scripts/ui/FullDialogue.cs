namespace No1.UI;

using Godot;

public partial class FullDialogue : Control
{
	public enum DialogPosition { Center, Top, Bottom }

	[Export] ColorRect _bg, _dialogBox;
	[Export] RichTextLabel _nameLabel, _textLabel, _hintLabel;

	static readonly Dictionary<DialogPosition, Vector2> _boxOffsets = new()
	{
		[DialogPosition.Center] = new(240, 160),
		[DialogPosition.Top]    = new(240, 40),
		[DialogPosition.Bottom]  = new(240, 520),
	};

	DialogPosition _currentPos = DialogPosition.Center;

	public override void _Ready()
	{
		Visible = false;
		StoreBasePoses();
	}

	Vector2 _baseBoxPos, _baseNamePos, _baseTextPos, _baseHintPos;

	void StoreBasePoses()
	{
		if (_dialogBox != null)   _baseBoxPos  = _dialogBox.Position;
		if (_nameLabel != null)   _baseNamePos = _nameLabel.Position;
		if (_textLabel != null)   _baseTextPos = _textLabel.Position;
		if (_hintLabel != null)   _baseHintPos = _hintLabel.Position;
	}

	public void Show(string speaker, string text, string positionStr = "center")
	{
		if (Enum.TryParse<DialogPosition>(positionStr, ignoreCase: true, out var p))
			LayoutAt(p);

		if (_nameLabel != null) _nameLabel.Text = $"[b]{speaker}[/b]";
		if (_textLabel != null) _textLabel.Text = text;
		Visible = true;
	}

	void LayoutAt(DialogPosition pos)
	{
		_currentPos = pos;
		var boxOffset = _boxOffsets[pos];
		var delta = boxOffset - _boxOffsets[DialogPosition.Center];

		if (_dialogBox != null) _dialogBox.Position = _baseBoxPos + delta;
		if (_nameLabel != null) _nameLabel.Position = _baseNamePos + delta;
		if (_textLabel != null) _textLabel.Position = _baseTextPos + delta;
		if (_hintLabel != null) _hintLabel.Position = _baseHintPos + delta;
	}

	public override void _Input(InputEvent e)
	{
		if (!Visible) return;
		if (e is InputEventMouseButton mb && mb.Pressed) Visible = false;
		if (e is InputEventKey k && k.Pressed && (k.Keycode == Key.Enter || k.Keycode == Key.Space)) Visible = false;
	}

	public bool IsShowing => Visible;
}
