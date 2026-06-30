namespace No1.UI;

using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// CanvasLayer overlay for selecting a world seed before entering the world.
/// Provides seed input, random generation, override file picker, and seed history.
/// Emits WorldSeedConfirmed signal when the player confirms.
/// </summary>
public partial class SeedSelector : CanvasLayer
{
	// ── Signal ──────────────────────────────────────────────────────
	[Signal]
	public delegate void WorldSeedConfirmedEventHandler(ulong seed, string[] overridePaths);

	// ── Properties ──────────────────────────────────────────────────
	public ulong SelectedSeed { get; private set; }
	public string[] SelectedOverridePaths { get; private set; } = System.Array.Empty<string>();
	public List<ulong> RecentSeeds { get; set; } = new();

	// ── Constants ───────────────────────────────────────────────────
	const int MaxRecentSeeds = 5;
	const int MaxSeedDigits = 20;
	const string OverrideDir = "res://assets/data/world_overrides/";
	const string SeedHistoryPath = "user://seed_history.cfg";

	// ── UI nodes ────────────────────────────────────────────────────
	Panel _panel;
	LineEdit _seedInput;
	ItemList _overrideList;
	OptionButton _recentDropdown;

	// ── Lifecycle ───────────────────────────────────────────────────

	public override void _Ready()
	{
		BuildUI();
		LoadRecentSeeds();
		_panel.Visible = false;
	}

	// ── UI construction ─────────────────────────────────────────────

