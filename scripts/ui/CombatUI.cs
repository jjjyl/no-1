namespace No1.UI;

using System.Collections.Generic;
using System.Linq;
using Godot;
using No1.Combat;
using No1.Core;
using No1.Data;

public enum CombatResult { Win, Lose, Escape }

public partial class CombatUI : Control
{
	[Export] RichTextLabel _playerStat, _playerName, _battleLog, _turnLabel, _resultLabel;
	[Export] ProgressBar _pBruiseBar, _pSevereBar, _pGaugeBar, _pEnergyBar;
	[Export] Button _atkBtn, _defBtn, _dodgeBtn, _escapeBtn, _itemBtn, _continueBtn, _templeBtn;
	[Export] Control _playerActions, _playerStatPanel, _dialoguePanel;
	[Export] RichTextLabel _dialogueSpeaker, _dialogueText;
	[Export] Button _dialogueNext;
	[Export] Container _skillRow;
	[Export] Container _companionSection;
	[Export] Container _enemyList;
	[Export] int EnemyGroupSize = 2;

	CharacterStats _player;
	float _pGauge;
	const float GaugeMax = 100f;
	float _escapeGauge;
	ProgressBar _escapeGaugeBar;
	[Export] float EscapeThreshold = 300f;
	[Export] float GaugeMultiplier = 10f;

	// ── Scene paths ──
	public const string SceneCombat = "res://scenes/combat/combat.tscn";
	public const string SceneTemple = "res://scenes/temple/temple_3d.tscn";

	string _queuedAction = "atk";
	int _defBonus, _dodgeBonus;
	bool _waiting, _done, _blocking;
	int _round = 1;

	Label _pBruiseLabel, _pSevereLabel;
	Label _pEnergyLabel;
	SkillDef _queuedSkill;
	List<CompSlot> _compSlots = new();
	CompSlot _activeCompanion;
	List<EnemySlot> _enemies = new();
	bool _targeting;
	SkillDef _targetSkill;
	CompSlot _targetActor;
	string _targetItemId;
	CompSlot _activeItemUser;
	bool _givingMode;

	class CompSlot
	{
		public CompanionState State;
		public CharacterStats Stats;
		public float Gauge;
		public Label NameLabel;
		public ProgressBar GaugeBar;
		public ProgressBar BruiseBar, SevereBar;
		public Label BruiseLabel, SevereLabel;
		public ProgressBar EnergyBar;
		public Label EnergyLabel;
		public Container SkillsArea;
		public RichTextLabel StatLabel;
		public RichTextLabel StatusLabel;
		public PanelContainer Card;
		public int DefBonus, DodgeBonus;
	}

	class EnemySlot
	{
		public CharacterStats Stats;
		public float Gauge;
		public bool Alive;
		public string DisplayName;
		public Label NameLabel;
		public ProgressBar BruiseBar, SevereBar;
		public Label BruiseLabel, SevereLabel;
		public ProgressBar GaugeBar, EnergyBar;
		public Label EnergyLabel;
		public RichTextLabel StatLabel;
		public RichTextLabel StatusLabel;
		public PanelContainer Card;
	}

