// ── Debug Console — in-game overlay for debug commands ──
// Toggle: ~ / ` (backtick, Key.QuoteLeft)
// Usage: Registered as Autoload in project.godot
namespace No1.Debug;

using Godot;
using No1.Core;

public partial class DebugConsole : Control
{
	// ── Singleton ──
	public static DebugConsole Instance { get; private set; } = null!;

	// ── State ──
	public static bool IsOpen { get; private set; }

	// ── UI nodes (built in _Ready) ──
	CanvasLayer _canvasLayer = null!;
	Panel _panel = null!;
	VBoxContainer _vbox = null!;
	RichTextLabel _outputLog = null!;
	LineEdit _inputLine = null!;

	// ── Output history ──
	const int MaxOutputLines = 100;
	readonly List<string> _outputLines = new();

	// ── Tween reference ──
	Tween _activeTween = null!;

	// ── Mouse mode before opening ──
	Input.MouseModeEnum _previousMouseMode;

	public override void _Ready()
	{
		Instance = this;
		_previousMouseMode = Input.MouseMode;

		BuildUI();
		HidePanelInstant();
		IsOpen = false;
	}

	public override void _Notification(int what)
	{
		if (what == NotificationWMCloseRequest)
			CycleManager.Instance?.SaveAccount();
	}

	// ────────────────────────────────────────
	//  UI Construction
	// ────────────────────────────────────────

	void BuildUI()
	{
		// CanvasLayer — high layer so it renders above everything
		_canvasLayer = new CanvasLayer { Layer = 100 };
		AddChild(_canvasLayer);

		// Panel — bottom half of screen
		_panel = new Panel
		{
			AnchorLeft = 0f,
			AnchorRight = 1f,
			AnchorTop = 0.5f,
			AnchorBottom = 1f,
			MouseFilter = MouseFilterEnum.Stop,
		};

		var bgStyle = new StyleBoxFlat();
		bgStyle.BgColor = new Color("#1a1a1a");
		_panel.AddThemeStyleboxOverride("panel", bgStyle);

		_canvasLayer.AddChild(_panel);

		// VBoxContainer — stack output + input
		_vbox = new VBoxContainer
		{
			AnchorLeft = 0f,
			AnchorRight = 1f,
			AnchorTop = 0f,
			AnchorBottom = 1f,
			OffsetLeft = 8,
			OffsetRight = -8,
			OffsetTop = 8,
			OffsetBottom = -8,
		};
		_panel.AddChild(_vbox);

		// RichTextLabel — scrollable output log
		_outputLog = new RichTextLabel
		{
			Name = "_outputLog",
			BbcodeEnabled = true,
			ScrollFollowing = true,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		_outputLog.AddThemeFontSizeOverride("normal_font_size", 14);
		_outputLog.AddThemeColorOverride("default_color", new Color("#50ff50"));
		_vbox.AddChild(_outputLog);

		// LineEdit — command input
		_inputLine = new LineEdit
		{
			Name = "_inputLine",
			PlaceholderText = "> Enter command...",
		};
		_inputLine.AddThemeFontSizeOverride("font_size", 14);
		_inputLine.TextSubmitted += OnTextSubmitted;
		_vbox.AddChild(_inputLine);
	}

	// ────────────────────────────────────────
	//  Input Handling
	// ────────────────────────────────────────

	public override void _Input(InputEvent e)
	{
		if (e is InputEventKey key && key.Pressed && !key.Echo)
		{
			// Toggle: ` / ~ OR Escape — always works regardless of LineEdit focus
			if (key.Keycode == Key.Quoteleft || (IsOpen && key.Keycode == Key.Escape))
			{
				Toggle();
				GetViewport().SetInputAsHandled();
			}
		}
	}

	public override void _Process(double delta)
	{
		if (IsOpen && _inputLine != null && !_inputLine.HasFocus())
			_inputLine.GrabFocus();
	}

	// ────────────────────────────────────────
	//  Toggle
	// ────────────────────────────────────────

	void Toggle()
	{
		if (IsOpen)
			ClosePanel();
		else
			OpenPanel();
	}

	void OpenPanel()
	{
		if (IsOpen) return;

		// Release mouse so player can type
		_previousMouseMode = Input.MouseMode;
		Input.MouseMode = Input.MouseModeEnum.Visible;

		IsOpen = true;
		_panel.Visible = true;
		KillTween();

		_activeTween = CreateTween();
		_activeTween.SetParallel(true);
		_activeTween.TweenProperty(_panel, "position:y", 0f, 0.15f);
		_activeTween.TweenProperty(_panel, "modulate:a", 1f, 0.15f);

		// Give focus ASAP so typing works immediately
		_inputLine.CallDeferred("grab_focus");
	}

	void ClosePanel()
	{
		if (!IsOpen) return;

		_inputLine.Clear();
		Input.MouseMode = _previousMouseMode;
		IsOpen = false;
		KillTween();

		_activeTween = CreateTween();
		_activeTween.SetParallel(true);
		_activeTween.TweenProperty(_panel, "position:y", 50f, 0.1f);
		_activeTween.TweenProperty(_panel, "modulate:a", 0f, 0.1f);

		_activeTween.Finished += () => _panel.Visible = false;
	}

	void HidePanelInstant()
	{
		_panel.Position = new Vector2(0f, 50f);
		_panel.Modulate = new Color(1f, 1f, 1f, 0f);
		_panel.Visible = false;
	}

	void KillTween()
	{
		_activeTween?.Kill();
		_activeTween = null;
	}

	// ────────────────────────────────────────
	//  Command Execution
	// ────────────────────────────────────────

	void OnTextSubmitted(string text)
	{
		var trimmed = text.Trim();
		if (string.IsNullOrEmpty(trimmed)) return;

		// Echo the command
		OnOutput($"> {trimmed}");
		_inputLine.Clear();

		// Execute via DebugCommands
		DebugCommands.Execute(trimmed, OnOutput);
	}

	// ────────────────────────────────────────
	//  Output
	// ────────────────────────────────────────

	void OnOutput(string text)
	{
		// Format the line
		string formatted;
		if (text.StartsWith("> "))
			formatted = $"[color=#80ff80]{text}[/color]";   // brighter green for echo
		else if (text.Contains("[color=", StringComparison.OrdinalIgnoreCase))
			formatted = text;                                // already has bbcode
		else
			formatted = $"[color=#50ff50]{text}[/color]";   // normal green

		_outputLines.Add(formatted);

		// Trim to max lines
		while (_outputLines.Count > MaxOutputLines)
			_outputLines.RemoveAt(0);

		// Rebuild log
		_outputLog.Text = string.Join("\n", _outputLines);
	}
}