	void BuildUI()
	{
		// ── Panel background ──
		_panel = new Panel
		{
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			AnchorTop = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -210,
			OffsetRight = 210,
			OffsetTop = -190,
			OffsetBottom = 190
		};

		// Dark semi-transparent panel style
		var panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.04f, 0.04f, 0.08f, 0.92f),
			BorderColor = new Color(0.25f, 0.25f, 0.45f, 0.8f),
			BorderWidthLeft = 2,
			BorderWidthRight = 2,
			BorderWidthTop = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ContentMarginLeft = 14,
			ContentMarginRight = 14,
			ContentMarginTop = 10,
			ContentMarginBottom = 10
		};
		_panel.AddThemeStyleboxOverride("panel", panelStyle);
		AddChild(_panel);

		// ── VBoxContainer root ──
		var vbox = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.Fill,
			SizeFlagsVertical = Control.SizeFlags.Fill,
			AnchorLeft = 0,
			AnchorRight = 1,
			AnchorTop = 0,
			AnchorBottom = 1
		};
		_panel.AddChild(vbox);

		// ── Title ──
		var title = new Label
		{
			Text = "选择世界种子",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.7f));
		title.AddThemeFontSizeOverride("font_size", 18);
		vbox.AddChild(title);

		vbox.AddChild(MakeHSeparator());

		// ── Seed input row ──
		var seedRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.Fill };
		var seedLabel = new Label
		{
			Text = "种子:",
			CustomMinimumSize = new Vector2(56, 0),
			VerticalAlignment = VerticalAlignment.Center
		};
		seedLabel.AddThemeColorOverride("font_color", Colors.White);
		seedRow.AddChild(seedLabel);

		_seedInput = new LineEdit
		{
			PlaceholderText = "输入数字或留空随机",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MaxLength = MaxSeedDigits + 1 // allow one extra for the filter to trim
		};
		_seedInput.AddThemeColorOverride("font_color", Colors.White);
		_seedInput.TextChanged += OnSeedTextChanged;
		seedRow.AddChild(_seedInput);
		vbox.AddChild(seedRow);

		// ── Recent seeds dropdown ──
		var recentRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.Fill };
		var recentLabel = new Label
		{
			Text = "历史:",
			CustomMinimumSize = new Vector2(56, 0),
			VerticalAlignment = VerticalAlignment.Center
		};
		recentLabel.AddThemeColorOverride("font_color", Colors.White);
		recentRow.AddChild(recentLabel);

		_recentDropdown = new OptionButton
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_recentDropdown.AddThemeColorOverride("font_color", Colors.White);
		_recentDropdown.ItemSelected += OnRecentSeedSelected;
		recentRow.AddChild(_recentDropdown);
		vbox.AddChild(recentRow);

		// ── Action buttons row ──
		var btnRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.Fill };
		btnRow.AddThemeConstantOverride("separation", 10);

		var randomBtn = new Button
		{
			Text = "随机种子",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(100, 32)
		};
		randomBtn.AddThemeColorOverride("font_color", new Color(0.7f, 1.0f, 0.7f));
		randomBtn.Pressed += OnRandomPressed;
		btnRow.AddChild(randomBtn);

		var confirmBtn = new Button
		{
			Text = "确认进入",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(100, 32)
		};
		confirmBtn.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.5f));
		confirmBtn.Pressed += OnConfirmPressed;
		btnRow.AddChild(confirmBtn);
		vbox.AddChild(btnRow);

		vbox.AddChild(MakeHSeparator());

		// ── Override file section ──
		var overrideLabel = new Label
		{
			Text = "覆盖文件:"
		};
		overrideLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.85f));
		vbox.AddChild(overrideLabel);

		_overrideList = new ItemList
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			SelectMode = ItemList.SelectModeEnum.Multi,
			AllowSearch = false,
			CustomMinimumSize = new Vector2(0, 100)
		};
		_overrideList.AddThemeColorOverride("font_color", Colors.White);
		vbox.AddChild(_overrideList);

		// ── Populate overrides ──
		PopulateOverrides();
	}

	// ── Seed input validation ───────────────────────────────────────

	void OnSeedTextChanged(string newText)
	{
		if (string.IsNullOrEmpty(newText)) return;

		var filtered = new string(newText.Where(char.IsDigit).ToArray());
		if (filtered.Length > MaxSeedDigits)
			filtered = filtered[..MaxSeedDigits];

		if (filtered != newText)
			_seedInput.Text = filtered;
	}

	// ── Button handlers ─────────────────────────────────────────────

	void OnRandomPressed()
	{
		ulong seed = (ulong)Random.Shared.NextInt64();
		_seedInput.Text = seed.ToString();
		GD.Print($"[SeedSelector] Random seed generated: {seed}");
	}

	void OnConfirmPressed()
	{
		// Parse seed — empty text means random
		string text = _seedInput.Text.Trim();
		ulong seed;
		if (string.IsNullOrEmpty(text))
		{
			seed = (ulong)Random.Shared.NextInt64();
		}
		else if (!ulong.TryParse(text, out seed))
		{
			seed = (ulong)Random.Shared.NextInt64();
		}

		SelectedSeed = seed;

		// Collect selected override file names
		var selectedOverrides = new List<string>();
		foreach (int idx in _overrideList.GetSelectedItems())
			selectedOverrides.Add(_overrideList.GetItemText(idx));
		SelectedOverridePaths = selectedOverrides.ToArray();

		// Persist seed in history
		SaveRecentSeed(seed);

		// Emit signal
		GD.Print($"[SeedSelector] Confirmed: seed={seed} overrides=[{string.Join(", ", SelectedOverridePaths)}]");
		EmitSignal(SignalName.WorldSeedConfirmed, seed, SelectedOverridePaths);

		Hide();
	}

	// ── Override file population ────────────────────────────────────

	void PopulateOverrides()
	{
		_overrideList.Clear();
		using var dir = DirAccess.Open(OverrideDir);
		if (dir == null)
		{
			GD.Print($"[SeedSelector] Override directory not found: {OverrideDir}");
			return;
		}

		var err = dir.ListDirBegin();
		if (err != Error.Ok) return;

		string fileName = dir.GetNext();
		while (!string.IsNullOrEmpty(fileName))
		{
			if (!dir.CurrentIsDir() && fileName.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
				_overrideList.AddItem(fileName);

			fileName = dir.GetNext();
		}
		dir.ListDirEnd();

		GD.Print($"[SeedSelector] Loaded {_overrideList.ItemCount} override files from {OverrideDir}");
	}

	// ── Recent seeds ────────────────────────────────────────────────

	void OnRecentSeedSelected(long index)
	{
		if (index < 0 || index >= RecentSeeds.Count) return;
		_seedInput.Text = RecentSeeds[(int)index].ToString();
		_recentDropdown.Select(-1);
	}

	void RefreshRecentDropdown()
	{
		_recentDropdown.Clear();
		_recentDropdown.AddItem("— 选择历史种子 —");
		_recentDropdown.SetItemDisabled(0, true);
		foreach (var seed in RecentSeeds)
			_recentDropdown.AddItem(seed.ToString());
	}

	void LoadRecentSeeds()
	{
		RecentSeeds.Clear();
		var config = new ConfigFile();
		var err = config.Load(SeedHistoryPath);
		if (err != Error.Ok) return;

		for (int i = 0; i < MaxRecentSeeds; i++)
		{
			var key = $"seed_{i}";
			var val = config.GetValue("recent", key, "").AsString();
			if (!string.IsNullOrEmpty(val) && ulong.TryParse(val, out var seed))
				RecentSeeds.Add(seed);
		}

		RefreshRecentDropdown();
		GD.Print($"[SeedSelector] Loaded {RecentSeeds.Count} recent seeds");
	}

	void SaveRecentSeed(ulong seed)
	{
		// Remove duplicates and prepend
		RecentSeeds.Remove(seed);
		RecentSeeds.Insert(0, seed);

		// Cap
		if (RecentSeeds.Count > MaxRecentSeeds)
			RecentSeeds = RecentSeeds.Take(MaxRecentSeeds).ToList();

		// Persist
		var config = new ConfigFile();
		for (int i = 0; i < RecentSeeds.Count; i++)
			config.SetValue("recent", $"seed_{i}", RecentSeeds[i].ToString());
		config.Save(SeedHistoryPath);

		RefreshRecentDropdown();
		GD.Print($"[SeedSelector] Saved recent seed {seed} (total: {RecentSeeds.Count})");
	}

	// ── Show / Hide ─────────────────────────────────────────────────

	public new void Show()
	{
		_seedInput.Clear();
		_panel.Visible = true;
		_seedInput.GrabFocus();
		GD.Print("[SeedSelector] Panel shown");
	}

	public new void Hide()
	{
		_panel.Visible = false;
		GD.Print("[SeedSelector] Panel hidden");
	}

	// ── Helpers ─────────────────────────────────────────────────────

	static HSeparator MakeHSeparator()
	{
		var sep = new HSeparator
		{
			CustomMinimumSize = new Vector2(0, 4)
		};
		return sep;
	}
}