	public override void _Ready()
	{
		_resultLabel.Visible = _continueBtn.Visible = _templeBtn.Visible = false;

		AddBarLabel(_pBruiseBar, out _pBruiseLabel);
		AddBarLabel(_pSevereBar, out _pSevereLabel);
		AddBarLabel(_pEnergyBar, out _pEnergyLabel);

		_continueBtn.Pressed += () =>
		{
			CycleManager.Instance.SkipStartEvents = true;
			GameManager.Instance.GoToScene(GameManager.SceneWorld);
		};
		_templeBtn.Pressed += () =>
		{
			CycleManager.Instance.ReturnToTemple();
			GameManager.Instance.GoToScene(GameManager.SceneTemple);
		};
		_atkBtn.Pressed += () => QueueAction("atk");
		_atkBtn.Icon = Icon("attack"); _atkBtn.ExpandIcon = true;
		_defBtn.Pressed += () => QueueAction("def");
		_defBtn.Icon = Icon("defend");  _defBtn.ExpandIcon = true;
		_dodgeBtn.Pressed += () => QueueAction("dodge");
		_dodgeBtn.Icon = Icon("dodge"); _dodgeBtn.ExpandIcon = true;
		_escapeBtn.Icon = Icon("dodge"); _escapeBtn.ExpandIcon = true;
		_escapeBtn.Pressed += () => TryEscape();
		_escapeBtn.Disabled = true;
		_escapeGauge = 0;

		_escapeGaugeBar = new ProgressBar { ShowPercentage = false };
		_escapeGaugeBar.AnchorRight = 1; _escapeGaugeBar.AnchorBottom = 1;
		_escapeGaugeBar.MouseFilter = MouseFilterEnum.Ignore;
		_escapeGaugeBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_escapeGaugeBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(0.15f, 0.5f, 0.15f, 0.35f) });
		_escapeGaugeBar.AddThemeStyleboxOverride("background", new StyleBoxEmpty());
		_escapeBtn.AddChild(_escapeGaugeBar);
		_escapeBtn.MoveChild(_escapeGaugeBar, 0);
		_itemBtn.Pressed += () => ShowItemMenu();
		_itemBtn.Text = " 物品";
		_itemBtn.Icon = Icon("heal");
		_itemBtn.Disabled = true;

		_playerName.MouseFilter = MouseFilterEnum.Stop;
		_playerName.GuiInput += e =>
		{
			if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
			{
				if (_targeting && !string.IsNullOrEmpty(_targetItemId))
				{
					if (_activeItemUser != null)
					{
						if (_givingMode)
							ConfirmCompanionGiveTarget(_targetItemId, _activeItemUser, null);
						else
							ConfirmCompanionItemUse(null);
					}
					else if (_givingMode)
					{
						// Player can't give to self — ignore click
					}
					else
						ConfirmItemTarget(_player);
				}
				else if (_targeting && _targetSkill != null && _targetSkill.Type == "heal" && _targetActor != null)
					ConfirmHealOnPlayer();
				else
					_playerStatPanel.Visible = !_playerStatPanel.Visible;
			}
		};

		_turnLabel.Text = "";
		_turnLabel.CustomMinimumSize = Vector2.Zero;

		_player = CycleManager.Instance.PlayerStats;
		if (_player != null)
			_player.Statuses ??= new StatusTracker(_player, "玩家");
		_pGauge = 0;

		if (_dialogueNext != null)
			_dialogueNext.Pressed += () =>
			{
				_dialoguePanel.Visible = false;
				_blocking = false;
				CombatEvents.ProcessNext();
			};

		CombatEvents.Init(CycleManager.Instance.PendingBattleEvents, this);

		BuildEnemyCards();
		BuildCompanionSlots();
		BuildSkillButtons();
		RefreshAll();

		CombatEvents.Fire("on_battle_start", new FireContext());
	}

	void BuildEnemyCards()
	{
		string enemyId = CycleManager.Instance.PendingEnemyScene;
		var def = EnemyState.Get(enemyId);
		if (def == null) def = EnemyState.Get("base_mob");

		for (int i = 0; i < EnemyGroupSize; i++)
		{
			var st = def.SpawnStats(CycleManager.Instance?.CurrentCycle ?? 1);
			st.DisplayName = def.IsElite ? $"精英{def.Name}{i + 1}" : $"{def.Name}{i + 1}";
			st.FullHeal();
			st.Statuses = new StatusTracker(st, st.DisplayName);

			var slot = new EnemySlot
			{
				Stats = st,
				Alive = true,
				DisplayName = st.DisplayName,
			};

			var card = new PanelContainer();
			slot.Card = card;
			var inner = new VBoxContainer();
			inner.AddThemeConstantOverride("separation", 2);

			var topRow = new HBoxContainer();
			topRow.AddThemeConstantOverride("separation", 4);
			slot.NameLabel = new Label();
			slot.NameLabel.MouseFilter = MouseFilterEnum.Stop;
			topRow.AddChild(slot.NameLabel);
			slot.GaugeBar = MakeGaugeBar();
			topRow.AddChild(slot.GaugeBar);
			inner.AddChild(topRow);

			var barRow = new HBoxContainer();
			barRow.Alignment = BoxContainer.AlignmentMode.Center;
			barRow.AddThemeConstantOverride("separation", 4);
			slot.BruiseBar = MakeHpBar();
			slot.SevereBar = MakeHpBar();
			barRow.AddChild(slot.BruiseBar);
			barRow.AddChild(slot.SevereBar);
			inner.AddChild(barRow);
			AddBarLabel(slot.BruiseBar, out slot.BruiseLabel);
			AddBarLabel(slot.SevereBar, out slot.SevereLabel);

			var gaugeRow = new HBoxContainer();
			gaugeRow.Alignment = BoxContainer.AlignmentMode.Center;
			gaugeRow.AddThemeConstantOverride("separation", 4);
			var energyLabelHead = new Label { Text = "精" };
			gaugeRow.AddChild(energyLabelHead);
			slot.EnergyBar = MakeEnergyBar();
			gaugeRow.AddChild(slot.EnergyBar);
			slot.EnergyLabel = new Label();
			gaugeRow.AddChild(slot.EnergyLabel);
			inner.AddChild(gaugeRow);

			slot.StatLabel = new RichTextLabel
			{
				BbcodeEnabled = true,
				FitContent = true,
				MouseFilter = MouseFilterEnum.Ignore,
				Visible = false,
			};
			inner.AddChild(slot.StatLabel);

			slot.StatusLabel = new RichTextLabel { BbcodeEnabled = true, FitContent = true };
			inner.AddChild(slot.StatusLabel);

			card.AddChild(inner);
			_enemyList.AddChild(card);

			slot.NameLabel.GuiInput += e =>
			{
				if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
				{
					if (_targeting && _targetSkill != null && _targetSkill.Type == "attack" && slot.Alive)
						ConfirmTarget(slot);
					else
						slot.StatLabel.Visible = !slot.StatLabel.Visible;
				}
			};
			card.GuiInput += e =>
			{
if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } && _targeting && slot.Alive && _targetSkill != null && _targetSkill.Type == "attack")
				{
					ConfirmTarget(slot);
					GetViewport().SetInputAsHandled();
				}
			};

		_enemies.Add(slot);
	}
}

	static Texture2D Icon(string name) => GD.Load<Texture2D>($"res://assets/ui/icons/{name}.png");

	static Texture2D SkillIcon(SkillDef skill)
	{
		string path = skill.Type switch
		{
			"attack" when skill.DamageType == "spirit" => "magic_slash",
			"attack" => "slash",
			"heal" => "heal",
			_ => "slash"
		};
		return Icon(path);
	}

	static ProgressBar MakeGaugeBar()
	{
		var bg = new StyleBoxFlat { BgColor = new Color(0.13f, 0.13f, 0.13f), CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3 };
		var bar = new ProgressBar
		{
			MaxValue = 100,
			ShowPercentage = false,
			SizeFlagsHorizontal = SizeFlags.Fill,
			CustomMinimumSize = new(120, 14)
		};
		bar.AddThemeStyleboxOverride("background", bg);
		return bar;
	}

	static ProgressBar MakeHpBar()
	{
		var bg = new StyleBoxFlat { BgColor = new Color(0.13f, 0.13f, 0.13f), CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3 };
		var bar = new ProgressBar
		{
			ShowPercentage = false,
			SizeFlagsHorizontal = SizeFlags.Fill,
			CustomMinimumSize = new(100, 22)
		};
		bar.AddThemeStyleboxOverride("background", bg);
		return bar;
	}

	static ProgressBar MakeEnergyBar()
	{
		var bg = new StyleBoxFlat { BgColor = new Color(0.13f, 0.13f, 0.13f), CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3 };
		var bar = new ProgressBar
		{
			ShowPercentage = false,
			SizeFlagsHorizontal = SizeFlags.Fill,
			CustomMinimumSize = new(80, 16)
		};
		bar.AddThemeStyleboxOverride("background", bg);
		return bar;
	}

	static void AddBarLabel(ProgressBar bar, out Label lbl)
	{
		bar.ShowPercentage = false;
		lbl = new Label();
		lbl.HorizontalAlignment = HorizontalAlignment.Center;
		lbl.VerticalAlignment = VerticalAlignment.Center;
		lbl.MouseFilter = Control.MouseFilterEnum.Ignore;
		lbl.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		bar.AddChild(lbl);
	}

	void BuildCompanionSlots()
	{
		foreach (var comp in CycleManager.Instance.ActiveCompanions)
		{
			var st = comp.SpawnStats();
			st.Statuses = new StatusTracker(st, comp.Name);
			var slot = new CompSlot { State = comp, Stats = st };

			var outer = new VBoxContainer();
			outer.AddThemeConstantOverride("separation", 2);

			var topRow = new HBoxContainer();
			topRow.AddThemeConstantOverride("separation", 4);
			slot.NameLabel = new Label();
			slot.NameLabel.MouseFilter = MouseFilterEnum.Stop;
			topRow.AddChild(slot.NameLabel);
			slot.GaugeBar = MakeGaugeBar();
			topRow.AddChild(slot.GaugeBar);
			outer.AddChild(topRow);

			var barRow = new HBoxContainer();
			barRow.Alignment = BoxContainer.AlignmentMode.Center;
			barRow.AddThemeConstantOverride("separation", 4);
			slot.BruiseBar = MakeHpBar();
			slot.SevereBar = MakeHpBar();
			barRow.AddChild(slot.BruiseBar);
			barRow.AddChild(slot.SevereBar);
			outer.AddChild(barRow);
			AddBarLabel(slot.BruiseBar, out slot.BruiseLabel);
			AddBarLabel(slot.SevereBar, out slot.SevereLabel);

			var energyRow = new HBoxContainer();
			energyRow.Alignment = BoxContainer.AlignmentMode.Center;
			energyRow.AddThemeConstantOverride("separation", 4);
			energyRow.AddChild(new Label { Text = "精" });
			slot.EnergyBar = MakeEnergyBar();
			energyRow.AddChild(slot.EnergyBar);
			slot.EnergyLabel = new Label();
			energyRow.AddChild(slot.EnergyLabel);
			outer.AddChild(energyRow);

			slot.StatLabel = new RichTextLabel
			{
				BbcodeEnabled = true,
				FitContent = true,
				MouseFilter = MouseFilterEnum.Ignore,
				Visible = false,
			};
			outer.AddChild(slot.StatLabel);

			slot.StatusLabel = new RichTextLabel { BbcodeEnabled = true, FitContent = true };
			outer.AddChild(slot.StatusLabel);

			slot.SkillsArea = new VBoxContainer { Visible = false };
			slot.SkillsArea.AddThemeConstantOverride("separation", 2);
			var actLabel = new RichTextLabel { BbcodeEnabled = true, FitContent = true };
			slot.SkillsArea.AddChild(actLabel);
			outer.AddChild(slot.SkillsArea);

			var card = new PanelContainer();
			slot.Card = card;
			card.AddChild(outer);
			_companionSection.AddChild(card);

			slot.NameLabel.GuiInput += e =>
			{
				if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
				{
					if (_targeting && !string.IsNullOrEmpty(_targetItemId) && slot.State.Alive)
					{
						if (_activeItemUser != null)
						{
							if (_givingMode)
								ConfirmCompanionGiveTarget(_targetItemId, _activeItemUser, slot);
							else
								ConfirmCompanionItemUse(slot);
						}
						else if (_givingMode)
							DoPlayerGift(_targetItemId, slot);
						else
							ConfirmItemTarget(slot.Stats);
					}
					else if (_targeting && _targetSkill != null && _targetSkill.Type == "heal" && slot.State.Alive)
						ConfirmAllyTarget(slot);
					else
						slot.StatLabel.Visible = !slot.StatLabel.Visible;
				}
			};

			card.GuiInput += e =>
			{
				if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
				{
					if (_targeting && !string.IsNullOrEmpty(_targetItemId) && slot.State.Alive)
					{
						if (_activeItemUser != null)
						{
							if (_givingMode)
								ConfirmCompanionGiveTarget(_targetItemId, _activeItemUser, slot);
							else
								ConfirmCompanionItemUse(slot);
						}
						else if (_givingMode)
							DoPlayerGift(_targetItemId, slot);
						else
							ConfirmItemTarget(slot.Stats);
						GetViewport().SetInputAsHandled();
					}
					else if (_targeting && _targetSkill != null && _targetSkill.Type == "heal" && slot.State.Alive)
					{
						ConfirmAllyTarget(slot);
						GetViewport().SetInputAsHandled();
					}
				}
			};

			_compSlots.Add(slot);
		}
	}

	void BuildSkillButtons()
	{
		var skills = SkillData.For("player");
		foreach (var skill in skills)
		{
			if (skill.Type == "buff") continue;
			var btn = new Button();
			btn.Text = skill.Cost > 0 ? $" {skill.Name}({skill.Cost})" : $" {skill.Name}";
			btn.CustomMinimumSize = new Vector2(80, 28);
			btn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			btn.Icon = SkillIcon(skill); btn.ExpandIcon = true;
			if (skill.DamageType == "spirit")
				btn.AddThemeColorOverride("font_color", new Color(0.6f, 0.7f, 1.0f));
			var captured = skill;
			btn.Pressed += () => QueueSkill(captured);
			_skillRow.AddChild(btn);
		}
	}

	public override void _Process(double delta)
	{
		if (_done || _player == null) return;
		if (_blocking) return;
		if (CombatEvents.HasPending()) { CombatEvents.ProcessNext(); return; }
		float dt = (float)delta;

		// Escape gauge fills only when game is NOT waiting for player input
		if (!_waiting && !_targeting && !_done)
		{
			_escapeGauge += _player.Speed * GaugeMultiplier * 5f * dt; // 10x for testing
			if (_escapeGauge >= EscapeThreshold)
			{
				_escapeGauge = EscapeThreshold;
				_escapeBtn.Disabled = false;
			}
			if (_escapeGaugeBar != null) _escapeGaugeBar.Value = _escapeGauge / EscapeThreshold * 100;
		}

		if (!_waiting && !_targeting)
		{
			_pGauge += _player.Speed * GaugeMultiplier * dt;
			foreach (var s in _compSlots)
				if (s.State.Alive) s.Gauge += s.Stats.Speed * GaugeMultiplier * dt;
			foreach (var e in _enemies)
				if (e.Alive) e.Gauge += e.Stats.Speed * GaugeMultiplier * dt;

			if (_pGauge >= GaugeMax)
			{
				_pGauge = GaugeMax;
				if (!_player.CanAct) ForceSkipPlayer();
				else { _waiting = true; CombatEvents.Fire("on_turn_start", new FireContext { Source = "player", Round = _round }); }
			}
			if (!_waiting)
			{
				foreach (var s in _compSlots)
				{
					if (s.Gauge >= GaugeMax && s.State.Alive)
					{
						s.Gauge -= GaugeMax;
						if (!s.Stats.CanAct)
						{
							s.Stats.Energy += 3;
							Log($"『{s.State.Name}』精力不足，无法行动", "#888888");
						}
						else
						{
							_activeCompanion = s;
							_waiting = true;
							CombatEvents.Fire("on_turn_start", new FireContext { Source = s.State.Name, Round = _round });
							ShowCompanionActions();
							break;
						}
					}
				}
			}
			if (!_waiting)
			{
				foreach (var e in _enemies)
				{
				if (e.Gauge >= GaugeMax && e.Alive)
				{
					e.Gauge -= GaugeMax;
					if (!e.Stats.CanAct)
					{
						e.Stats.Energy += 3;
						Log($"{e.DisplayName}陷入恍惚……", "#888888");
					}
					else
					{
						CombatEvents.Fire("on_turn_start", new FireContext { Source = e.DisplayName, Round = _round });
						EnemyAct(e);
						EndCharacterTurn(e.Stats.Statuses, e.DisplayName);
						_round++;
						CombatEvents.Fire("on_round", new FireContext { Round = _round });
					}
					break;
				}
				}
			}
		}

#if DEBUG
		HandleDebugKeys();
#endif
		UpdateDisplay();
	}

