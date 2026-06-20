namespace No1.UI;

using Godot;

public partial class FullDialogue : Control
{
	public enum DialogPosition { Center, Top, Bottom }

	[Export] ColorRect _bg, _dialogBox;
	[Export] RichTextLabel _nameLabel, _textLabel, _hintLabel;
	[Export] Control _portraitArea;

	static readonly Dictionary<DialogPosition, float> _dialogHeights = new()
	{
		[DialogPosition.Center] = -220f,
		[DialogPosition.Top]    = -300f,
		[DialogPosition.Bottom]  = -160f,
	};

	DialogPosition _currentPos = DialogPosition.Center;
	string _currentPortraitChar;
	bool _dismissing;
	TextureRect _activeRect;
	Tween _breatheTween;

	public override void _Ready()
	{
		Visible = false;
		if (_portraitArea != null)
			_portraitArea.Visible = false;
	}

	public void Show(string speaker, string text, string positionStr = "center",
		string portraitExpression = null, string entry = "slide_bottom", string effect = null)
	{
		if (Enum.TryParse<DialogPosition>(positionStr, ignoreCase: true, out var p))
			LayoutAt(p);

		if (_nameLabel != null) _nameLabel.Text = $"[b]{speaker}[/b]";
		if (_textLabel != null) _textLabel.Text = text;
		Visible = true;

		if (!string.IsNullOrEmpty(portraitExpression))
			ShowPortrait(speaker, portraitExpression, entry, effect);
		else
		{
			_currentPortraitChar = null;
			_activeRect = null;
			if (_portraitArea != null)
				_portraitArea.Visible = false;
		}
	}

	void LayoutAt(DialogPosition pos)
	{
		_currentPos = pos;
		if (_dialogBox != null)
			_dialogBox.OffsetTop = _dialogHeights[pos];
	}

	void ShowPortrait(string charName, string expression, string entry, string effect)
	{
		if (_portraitArea == null) return;

		var tex = No1.Core.PortraitManager.LoadPortrait(charName, expression);
		if (tex == null) { _portraitArea.Visible = false; _currentPortraitChar = null; _activeRect = null; return; }

		// Same character — just swap texture and play effect
		if (_currentPortraitChar == charName && _activeRect != null)
		{
			_activeRect.Texture = tex;
			_portraitArea.Visible = true;
			PlayEffect(effect);
			StartBreathe();
			return;
		}

		// Different character — full rebuild
		_currentPortraitChar = charName;
		StopBreathe();

		foreach (var child in _portraitArea.GetChildren())
			child.QueueFree();

		var rect = new TextureRect();
		rect.Texture = tex;
		rect.SetAnchorsPreset(LayoutPreset.FullRect);
		rect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		rect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		_portraitArea.AddChild(rect);
		_activeRect = rect;
		_portraitArea.Visible = true;

		PlayEntry(entry);
		PlayEffect(effect);
	}

	// ── Entry animations ──

	void PlayEntry(string entry)
	{
		if (_activeRect == null) return;
		float vw = GetViewportRect().Size.X;
		float vh = GetViewportRect().Size.Y;
		var tween = CreateTween();

		switch (entry)
		{
			case "slide_left":
				_activeRect.OffsetLeft = vw;
				tween.TweenProperty(_activeRect, "offset_left", 0f, 0.5f)
					.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
				break;
			case "slide_right":
				_activeRect.OffsetLeft = -vw;
				tween.TweenProperty(_activeRect, "offset_left", 0f, 0.5f)
					.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
				break;
			case "slide_top":
				_activeRect.OffsetTop = -vh;
				tween.TweenProperty(_activeRect, "offset_top", 0f, 0.5f)
					.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
				break;
			case "fade_in":
				_activeRect.Modulate = new Color(1, 1, 1, 0);
				tween.TweenProperty(_activeRect, "modulate:a", 1f, 0.4f)
					.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
				break;
			default: // slide_bottom
				_activeRect.OffsetTop = vh;
				tween.TweenProperty(_activeRect, "offset_top", 0f, 0.5f)
					.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
				break;
		}
		tween.TweenCallback(Callable.From(StartBreathe));
	}

	// ── Continuous breathe ──

	void StartBreathe()
	{
		if (_activeRect == null || !_portraitArea.Visible) return;
		StopBreathe();
		_breatheTween = CreateTween();
		_breatheTween.SetLoops();
		_breatheTween.TweenProperty(_activeRect, "offset_top", 6f, 1.3f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		_breatheTween.TweenProperty(_activeRect, "offset_top", -6f, 1.3f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
	}

	void StopBreathe()
	{
		if (_breatheTween != null && _breatheTween.IsValid())
			_breatheTween.Kill();
		_breatheTween = null;
	}

	// ── One-shot expression effects ──

	void PlayEffect(string effect)
	{
		if (_activeRect == null || string.IsNullOrEmpty(effect)) return;

		StopBreathe();
		var tween = CreateTween();

		switch (effect)
		{
			case "shake":
			{
				tween.TweenInterval(0.5f);
				float amp = 12f;
				for (int i = 0; i < 8; i++)
				{
					float x = (float)GD.RandRange(-amp, amp);
					tween.TweenProperty(_activeRect, "offset_left", x, 0.04f);
					tween.TweenProperty(_activeRect, "offset_right", x, 0.04f);
					amp *= 0.65f;
				}
				tween.TweenProperty(_activeRect, "offset_left", 0f, 0.04f);
				tween.TweenProperty(_activeRect, "offset_right", 0f, 0.04f);
				break;
			}
			case "tilt":
				tween.TweenProperty(_activeRect, "rotation", Mathf.DegToRad(3f), 0.1f)
					.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
				tween.TweenProperty(_activeRect, "rotation", Mathf.DegToRad(-2f), 0.1f)
					.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.InOut);
				tween.TweenProperty(_activeRect, "rotation", 0f, 0.1f)
					.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
				break;
			case "flash":
				_activeRect.Modulate = Colors.White;
				tween.TweenProperty(_activeRect, "modulate", Colors.White, 0.1f);
				tween.TweenProperty(_activeRect, "modulate", new Color(1, 1, 1, 1), 0.15f)
					.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
				break;
			case "zoom_in":
				tween.TweenProperty(_activeRect, "scale", new Vector2(1.05f, 1.05f), 0.1f)
					.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
				tween.TweenProperty(_activeRect, "scale", Vector2.One, 0.15f)
					.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
				break;
		}
	}

	// ── Exit and dismiss ──

	void HidePortrait()
	{
		if (_portraitArea == null || !_portraitArea.Visible || _activeRect == null) return;
		StopBreathe();

		float vh = GetViewportRect().Size.Y;
		var tween = CreateTween();
		tween.TweenProperty(_activeRect, "offset_top", vh, 0.3f)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.In);
		tween.TweenCallback(Callable.From(() =>
		{
			_portraitArea.Visible = false;
			_activeRect = null;
			if (IsInsideTree()) QueueFree();
		}));
	}

	public override void _Input(InputEvent e)
	{
		if (!Visible || _dismissing) return;
		if (e is InputEventMouseButton mb && mb.Pressed) Dismiss();
		if (e is InputEventKey k && k.Pressed && (k.Keycode == Key.Enter || k.Keycode == Key.Space)) Dismiss();
	}

	void Dismiss()
	{
		_dismissing = true;
		MouseFilter = MouseFilterEnum.Ignore;
		if (_portraitArea != null && _portraitArea.Visible && _activeRect != null)
			HidePortrait();
		else
			QueueFree();
	}

	public bool IsShowing => Visible;
}
