namespace No1.UI;

using Godot;

public partial class FullDialogue : Control
{
	public enum DialogPosition { Center, Top, Bottom }

	[Export] ColorRect _bg, _dialogBox;
	[Export] RichTextLabel _nameLabel, _textLabel, _hintLabel;
	[Export] Control _portraitArea;
	[Export] TextureRect _portraitTex;

	static readonly Dictionary<DialogPosition, float> _dialogHeights = new()
	{
		[DialogPosition.Center] = -220f,
		[DialogPosition.Top]    = -300f,
		[DialogPosition.Bottom]  = -160f,
	};

	DialogPosition _currentPos = DialogPosition.Center;

	public override void _Ready()
	{
		Visible = false;
		if (_portraitArea != null)
			_portraitArea.Visible = false;
	}

	public void Show(string speaker, string text, string positionStr = "center", string portraitExpression = null)
	{
		if (Enum.TryParse<DialogPosition>(positionStr, ignoreCase: true, out var p))
			LayoutAt(p);

		if (_nameLabel != null) _nameLabel.Text = $"[b]{speaker}[/b]";
		if (_textLabel != null) _textLabel.Text = text;
		Visible = true;

		if (!string.IsNullOrEmpty(portraitExpression))
			ShowPortrait(speaker, portraitExpression);
		else if (_portraitArea != null)
			_portraitArea.Visible = false;
	}

	void LayoutAt(DialogPosition pos)
	{
		_currentPos = pos;
		if (_dialogBox != null)
			_dialogBox.OffsetTop = _dialogHeights[pos];
	}

	void ShowPortrait(string charName, string expression)
	{
		if (_portraitArea == null || _portraitTex == null) return;

		var tex = No1.Core.PortraitManager.LoadPortrait(charName, expression);
		if (tex != null)
		{
			_portraitTex.Texture = tex;
			_portraitArea.Visible = true;

			float viewportH = GetViewportRect().Size.Y;
			GD.Print($"[FullDialogue] ShowPortrait: {charName}/{expression} vpH={viewportH}");
			var tween = CreateTween();
			tween.TweenProperty(_portraitTex, "position:y", 0f, 0.6f)
				.From(viewportH)
				.SetTrans(Tween.TransitionType.Quad)
				.SetEase(Tween.EaseType.Out);
		}
		else
		{
			_portraitArea.Visible = false;
		}
	}

	void HidePortrait()
	{
		if (_portraitArea == null || _portraitTex == null || !_portraitArea.Visible) return;

		float viewportH = GetViewportRect().Size.Y;
		var tween = CreateTween();
		tween.TweenProperty(_portraitTex, "position:y", viewportH, 0.3f)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.In);
		tween.TweenCallback(Callable.From(() => _portraitArea.Visible = false));
	}

	public override void _Input(InputEvent e)
	{
		if (!Visible) return;
		if (e is InputEventMouseButton mb && mb.Pressed) QueueFree();
		if (e is InputEventKey k && k.Pressed && (k.Keycode == Key.Enter || k.Keycode == Key.Space)) QueueFree();
	}

	public bool IsShowing => Visible;
}