#if DEBUG
	float _debugKeyCooldown;

	void HandleDebugKeys()
	{
		_debugKeyCooldown -= (float)GetProcessDeltaTime();
		if (_debugKeyCooldown > 0) return;

		var tracker = _player?.Statuses;
		var enemyTracker = _enemies.FirstOrDefault(e => e.Alive)?.Stats?.Statuses;

		if (Input.IsKeyPressed(Key.F1))
		{
			_debugKeyCooldown = 0.5f;
			tracker?.Apply("fear", 3, "DEBUG-F1");
			Log("[DEBUG] 玩家获得 恐惧(3回合)", "#ffcc44");
			RefreshAll();
		}
		else if (Input.IsKeyPressed(Key.F2))
		{
			_debugKeyCooldown = 0.5f;
			tracker?.Apply("burning", 3, "DEBUG-F2");
			Log("[DEBUG] 玩家获得 灼烧(3回合)", "#ffcc44");
			RefreshAll();
		}
		else if (Input.IsKeyPressed(Key.F3))
		{
			_debugKeyCooldown = 0.5f;
			tracker?.Apply("def_up", 2, "DEBUG-F3");
			Log("[DEBUG] 玩家获得 防御提升(2回合)", "#ffcc44");
			RefreshAll();
		}
		else if (Input.IsKeyPressed(Key.F4))
		{
			_debugKeyCooldown = 0.5f;
			enemyTracker?.Apply("enrage", 3, "DEBUG-F4");
			Log("[DEBUG] 敌人获得 狂怒(3回合)", "#ffcc44");
			RefreshAll();
		}
	}
#endif

	void ForceSkipPlayer()
	{
		_pGauge = 0;
		_player.Energy += 3;
		Log("精力不足，无法行动", "#888888");
	}

	void ShowCompanionActions()
	{
		var comp = _activeCompanion;
		var children = comp.SkillsArea.GetChildren();
		for (int i = children.Count - 1; i >= 1; i--)
			children[i].QueueFree();

		var basicRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		basicRow.AddThemeConstantOverride("separation", 2);

		var atkBtn = new Button();
		atkBtn.Text = " 攻击";
		atkBtn.CustomMinimumSize = new Vector2(80, 28);
		atkBtn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		atkBtn.Icon = Icon("attack"); atkBtn.ExpandIcon = true;
		atkBtn.Pressed += () => BeginTargeting(new SkillDef { Type = "attack", DamageType = "physical", Power = 1.0f, Cost = 0, Name = "攻击" }, comp);
		basicRow.AddChild(atkBtn);

		var defBtn = new Button();
		defBtn.Text = " 防御";
		defBtn.CustomMinimumSize = new Vector2(80, 28);
		defBtn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		defBtn.Icon = Icon("defend"); defBtn.ExpandIcon = true;
		defBtn.Pressed += () => ExecuteCompanionSkill(comp, new SkillDef { Type = "def", Cost = 0, Name = "防御" });
		basicRow.AddChild(defBtn);

		var dodBtn = new Button();
		dodBtn.Text = " 闪避";
		dodBtn.CustomMinimumSize = new Vector2(80, 28);
		dodBtn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		dodBtn.Icon = Icon("dodge"); dodBtn.ExpandIcon = true;
		dodBtn.Pressed += () => ExecuteCompanionSkill(comp, new SkillDef { Type = "dodge", Cost = 0, Name = "闪避" });
		basicRow.AddChild(dodBtn);

		var hasItems = comp.State.Inventory != null && comp.State.Inventory.GetAllItemIds().Any();
		var itemBtn = new Button();
		itemBtn.Text = " 物品";
		itemBtn.CustomMinimumSize = new Vector2(80, 28);
		itemBtn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		itemBtn.Icon = Icon("heal"); itemBtn.ExpandIcon = true;
		if (hasItems)
			itemBtn.Pressed += () => ShowCompanionItemMenu(comp);
		else
			itemBtn.Disabled = true;
		basicRow.AddChild(itemBtn);

		comp.SkillsArea.AddChild(basicRow);

		var skillRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		skillRow.AddThemeConstantOverride("separation", 2);
		var skills = SkillData.For(comp.State.Name);
		foreach (var skill in skills)
		{
			if (skill.Type == "buff") continue;
			var btn = new Button();
			btn.Text = skill.Cost > 0 ? $" {skill.Name}({skill.Cost})" : $" {skill.Name}";
			btn.CustomMinimumSize = new Vector2(80, 28);
			btn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			btn.Icon = SkillIcon(skill); btn.ExpandIcon = true;
			var captured = skill;
			if (skill.Type == "attack")
				btn.Pressed += () => BeginTargeting(captured, comp);
			else if (skill.Type == "heal" && skill.Target != "self")
				btn.Pressed += () => BeginTargeting(captured, comp);
			else
				btn.Pressed += () => ExecuteCompanionSkill(comp, captured);
			skillRow.AddChild(btn);
		}
		comp.SkillsArea.AddChild(skillRow);

		var actLabel = comp.SkillsArea.GetChild<RichTextLabel>(0);
		actLabel.Text = $"[b][color=#88aacc]{comp.State.Name}的回合[/color][/b] 羁绊Lv.{comp.State.Favor}";
		comp.SkillsArea.Visible = true;
	}

	void CancelTargeting()
	{
		if (!_targeting) return;
		_targeting = false;
		_targetSkill = null;
		_targetActor = null;
		_targetItemId = null;
		_activeItemUser = null;
		_givingMode = false;
		_playerName.RemoveThemeColorOverride("font_color");
		foreach (var e in _enemies) e.Card.RemoveThemeColorOverride("panel");
		foreach (var s in _compSlots) s.Card.RemoveThemeColorOverride("panel");
	}

	void BeginTargeting(SkillDef skill, CompSlot actor)
	{
		CancelTargeting();
		_targeting = true;
		_targetSkill = skill;
		_targetActor = actor;
		actor.SkillsArea.Visible = false;
		if (skill.Type == "attack")
		{
			foreach (var e in _enemies)
				if (e.Alive)
					e.Card.AddThemeColorOverride("panel", new Color(0.5f, 0.3f, 0.3f));
		}
		else if (skill.Type == "heal")
		{
			_playerName.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 0.3f));
			foreach (var s in _compSlots)
				if (s.State.Alive && s != actor)
					s.Card.AddThemeColorOverride("panel", new Color(0.3f, 0.5f, 0.3f));
		}
	}

	void BeginPlayerTargeting(SkillDef skill)
	{
		CancelTargeting();
		_targeting = true;
		_targetSkill = skill;
		_targetActor = null;
		foreach (var e in _enemies)
			if (e.Alive)
				e.Card.AddThemeColorOverride("panel", new Color(0.5f, 0.3f, 0.3f));
	}

	void ConfirmTarget(EnemySlot e)
	{
		_targeting = false;
		foreach (var en in _enemies)
			en.Card.RemoveThemeColorOverride("panel");

		var skill = _targetSkill;
		if (_targetActor != null)
		{
			if (_targetActor.Stats.Energy < skill.Cost)
			{
				Log($"『{_targetActor.State.Name}』精力不足", "#888888");
				_targetActor.SkillsArea.Visible = false;
				_activeCompanion = null;
				_targeting = false;
				foreach (var en in _enemies) en.Card.RemoveThemeColorOverride("panel");
				return;
			}
			_targetActor.Stats.ApplyModifier("def_flat", -_targetActor.DefBonus);
			_targetActor.DefBonus = 0;
			_targetActor.Stats.ApplyModifier("dodge", -_targetActor.DodgeBonus);
			_targetActor.DodgeBonus = 0;

			bool isSpirit = skill.DamageType == "spirit";
			int baseStat = isSpirit ? _targetActor.Stats.Heart : _targetActor.Stats.ATK;
			int raw = _targetActor.Stats.DealDamage(skill.Power, isSpirit);
			int d = e.Stats.TakeDamage(raw, isSpirit);
			_targetActor.Stats.Energy -= skill.Cost;
			string tag = isSpirit ? "精神" : "物理";
			Log($"『{_targetActor.State.Name}』{skill.Name}({tag}, {skill.Power:F1}×{baseStat}), 对{e.DisplayName}造成 [b]{d}[/b] 伤害", "#88aacc");
			CombatEvents.Fire("on_skill_used", new FireContext { Source = _targetActor.State.Name, Target = e.DisplayName, SkillId = skill.Id, Damage = d, Round = _round });
			CombatEvents.Fire("on_damage_dealt", new FireContext { Source = _targetActor.State.Name, Target = e.DisplayName, Damage = d, Round = _round });

			_targetActor.Stats.Energy = Math.Min(_targetActor.Stats.Energy + _targetActor.Stats.EnergyRegen, _targetActor.Stats.EnergyMax);
			RefreshAll();
			_waiting = false;
			_targetActor.SkillsArea.Visible = false;
			_activeCompanion = null;
			EndCharacterTurn(_targetActor.Stats.Statuses, _targetActor.State.Name);
			if (!e.Stats.IsDead) e.Alive = true;
			else { e.Alive = false; Log($"[color=red]{e.DisplayName}倒下了！[/color]", "#ff8888"); CombatEvents.Fire("on_enemy_defeated", new FireContext { Source = _targetActor.State.Name, Target = e.DisplayName, Round = _round }); }
			CheckAllDead();
		}
		else
		{
			if (_player.Energy < skill.Cost)
			{
				Log($"精力不足，无法使用『{skill.Name}』", "#888888");
				_pGauge += GaugeMax;
				_targeting = false;
				foreach (var en in _enemies) en.Card.RemoveThemeColorOverride("panel");
				return;
			}
			_player.ApplyModifier("def_flat", -_defBonus);
			_defBonus = 0;
			_player.ApplyModifier("dodge", -_dodgeBonus);
			_dodgeBonus = 0;

			bool isSpirit = skill.DamageType == "spirit";
			int baseStat = isSpirit ? _player.Heart : _player.ATK;
			int raw = _player.DealDamage(skill.Power, isSpirit);
			int d = e.Stats.TakeDamage(raw, isSpirit);
			_player.Energy -= skill.Cost;
			string tag = isSpirit ? "精神" : "物理";
			Log($"你使用『{skill.Name}』({tag}, {skill.Power:F1}×{baseStat}), 对{e.DisplayName}造成 [b]{d}[/b] 伤害, 精力-{skill.Cost}", "#aa88ff");
			CombatEvents.Fire("on_skill_used", new FireContext { Source = "player", Target = e.DisplayName, SkillId = skill.Id, Damage = d, Round = _round });
			CombatEvents.Fire("on_damage_dealt", new FireContext { Source = "player", Target = e.DisplayName, Damage = d, Round = _round });

			_player.Energy = Math.Min(_player.Energy + _player.EnergyRegen, _player.EnergyMax);
			RefreshAll();
			_waiting = false;
			_playerActions.Visible = false;
			EndCharacterTurn(_player?.Statuses, "玩家");
			if (!e.Stats.IsDead) e.Alive = true;
			else { e.Alive = false; Log($"[color=red]{e.DisplayName}倒下了！[/color]", "#ff8888"); CombatEvents.Fire("on_enemy_defeated", new FireContext { Source = "player", Target = e.DisplayName, Round = _round }); }
			CheckAllDead();
		}
	}

	void ConfirmAllyTarget(CompSlot target)
	{
		_targeting = false;
		foreach (var s in _compSlots) s.Card.RemoveThemeColorOverride("panel");

		var actor = _targetActor;
		if (actor == null) return;
		var skill = _targetSkill;

		if (actor.Stats.Energy < skill.Cost)
		{
			Log($"『{actor.State.Name}』精力不足", "#888888");
			actor.SkillsArea.Visible = false;
			_activeCompanion = null;
			return;
		}

		int heal = Mathf.RoundToInt(skill.Power * actor.Stats.Heart);
		target.Stats.BruiseHP = Math.Min(target.Stats.BruiseHP + heal, target.Stats.MaxBruiseHP);
		actor.Stats.Energy -= skill.Cost;
		Log($"『{actor.State.Name}』{skill.Name}, 恢复『{target.State.Name}』[b]{heal}[/b] 擦伤", "#88aacc");
		CombatEvents.Fire("on_skill_used", new FireContext { Source = actor.State.Name, Target = target.State.Name, SkillId = skill.Id, Round = _round });

		actor.Stats.Energy = Math.Min(actor.Stats.Energy + actor.Stats.EnergyRegen, actor.Stats.EnergyMax);
		RefreshAll();
		_waiting = false;
		actor.SkillsArea.Visible = false;
		_activeCompanion = null;
		EndCharacterTurn(actor.Stats.Statuses, actor.State.Name);
	}

	void ConfirmHealOnPlayer()
	{
		_playerName.RemoveThemeColorOverride("font_color");
		_targeting = false;
		var actor = _targetActor;
		if (actor == null) return;
		var skill = _targetSkill;

		if (actor.Stats.Energy < skill.Cost)
		{
			Log($"『{actor.State.Name}』精力不足", "#888888");
			actor.SkillsArea.Visible = false;
			_activeCompanion = null;
			return;
		}

		int heal = Mathf.RoundToInt(skill.Power * actor.Stats.Heart);
		_player.BruiseHP = Math.Min(_player.BruiseHP + heal, _player.MaxBruiseHP);
		actor.Stats.Energy -= skill.Cost;
		Log($"『{actor.State.Name}』{skill.Name}, 恢复你 [b]{heal}[/b] 擦伤", "#88aacc");
		CombatEvents.Fire("on_skill_used", new FireContext { Source = actor.State.Name, Target = "player", SkillId = skill.Id, Round = _round });

		actor.Stats.Energy = Math.Min(actor.Stats.Energy + actor.Stats.EnergyRegen, actor.Stats.EnergyMax);
		RefreshAll();
		_waiting = false;
		actor.SkillsArea.Visible = false;
		_activeCompanion = null;
		EndCharacterTurn(actor.Stats.Statuses, actor.State.Name);
	}

	void CheckAllDead()
	{
		if (_enemies.All(e => !e.Alive)) Win();
	}

	void ExecuteCompanionSkill(CompSlot comp, SkillDef skill)
	{
		CancelTargeting();
		_waiting = false;
		_targeting = false;
		foreach (var en in _enemies) en.Card.RemoveThemeColorOverride("panel");
		foreach (var s in _compSlots) s.Card.RemoveThemeColorOverride("panel");
		if (comp.Stats.Energy < skill.Cost)
		{
			Log($"『{comp.State.Name}』精力不足", "#888888");
			_activeCompanion = null;
			comp.SkillsArea.Visible = false;
			return;
		}

		comp.Stats.ApplyModifier("def_flat", -comp.DefBonus);
		comp.DefBonus = 0;
		comp.Stats.ApplyModifier("dodge", -comp.DodgeBonus);
		comp.DodgeBonus = 0;

		if (skill.Type == "heal")
		{
			CharacterStats target;
			if (skill.Target == "self")
				target = comp.Stats;
			else if (skill.Target == "ally_lowest")
			{
				var cs = new List<CharacterStats> { _player };
				foreach (var s in _compSlots)
					if (s.State.Alive && s != comp)
						cs.Add(s.Stats);
				target = cs.OrderBy(st => (float)st.BruiseHP / st.MaxBruiseHP).First();
			}
			else
				target = _player;

			int heal = Mathf.RoundToInt(skill.Power * comp.Stats.Heart);
			target.BruiseHP = Math.Min(target.BruiseHP + heal, target.MaxBruiseHP);
			comp.Stats.Energy -= skill.Cost;

			string tn = target == _player ? "你" : "自己";
			if (target != comp.Stats && target != _player)
				tn = _compSlots.FirstOrDefault(s => s.Stats == target)?.State.Name ?? "同伴";
			Log($"『{comp.State.Name}』{skill.Name}, 恢复{tn} [b]{heal}[/b] 擦伤", "#88aacc");
		}
		else if (skill.Type == "def")
		{
			comp.DefBonus = 4;
			comp.Stats.ApplyModifier("def_flat", 4);
			Log($"『{comp.State.Name}』防御，物理减伤提升", "#88aacc");
		}
		else if (skill.Type == "dodge")
		{
			comp.DodgeBonus = 30;
			comp.Stats.ApplyModifier("dodge", 30);
			Log($"『{comp.State.Name}』集中闪避，闪避提升", "#88aacc");
		}

		comp.Stats.Energy = Math.Min(comp.Stats.Energy + comp.Stats.EnergyRegen, comp.Stats.EnergyMax);
		RefreshAll();
		_activeCompanion = null;
		comp.SkillsArea.Visible = false;
		EndCharacterTurn(comp.Stats.Statuses, comp.State.Name);
	}

	void QueueAction(string action)
	{
		_queuedAction = action;
		_queuedSkill = null;
		if (!_waiting) return;
		if (action == "atk")
		{
			_pGauge -= GaugeMax;
			BeginPlayerTargeting(new SkillDef { Type = "attack", DamageType = "physical", Power = 1.0f, Cost = 0, Name = "攻击" });
		}
		else
		{
			_waiting = false;
			_targeting = false;
			foreach (var en in _enemies) en.Card.RemoveThemeColorOverride("panel");
			_pGauge -= GaugeMax;
			PlayerAct();
		}
	}

	void QueueSkill(SkillDef skill)
	{
		_queuedSkill = skill;
		_queuedAction = "skill";
		if (!_waiting) return;
		if (skill.Type == "attack")
		{
			_pGauge -= GaugeMax;
			BeginPlayerTargeting(skill);
		}
		else
		{
			_waiting = false;
			_targeting = false;
			foreach (var en in _enemies) en.Card.RemoveThemeColorOverride("panel");
			_pGauge -= GaugeMax;
			PlayerAct();
		}
	}

	void PlayerAct()
	{
		CancelTargeting();
		_player.ApplyModifier("def_flat", -_defBonus);
		_defBonus = 0;
		_player.ApplyModifier("dodge", -_dodgeBonus);
		_dodgeBonus = 0;

		switch (_queuedAction)
		{
			case "def":
				_defBonus = 4;
				_player.ApplyModifier("def_flat", 4);
				Log("你防御，物理减伤提升", "#88ccff");
				break;
			case "dodge":
				_dodgeBonus = 30;
				_player.ApplyModifier("dodge", 30);
				Log("你集中闪避，闪避提升", "#88ccff");
				break;
			case "skill":
				ExecuteSkill();
				break;
		}
		_player.Energy = Math.Min(_player.Energy + _player.EnergyRegen, _player.EnergyMax);
		RefreshAll();
		_playerActions.Visible = false;
		EndCharacterTurn(_player?.Statuses, "玩家");
	}

	void TryEscape()
	{
		if (_escapeGauge < EscapeThreshold) return;
		_escapeGauge = 0;
		_escapeBtn.Disabled = true;
		if (_escapeGaugeBar != null) _escapeGaugeBar.Value = 0;

		int chance = 40 + _player.Agility * 2;
		if ((int)(GD.Randi() % 100) < chance)
		{
			Log("你成功逃脱了！", "#ffcc44");
			EndCombat(CombatResult.Escape);
		}
		else
		{
			Log("逃跑失败！", "#888888");
		}
		RefreshAll();
	}

	void ExecuteSkill()
	{
		var skill = _queuedSkill;
		if (skill == null) return;

		if (_player.Energy < skill.Cost)
		{
			Log($"精力不足，无法使用『{skill.Name}』", "#888888");
			_pGauge += GaugeMax;
			return;
		}

		if (skill.Type == "heal")
		{
			int heal = Mathf.RoundToInt(skill.Power * _player.Heart);
			_player.BruiseHP = Math.Min(_player.BruiseHP + heal, _player.MaxBruiseHP);
			_player.Energy -= skill.Cost;
			Log($"你使用『{skill.Name}』, 恢复 [b]{heal}[/b] 擦伤, 精力-{skill.Cost}", "#88ff88");
		}
	}

	void EnemyAct(EnemySlot e)
	{
		if (!e.Alive) return;
		var targets = new List<CharacterStats> { _player };
		foreach (var s in _compSlots) if (s.State.Alive) targets.Add(s.Stats);
		var t = targets[(int)(GD.Randi() % (uint)targets.Count)];
		int d = t.TakeDamage(e.Stats.DealDamage(1.0f));
		var hit = _compSlots.FirstOrDefault(s => s.Stats == t);
		Log($"{e.DisplayName}攻击{(t == _player ? "你" : hit?.State.Name)}，造成 [b]{d}[/b] 伤害", "#ff8888");
		e.Stats.Energy = Math.Min(e.Stats.Energy + e.Stats.EnergyRegen, e.Stats.EnergyMax);

		foreach (var s in _compSlots)
			if (s.State.Alive && s.Stats.IsDead) { s.State.Alive = false; Log($"[color=red]{s.State.Name}倒下了！[/color]", "#ff8888"); }

		RefreshAll();
		if (_player.IsDead) Lose();
	}

	void ShowItemMenu()
	{
		var inv = CycleManager.Instance.PlayerInventory;
		if (inv == null) return;

		var scene = GD.Load<PackedScene>("res://scenes/ui/combat_item_menu.tscn");
		if (scene == null) return;
		var node = scene.Instantiate();
		var menu = node as CombatItemMenu;
		if (menu == null) { node.QueueFree(); return; }

		menu.SourceInventory = inv;
		menu.OnItemSelected = (itemId) => BeginItemTargeting(itemId);
		menu.OnItemGiven = (itemId) =>
		{
			_givingMode = true;
			BeginPlayerGiveTargeting(itemId);
			menu.QueueFree();
		};
		AddChild(menu);
	}

	void BeginItemTargeting(string itemId)
	{
		CancelTargeting();
		_targeting = true;
		_targetItemId = itemId;

		_playerName.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 0.3f));
		foreach (var s in _compSlots)
			if (s.State.Alive)
				s.Card.AddThemeColorOverride("panel", new Color(0.3f, 0.5f, 0.3f));
	}

	void ConfirmItemTarget(CharacterStats target)
	{
		var itemId = _targetItemId;
		CancelTargeting();
		var inv = CycleManager.Instance.PlayerInventory;
		if (inv == null || string.IsNullOrEmpty(itemId)) return;

		var def = ItemDef.Get(itemId);
		// Always use live stats from CycleManager so heals apply to the current player object
		var liveTarget = (target == _player) ? CycleManager.Instance.PlayerStats : target;
		var result = inv.UseItemOn(itemId, liveTarget);

		if (result != null)
		{
			Log($"使用失败: {result}", "#ff8888");
			return;
		}

		string targetName = target == _player ? "自己" :
			_compSlots.FirstOrDefault(s => s.Stats == target)?.State.Name ?? "同伴";
		Log($"对{targetName}使用了 [b]{def?.Name ?? "物品"}[/b]", "#88ff88");

		_pGauge -= GaugeMax;
		_player.Energy = Math.Min(_player.Energy + _player.EnergyRegen, _player.EnergyMax);
		RefreshAll();
		_waiting = false;
		_playerActions.Visible = false;
	}

	void ShowCompanionItemMenu(CompSlot comp)
	{
		var scene = GD.Load<PackedScene>("res://scenes/ui/combat_item_menu.tscn");
		if (scene == null) return;
		var node = scene.Instantiate();
		var menu = node as CombatItemMenu;
		if (menu == null) { node.QueueFree(); return; }

		menu.SourceInventory = comp.State.Inventory;
		menu.OnItemSelected = (itemId) =>
		{
			_givingMode = false;
			BeginCompanionItemTargeting(itemId, comp);
		};
		menu.OnItemGiven = (itemId) =>
		{
			_givingMode = true;
			BeginCompanionGiveTargeting(itemId, comp);
			menu.QueueFree();
		};
		AddChild(menu);
	}

	void BeginCompanionItemTargeting(string itemId, CompSlot fromComp)
	{
		CancelTargeting();
		_targeting = true;
		_targetItemId = itemId;
		_activeItemUser = fromComp;

		_playerName.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 0.3f));
		foreach (var s in _compSlots)
			if (s.State.Alive && s != fromComp)
				s.Card.AddThemeColorOverride("panel", new Color(0.3f, 0.5f, 0.3f));
	}

	void ConfirmCompanionItemUse(CompSlot target)
	{
		var itemId = _targetItemId;
		var fromComp = _activeItemUser;
		if (fromComp == null) return;

		CancelTargeting();

		var liveTarget = (target == null) ? _player : target.Stats;
		var def = ItemDef.Get(itemId);
		var result = fromComp.State.Inventory.UseItemOn(itemId, liveTarget);

		if (result != null)
		{
			Log($"使用失败: {result}", "#ff8888");
			fromComp.SkillsArea.Visible = false;
			_activeCompanion = null;
			_waiting = false;
			return;
		}

		if (target != null)
			target.State.SyncFromStats(target.Stats);

		string targetName = target == null ? "你" : target.State.Name;
		Log($"『{fromComp.State.Name}』对{targetName}使用了 [b]{def?.Name ?? "物品"}[/b]", "#88aacc");

		fromComp.Stats.Energy = Math.Min(fromComp.Stats.Energy + fromComp.Stats.EnergyRegen, fromComp.Stats.EnergyMax);
		RefreshAll();
		_waiting = false;
		_activeItemUser = null;
		fromComp.SkillsArea.Visible = false;
		_activeCompanion = null;
		EndCharacterTurn(fromComp.Stats.Statuses, fromComp.State.Name);
	}

	void BeginCompanionGiveTargeting(string itemId, CompSlot fromComp)
	{
		CancelTargeting();
		_targeting = true;
		_targetItemId = itemId;
		_activeItemUser = fromComp;

		_playerName.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 0.3f));
		foreach (var s in _compSlots)
			if (s.State.Alive && s != fromComp)
				s.Card.AddThemeColorOverride("panel", new Color(0.3f, 0.5f, 0.3f));
	}

	void BeginPlayerGiveTargeting(string itemId)
	{
		CancelTargeting();
		_targeting = true;
		_targetItemId = itemId;
		_activeItemUser = null; // null means player is the giver

		// Highlight companions only (NOT player, NOT enemies)
		foreach (var s in _compSlots)
			if (s.State.Alive)
				s.Card.AddThemeColorOverride("panel", new Color(0.3f, 0.5f, 0.3f));
	}

	void ConfirmCompanionGiveTarget(string itemId, CompSlot fromComp, CompSlot toComp)
	{
		CancelTargeting();

		string recipientName = toComp?.State.Name ?? "玩家";
		string giverName = fromComp.State.Name;
		int maxGive = Math.Min(fromComp.State.Inventory.GetCount(itemId), 3);

		var popup = new Panel();
		popup.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.1f, 0.1f, 0.15f, 0.95f), CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 });
		popup.SetAnchorsPreset(Control.LayoutPreset.Center);
		popup.CustomMinimumSize = new Vector2(260, 120);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 8);
		vbox.AnchorRight = 1;
		vbox.AnchorBottom = 1;
		vbox.OffsetLeft = 12;
		vbox.OffsetTop = 12;
		vbox.OffsetRight = -12;
		vbox.OffsetBottom = -12;

		var label = new Label { Text = $"给 {recipientName} 几个？", HorizontalAlignment = HorizontalAlignment.Center };
		vbox.AddChild(label);

		var spinBox = new SpinBox();
		spinBox.MinValue = 1;
		spinBox.MaxValue = maxGive;
		spinBox.Value = 1;
		spinBox.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		vbox.AddChild(spinBox);

		var hbox = new HBoxContainer();
		hbox.Alignment = BoxContainer.AlignmentMode.Center;
		hbox.AddThemeConstantOverride("separation", 8);

		var confirmBtn = new Button { Text = "确认" };
		confirmBtn.Pressed += () =>
		{
			DoCompanionGift(itemId, fromComp, toComp, (int)spinBox.Value);
			popup.QueueFree();
		};
		hbox.AddChild(confirmBtn);

		var cancelBtn = new Button { Text = "取消" };
		cancelBtn.Pressed += () =>
		{
			popup.QueueFree();
			fromComp.SkillsArea.Visible = true;
		};
		hbox.AddChild(cancelBtn);

		vbox.AddChild(hbox);
		popup.AddChild(vbox);
		AddChild(popup);
	}

	void DoCompanionGift(string itemId, CompSlot fromComp, CompSlot toComp, int amount)
	{
		if (fromComp == null) return;

		var targetInv = toComp?.State.Inventory ?? CycleManager.Instance.PlayerInventory;
		var result = fromComp.State.Inventory.TransferTo(targetInv, itemId, amount);

		if (result != null)
		{
			Log($"给予失败: {result}", "#ff8888");
			fromComp.SkillsArea.Visible = false;
			_activeCompanion = null;
			_waiting = false;
			return;
		}

		string recipientName = toComp?.State.Name ?? "玩家";
		var def = ItemDef.Get(itemId);
		Log($"『{fromComp.State.Name}』将 {def?.Name ?? "物品"} ×{amount} 交给了『{recipientName}』", "#88aacc");

		fromComp.Stats.Energy = Math.Min(fromComp.Stats.Energy + fromComp.Stats.EnergyRegen, fromComp.Stats.EnergyMax);
		RefreshAll();
		_waiting = false;
		_activeItemUser = null;
		fromComp.SkillsArea.Visible = false;
		_activeCompanion = null;
		EndCharacterTurn(fromComp.Stats.Statuses, fromComp.State.Name);
	}

	void DoPlayerGift(string itemId, CompSlot toComp)
	{
		CancelTargeting();
		var inv = CycleManager.Instance.PlayerInventory;
		if (inv == null || toComp?.State?.Inventory == null) return;

		int maxGive = Math.Min(inv.GetCount(itemId), 3);

		var popup = new Panel();
		popup.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.1f, 0.1f, 0.15f, 0.95f), CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 });
		popup.SetAnchorsPreset(Control.LayoutPreset.Center);
		popup.CustomMinimumSize = new Vector2(260, 120);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 8);
		vbox.AnchorRight = 1;
		vbox.AnchorBottom = 1;
		vbox.OffsetLeft = 12;
		vbox.OffsetTop = 12;
		vbox.OffsetRight = -12;
		vbox.OffsetBottom = -12;

		var label = new Label { Text = $"给 {toComp.State.Name} 几个？", HorizontalAlignment = HorizontalAlignment.Center };
		vbox.AddChild(label);

		var spinBox = new SpinBox();
		spinBox.MinValue = 1;
		spinBox.MaxValue = maxGive;
		spinBox.Value = 1;
		spinBox.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		vbox.AddChild(spinBox);

		var hbox = new HBoxContainer();
		hbox.Alignment = BoxContainer.AlignmentMode.Center;
		hbox.AddThemeConstantOverride("separation", 8);

		var confirmBtn = new Button { Text = "确认" };
		confirmBtn.Pressed += () =>
		{
			int amount = (int)spinBox.Value;
			var result = inv.TransferTo(toComp.State.Inventory, itemId, amount);
			if (result != null)
			{
				Log($"给予失败: {result}", "#ff8888");
			}
			else
			{
				var def = ItemDef.Get(itemId);
				Log($"将 [b]{def?.Name ?? itemId}[/b] ×{amount} 交给了『{toComp.State.Name}』", "#88ff88");
			}
			popup.QueueFree();

			// End player turn
			_pGauge -= GaugeMax;
			_player.Energy = Math.Min(_player.Energy + _player.EnergyRegen, _player.EnergyMax);
			RefreshAll();
			_waiting = false;
			_playerActions.Visible = false;
		};
		hbox.AddChild(confirmBtn);

		var cancelBtn = new Button { Text = "取消" };
		cancelBtn.Pressed += () => popup.QueueFree();
		hbox.AddChild(cancelBtn);

		vbox.AddChild(hbox);
		popup.AddChild(vbox);
		AddChild(popup);
	}

	void EndCombat(CombatResult result)
	{
		_done = true;
		_playerActions.Visible = false;

		// Save companion HP regardless of result
		foreach (var s in _compSlots)
		{
			s.State.BruiseHP = s.Stats.BruiseHP;
			s.State.SevereHP = s.Stats.SevereHP;
			if (!s.State.Alive) { s.State.BruiseHP = 0; s.State.SevereHP = 0; }
		}

		switch (result)
		{
			case CombatResult.Win:
				foreach (var s in _compSlots)
				{
					s.State.Favor++;
					s.State.HighestFavor = Math.Max(s.State.HighestFavor, s.State.Favor);
				}
				var drops = CombatLog.GetItems();
				var inv = CycleManager.Instance.PlayerInventory;
				if (inv != null)
					foreach (var kv in drops)
					{
						inv.AddItem(kv.Key, kv.Value);
						var itemDef = ItemDef.Get(kv.Key);
						Log($"获得了 [b]{itemDef?.Name ?? kv.Key}[/b] ×{kv.Value}", "#ffcc44");
					}
				CombatLog.Clear();
				_turnLabel.Text = "[center][b][color=green]胜利！[/color][/b][/center]";
				Log("击败了所有敌人！", "#ffcc44");
				_resultLabel.Text = "[center][b]胜利[/b][/center]";
				break;

			case CombatResult.Escape:
				_turnLabel.Text = "[center][b][color=#ffcc44]逃脱成功[/color][/b][/center]";
				_resultLabel.Text = "[center][b]逃脱[/b][/center]";
				break;

			case CombatResult.Lose:
			default:
				break;
		}

		_resultLabel.Visible = _continueBtn.Visible = _templeBtn.Visible = true;
	}

	void Win()
	{
		EndCombat(CombatResult.Win);
	}

	async void Lose()
	{
		EndCombat(CombatResult.Lose);
		_continueBtn.Visible = false; // can't continue after losing
		_turnLabel.Text = "[center][b][color=red]败北[/color][/b][/center]";
		Log("你倒下了……", "#ffcc44");
		_resultLabel.Text = "[center][b]你被击败了[/b][/center]";
		await ToSignal(GetTree().CreateTimer(1.5f), "timeout");
		CycleManager.Instance.OnDeath();
		GameManager.Instance.GoToScene(GameManager.SceneTemple);
	}

	void UpdateDisplay()
	{
		_pGaugeBar.Value = _pGauge;
		foreach (var s in _compSlots) s.GaugeBar.Value = s.Gauge;
		foreach (var e in _enemies) e.GaugeBar.Value = e.Gauge;

		_pEnergyBar.MaxValue = _player.EnergyMax; _pEnergyBar.Value = _player.Energy;
		_pEnergyLabel.Text = $"{_player.Energy}/{_player.EnergyMax}";
		foreach (var e in _enemies)
		{
			e.EnergyBar.MaxValue = e.Stats.EnergyMax; e.EnergyBar.Value = e.Stats.Energy;
			e.EnergyLabel.Text = $"{e.Stats.Energy}/{e.Stats.EnergyMax}";
		}

		_playerActions.Visible = _waiting && !_done && _activeCompanion == null;
		if (_itemBtn != null)
			_itemBtn.Disabled = !_waiting || _done || _activeCompanion != null;
		if (_activeCompanion != null)
			_activeCompanion.SkillsArea.Visible = true;

		string status = "";
		if (_done)
			status = "";
		else if (_targeting && !string.IsNullOrEmpty(_targetItemId))
			status = "[center][color=#88ff88][b]▸ 点击角色选择目标[/b][/color][/center]";
		else if (_targeting)
			status = _targetSkill != null && _targetSkill.Type == "heal"
				? "[center][color=#88ff88][b]▸ 点击同伴选择目标[/b][/color][/center]"
				: "[center][color=#ff8888][b]▸ 点击敌人选择目标[/b][/color][/center]";
		else if (_waiting && _activeCompanion != null)
			status = $"[center][color=#88aacc][b]▸ {_activeCompanion.State.Name}选择行动[/b][/color][/center]";
		else if (_waiting)
			status = "[center][color=yellow][b]▸ 请选择行动[/b][/color][/center]";
		else if (_player.Energy <= 0)
			status = "[center][color=gray]▸ 精力耗尽，恢复中[/color][/center]";
		else
			status = "[center]▸ 行动槽充能中[/center]";
		_turnLabel.Text = status;
	}

	public void Log(string m, string c) => _battleLog.AppendText($"[color={c}]{m}[/color]\n");

	void RefreshAll()
	{
		RefreshBars(_player, _pBruiseBar, _pBruiseLabel, _pSevereBar, _pSevereLabel);
		foreach (var s in _compSlots) RefreshCompSlot(s);
		foreach (var e in _enemies) RefreshEnemySlot(e);
		RefreshStatDisplay();
	}

	void RefreshEnemySlot(EnemySlot e)
	{
		var st = e.Stats;
		e.NameLabel.Text = e.Alive ? e.DisplayName : $"{e.DisplayName}(败)";
		e.NameLabel.Modulate = e.Alive ? new Color(1f, 0.4f, 0.4f) : Colors.Gray;
		e.BruiseBar.MaxValue = st.MaxBruiseHP; e.BruiseBar.Value = st.BruiseHP;
		e.SevereBar.MaxValue = st.MaxSevereHP; e.SevereBar.Value = st.SevereHP;
		e.BruiseLabel.Text = $"擦: {st.BruiseHP}/{st.MaxBruiseHP}";
		e.SevereLabel.Text = $"重: {st.SevereHP}/{st.MaxSevereHP}";
		e.StatLabel.Text = $"力{st.Power}体{st.Body}敏{st.Agility}心{st.Heart}运{st.Fortune}  "
			+ $"ATK{st.ATK} 物防{st.DefFlat} 精防{st.SpiritDef} 速{st.Speed}  "
			+ $"闪{st.Dodge:F0}% 暴{st.CritRate:F0}%";
		var statusLine = StatusText(st.Statuses);
		e.StatusLabel.Text = statusLine;
		e.StatusLabel.Visible = !string.IsNullOrEmpty(statusLine);
	}

	void RefreshCompSlot(CompSlot s)
	{
		var st = s.Stats;
		s.NameLabel.Text = $"{s.State.Name} 羁绊{s.State.Favor}";
		s.BruiseBar.MaxValue = st.MaxBruiseHP; s.BruiseBar.Value = st.BruiseHP;
		s.SevereBar.MaxValue = st.MaxSevereHP; s.SevereBar.Value = st.SevereHP;
		s.BruiseLabel.Text = $"擦: {st.BruiseHP}/{st.MaxBruiseHP}";
		s.SevereLabel.Text = $"重: {st.SevereHP}/{st.MaxSevereHP}";
		s.EnergyBar.MaxValue = st.EnergyMax; s.EnergyBar.Value = st.Energy;
		s.EnergyLabel.Text = $"{st.Energy}/{st.EnergyMax}";
		s.StatLabel.Text = $"力{st.Power}体{st.Body}敏{st.Agility}心{st.Heart}运{st.Fortune}  "
			+ $"ATK{st.ATK} 物防{st.DefFlat} 精防{st.SpiritDef} 速{st.Speed}  "
			+ $"闪{st.Dodge:F0}% 暴{st.CritRate:F0}%";
		var statusLine = StatusText(st.Statuses);
		s.StatusLabel.Text = statusLine;
		s.StatusLabel.Visible = !string.IsNullOrEmpty(statusLine);
	}

	void RefreshBars(CharacterStats st, ProgressBar bb, Label bl, ProgressBar sb, Label sl)
	{
		bb.MaxValue = st.MaxBruiseHP; bb.Value = st.BruiseHP;
		sb.MaxValue = st.MaxSevereHP; sb.Value = st.SevereHP;
		bl.Text = $"擦: {st.BruiseHP}/{st.MaxBruiseHP}";
		sl.Text = $"重: {st.SevereHP}/{st.MaxSevereHP}";
	}

	static string StatusText(StatusTracker tracker)
	{
		if (tracker == null || tracker.Count == 0) return "";
		var parts = new List<string>();
		foreach (var s in tracker.GetAllActive())
		{
			string dur = s.Duration < 0 ? "" : $"({s.Duration})";
			string color = s.Type == "debuff" ? "red" : s.Type == "buff" ? "cyan" : "yellow";
			string tick = s.TickDamage > 0 ? $"🔥" : "";
			parts.Add($"[color={color}][{s.Name}{dur}]{tick}[/color]");
		}
		return string.Join(" ", parts);
	}

	void RefreshStatDisplay()
	{
		_playerStat.Text = $"力{_player.Power}体{_player.Body}敏{_player.Agility}心{_player.Heart}运{_player.Fortune}  "
			+ $"ATK{_player.ATK} 物防{_player.DefFlat} 精防{_player.SpiritDef} 速{_player.Speed}  "
			+ $"闪{_player.Dodge:F0}% 暴{_player.CritRate:F0}%";
		var statusLine = StatusText(_player?.Statuses);
		if (!string.IsNullOrEmpty(statusLine))
			_playerStat.Text += $"\n{statusLine}";
	}

	public float GetHpPct(string target)
	{
		if (target == "player") return _player != null ? (float)_player.BruiseHP / _player.MaxBruiseHP : 1f;
		var c = _compSlots.FirstOrDefault(s => s.State.Name == target);
		if (c != null) return (float)c.Stats.BruiseHP / c.Stats.MaxBruiseHP;
		var e = _enemies.FirstOrDefault(en => en.DisplayName == target);
		if (e != null) return (float)e.Stats.BruiseHP / e.Stats.MaxBruiseHP;
		return 1f;
	}

	public int GetFavor(string target)
	{
		var c = _compSlots.FirstOrDefault(s => s.State.Name == target);
		return c?.State.Favor ?? 0;
	}

	public bool IsAlive(string target)
	{
		if (target == "player") return _player != null && !_player.IsDead;
		var c = _compSlots.FirstOrDefault(s => s.State.Name == target);
		if (c != null) return c.State.Alive;
		var e = _enemies.FirstOrDefault(en => en.DisplayName == target);
		return e?.Alive ?? false;
	}

	public int AliveEnemyCount() => _enemies.Count(e => e.Alive);

	public void ShowDialogue(string speaker, string text)
	{
		_dialogueSpeaker.Text = $"[b]{speaker}[/b]";
		_dialogueText.Text = text;
		_dialoguePanel.Visible = true;
		_blocking = true;
	}

	public void ForceAction(string actor, string skillId, string target)
	{
		if (actor == "player")
		{
			var skill = SkillData.For("player").FirstOrDefault(s => s.Id == skillId);
			if (skill == null) return;
			if (skill.Type == "heal")
			{
				int heal = Mathf.RoundToInt(skill.Power * _player.Heart);
				_player.BruiseHP = Math.Min(_player.BruiseHP + heal, _player.MaxBruiseHP);
				Log($"(事件) 你使用{skill.Name}, 恢复 [b]{heal}[/b] 擦伤", "#ffcc44");
			}
			RefreshAll();
			return;
		}

		var comp = _compSlots.FirstOrDefault(s => s.State.Name == actor);
		if (comp == null || !comp.State.Alive) return;
		var compSkill = SkillData.For(actor).FirstOrDefault(s => s.Id == skillId);
		if (compSkill == null) return;

		if (compSkill.Type == "attack")
		{
			var enemy = _enemies.FirstOrDefault(e => e.Alive);
			if (enemy == null) return;
			bool isSpirit = compSkill.DamageType == "spirit";
			int d = enemy.Stats.TakeDamage(comp.Stats.DealDamage(compSkill.Power, isSpirit));
			comp.Stats.Energy -= compSkill.Cost;
			Log($"(事件) 『{actor}』{compSkill.Name}, 对{enemy.DisplayName}造成 [b]{d}[/b] 伤害", "#ffcc44");
			if (enemy.Stats.IsDead) { enemy.Alive = false; Log($"[color=red]{enemy.DisplayName}倒下了！[/color]", "#ff8888"); }
		}
		else if (compSkill.Type == "heal")
		{
			CharacterStats healTarget;
			if (target == "player") healTarget = _player;
			else
			{
				var tc = _compSlots.FirstOrDefault(s => s.State.Name == target);
				healTarget = tc?.Stats ?? _player;
			}
			int heal = Mathf.RoundToInt(compSkill.Power * comp.Stats.Heart);
			healTarget.BruiseHP = Math.Min(healTarget.BruiseHP + heal, healTarget.MaxBruiseHP);
			comp.Stats.Energy -= compSkill.Cost;
			Log($"(事件) 『{actor}』{compSkill.Name}, 恢复 [b]{heal}[/b] 擦伤", "#ffcc44");
		}
		RefreshAll();
		CheckAllDead();
	}

	public void AddEnemies(string enemyId, int count)
	{
		var def = EnemyState.Get(enemyId);
		if (def == null) def = EnemyState.Get("base_mob");
		if (def == null) return;
		for (int i = 0; i < count; i++)
			AddEnemyFromState(def, $"{def.Name}{_enemies.Count + 1}");
		RefreshAll();
	}

	void AddEnemyFromState(EnemyState def, string name)
	{
		var st = def.SpawnStats(CycleManager.Instance?.CurrentCycle ?? 1);
		st.DisplayName = name;
		st.FullHeal();
		st.Statuses = new StatusTracker(st, name);

		var slot = new EnemySlot { Stats = st, Alive = true, DisplayName = name };
		var card = new PanelContainer();
		slot.Card = card;
		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 2);
		var topRow = new HBoxContainer();
		slot.NameLabel = new Label { MouseFilter = MouseFilterEnum.Stop };
		topRow.AddChild(slot.NameLabel);
		slot.GaugeBar = MakeGaugeBar();
		topRow.AddChild(slot.GaugeBar);
		inner.AddChild(topRow);
		var barRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		slot.BruiseBar = MakeHpBar(); slot.SevereBar = MakeHpBar();
		barRow.AddChild(slot.BruiseBar); barRow.AddChild(slot.SevereBar);
		inner.AddChild(barRow);
		AddBarLabel(slot.BruiseBar, out slot.BruiseLabel);
		AddBarLabel(slot.SevereBar, out slot.SevereLabel);
		var eRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		eRow.AddChild(new Label { Text = "精" });
		slot.EnergyBar = MakeEnergyBar();
		eRow.AddChild(slot.EnergyBar);
		slot.EnergyLabel = new Label();
		eRow.AddChild(slot.EnergyLabel);
		inner.AddChild(eRow);
		slot.StatLabel = new RichTextLabel { BbcodeEnabled = true, FitContent = true, Visible = false };
		inner.AddChild(slot.StatLabel);
		slot.StatusLabel = new RichTextLabel { BbcodeEnabled = true, FitContent = true };
		inner.AddChild(slot.StatusLabel);
		card.AddChild(inner);
		_enemyList.AddChild(card);
		slot.NameLabel.GuiInput += e =>
		{
			if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
			{
				if (_targeting && _targetSkill != null && _targetSkill.Type == "attack" && slot.Alive)
					ConfirmTarget(slot);
				else
					slot.StatLabel.Visible = !slot.StatLabel.Visible;
			}
		};
		card.GuiInput += e =>
		{
			if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } && _targeting && slot.Alive && _targetSkill != null && _targetSkill.Type == "attack")
			{
				ConfirmTarget(slot);
				GetViewport().SetInputAsHandled();
			}
		};
		_enemies.Add(slot);
	}

	public void AddAlly(string name)
	{
		var state = CompanionState.Meet(name);
		if (state == null) return;
		var st = state.SpawnStats();
		var slot = new CompSlot { State = state, Stats = st };
		var outer = new VBoxContainer();
		outer.AddThemeConstantOverride("separation", 2);
		var topRow = new HBoxContainer();
		slot.NameLabel = new Label { MouseFilter = MouseFilterEnum.Stop };
		topRow.AddChild(slot.NameLabel);
		slot.GaugeBar = MakeGaugeBar();
		topRow.AddChild(slot.GaugeBar);
		outer.AddChild(topRow);
		var barRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		slot.BruiseBar = MakeHpBar(); slot.SevereBar = MakeHpBar();
		barRow.AddChild(slot.BruiseBar); barRow.AddChild(slot.SevereBar);
		outer.AddChild(barRow);
		AddBarLabel(slot.BruiseBar, out slot.BruiseLabel);
		AddBarLabel(slot.SevereBar, out slot.SevereLabel);
		var energyRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		energyRow.AddChild(new Label { Text = "精" });
		slot.EnergyBar = MakeEnergyBar();
		energyRow.AddChild(slot.EnergyBar);
		slot.EnergyLabel = new Label();
		energyRow.AddChild(slot.EnergyLabel);
		outer.AddChild(energyRow);
		slot.StatLabel = new RichTextLabel { BbcodeEnabled = true, FitContent = true, Visible = false };
			outer.AddChild(slot.StatLabel);
			slot.StatusLabel = new RichTextLabel { BbcodeEnabled = true, FitContent = true };
			outer.AddChild(slot.StatusLabel);
			slot.SkillsArea = new VBoxContainer { Visible = false };
		var actLabel = new RichTextLabel { BbcodeEnabled = true, FitContent = true };
		slot.SkillsArea.AddChild(actLabel);
		outer.AddChild(slot.SkillsArea);
		var card = new PanelContainer();
		slot.Card = card;
		card.AddChild(outer);
		_companionSection.AddChild(card);
		slot.NameLabel.GuiInput += e =>
		{
			if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
			{
				if (_targeting && !string.IsNullOrEmpty(_targetItemId) && slot.State.Alive)
				{
					if (_activeItemUser != null)
					{
						if (_givingMode)
							ConfirmCompanionGiveTarget(_targetItemId, _activeItemUser, slot);
						else
							ConfirmCompanionItemUse(slot);
					}
					else if (_givingMode)
						DoPlayerGift(_targetItemId, slot);
					else
						ConfirmItemTarget(slot.Stats);
				}
				else if (_targeting && _targetSkill != null && _targetSkill.Type == "heal" && slot.State.Alive)
					ConfirmAllyTarget(slot);
				else
					slot.StatLabel.Visible = !slot.StatLabel.Visible;
			}
		};
		card.GuiInput += e =>
		{
			if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
			{
				if (_targeting && !string.IsNullOrEmpty(_targetItemId) && slot.State.Alive)
				{
					if (_activeItemUser != null)
					{
						if (_givingMode)
							ConfirmCompanionGiveTarget(_targetItemId, _activeItemUser, slot);
						else
							ConfirmCompanionItemUse(slot);
					}
					else if (_givingMode)
						DoPlayerGift(_targetItemId, slot);
					else
						ConfirmItemTarget(slot.Stats);
					GetViewport().SetInputAsHandled();
				}
				else if (_targeting && _targetSkill != null && _targetSkill.Type == "heal" && slot.State.Alive)
				{
					ConfirmAllyTarget(slot);
					GetViewport().SetInputAsHandled();
				}
			}
		};
		_compSlots.Add(slot);
		Log($"『{name}』加入了战斗！", "#ffcc44");
		RefreshAll();
	}

	public void ApplyBuff(string target, string buffId, int duration)
	{
		GD.Print($"[CombatUI.ApplyBuff] target={target} buffId={buffId} dur={duration}");
		var tracker = GetStatusTracker(target);
		if (tracker == null)
		{
			GD.PrintErr($"[CombatUI.ApplyBuff] No StatusTracker for target '{target}'");
			return;
		}

		string statusId = buffId;
		GD.Print($"[CombatUI.ApplyBuff] Applying '{statusId}' to '{target}' via tracker...");
		var active = tracker.Apply(statusId, duration, target);
		if (active == null) { GD.PrintErr("[CombatUI.ApplyBuff] Apply returned null!"); return; }

		GD.Print($"[CombatUI.ApplyBuff] OK — {target} now has {tracker.Count} active status(es)");
		CombatEvents.Fire("on_state_applied", new FireContext
		{
			Source = target,
			Target = target,
			StatusId = statusId,
			Round = _round,
		});

		RefreshAll();
	}

	StatusTracker GetStatusTracker(string target)
	{
		if (target == "player") return _player?.Statuses;
		var comp = _compSlots.FirstOrDefault(s => s.State.Name == target);
		if (comp != null) return comp.Stats?.Statuses;
		var enemy = _enemies.FirstOrDefault(e => e.DisplayName == target);
		return enemy?.Stats?.Statuses;
	}

	public bool HasStatus(string target, string statusId)
	{
		var tracker = GetStatusTracker(target);
		return tracker != null && tracker.HasStatus(statusId);
	}

	void TickOwnerStatuses(StatusTracker tracker, string ownerName)
	{
		if (tracker == null)
		{
			GD.Print($"── [StatusTracker] {ownerName} 回合结算 (tracker=null, skip)");
			return;
		}
		if (tracker.Count == 0)
		{
			GD.Print($"── [StatusTracker] {ownerName} 回合结算 (无状态, skip)");
			return;
		}
		GD.Print($"── [StatusTracker] {ownerName} 回合结算 ──");
		var events = tracker.TickRound();
		foreach (var (status, reason) in events)
		{
			if (reason == "expired")
			{
				GD.Print($"  ⏰ [{ownerName}] 状态 [{status.StatusId}] {status.Name} 到期");
				CombatEvents.Fire("on_state_removed", new FireContext
				{
					Source = ownerName,
					StatusId = status.StatusId,
					Round = _round,
				});
			}
		}
	}

	void EndCharacterTurn(StatusTracker tracker, string name)
	{
		CombatEvents.Fire("on_turn_end", new FireContext { Source = name, Round = _round });
		TickOwnerStatuses(tracker, name);
	}
}
