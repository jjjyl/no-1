namespace No1.UI;
using Godot;
using No1.Core;

public partial class CharacterPanel : Control
{
	Control _statsTab;
	Label _nameLabel, _cycleLabel;
	Label _powerLabel, _bodyLabel, _agilityLabel, _heartLabel, _fortuneLabel;
	Label _atkLabel, _defLabel, _speedLabel, _critLabel;
	Label _hpBruiseLabel, _hpSevereLabel;
	ProgressBar _hpBruiseBar, _hpSevereBar;
	Label _blessingLabel;
	Container _companionList;
	Button _btnStats, _btnInventory, _btnClose;

	int _viewingCompanion = -1;

	public override void _Ready()
	{
		BindNodes();

		_btnClose.Pressed += QueueFree;
		_btnStats.Pressed += RefreshStats;
		_btnInventory.Pressed += () =>
		{
			var cm = CycleManager.Instance;
			var panel = DialogueManager.Instance.ShowInventory();
			if (panel == null || cm == null) return;

			if (_viewingCompanion >= 0 && _viewingCompanion < cm.ActiveCompanions.Count)
			{
				var comp = cm.ActiveCompanions[_viewingCompanion];
				if (comp.Inventory == null)
					comp.Inventory = new Inventory(owner: null, maxItems: 5);
				panel.SetInventory(comp.Inventory, comp.Name);
			}
			else
			{
				panel.SetInventory(cm.PlayerInventory, cm.PlayerStats?.DisplayName ?? "玩家");
			}
		};

		ShowPlayer();
	}

	public override void _ExitTree() { }

	void BindNodes()
	{
		var p = GetNode<Control>("Panel");

		_statsTab = p.GetNode<Control>("StatsTab");

		_nameLabel  = _statsTab.GetNode<Label>("NameLabel");
		_cycleLabel = _statsTab.GetNode<Label>("LevelLabel");

		var baseGrid = _statsTab.GetNode<GridContainer>("BaseGrid");
		_powerLabel   = baseGrid.GetNode<Label>("PowerLabel");
		_bodyLabel    = baseGrid.GetNode<Label>("BodyLabel");
		_agilityLabel = baseGrid.GetNode<Label>("AgilityLabel");
		_heartLabel   = baseGrid.GetNode<Label>("HeartLabel");
		_fortuneLabel = baseGrid.GetNode<Label>("FortuneLabel");

		var derivedGrid = _statsTab.GetNode<GridContainer>("DerivedGrid");
		_atkLabel   = derivedGrid.GetNode<Label>("AtkLabel");
		_defLabel   = derivedGrid.GetNode<Label>("DefLabel");
		_speedLabel = derivedGrid.GetNode<Label>("SpeedLabel");
		_critLabel  = derivedGrid.GetNode<Label>("CritLabel");

		var hpSec = _statsTab.GetNode<Control>("HPSection");
		_hpBruiseLabel = hpSec.GetNode<Label>("BruiseHPLabel");
		_hpBruiseBar   = hpSec.GetNode<ProgressBar>("BruiseHPBar");
		_hpSevereLabel = hpSec.GetNode<Label>("SevereHPLabel");
		_hpSevereBar   = hpSec.GetNode<ProgressBar>("SevereHPBar");

		_blessingLabel  = _statsTab.GetNode<Label>("BlessingLabel");
		_companionList  = _statsTab.GetNode<Container>("CompanionSection/CompanionList");

		var tabBar = p.GetNode<HBoxContainer>("TabBar");
		_btnStats    = tabBar.GetNode<Button>("BtnStats");
		_btnInventory = tabBar.GetNode<Button>("BtnInventory");
		_btnClose    = p.GetNode<Button>("BtnClose");
	}

	void ShowPlayer()
	{
		_viewingCompanion = -1;
		FillPlayerStats();
	}

	void ShowCompanion(int idx)
	{
		_viewingCompanion = idx;
		var cm = CycleManager.Instance;
		if (cm == null || idx >= cm.ActiveCompanions.Count) return;
		var comp = cm.ActiveCompanions[idx];
		var st = comp.SpawnStats();
		st.BruiseHP = comp.BruiseHP;
		st.SevereHP = comp.SevereHP;

		_nameLabel.Text   = comp.Name;
		_cycleLabel.Text  = $"好感 {comp.Favor}";
		_blessingLabel.Text = comp.Alive ? "同行中" : "已离队";

		FillBaseStats(st.Power, st.Body, st.Agility, st.Heart, st.Fortune);
		FillDerivedStats(st.ATK, st.DefFlat, st.Speed, st.CritRate);
		FillHP(st.BruiseHP, st.MaxBruiseHP, st.SevereHP, st.MaxSevereHP);

		BuildCompanionList();
	}

	void FillPlayerStats()
	{
		var cm = CycleManager.Instance;
		if (cm == null) return;
		var s = cm.PlayerStats;
		if (s == null) return;

		_nameLabel.Text   = s.DisplayName;
		_cycleLabel.Text  = $"第 {cm.CurrentCycle} 轮回";
		_blessingLabel.Text = cm.SelectedBlessing?.ToString() ?? "无加护";

		FillBaseStats(s.Power, s.Body, s.Agility, s.Heart, s.Fortune);
		FillDerivedStats(s.ATK, s.DefFlat, s.Speed, s.CritRate);
		FillHP(s.BruiseHP, s.MaxBruiseHP, s.SevereHP, s.MaxSevereHP);

		BuildCompanionList();
	}

	public void RefreshStats()
	{
		if (_viewingCompanion < 0) FillPlayerStats();
		else ShowCompanion(_viewingCompanion);
	}

	void FillBaseStats(int power, int body, int agility, int heart, int fortune)
	{
		_powerLabel.Text   = $"力 {power}";
		_bodyLabel.Text    = $"体 {body}";
		_agilityLabel.Text = $"敏 {agility}";
		_heartLabel.Text   = $"心 {heart}";
		_fortuneLabel.Text = $"运 {fortune}";
	}

	void FillDerivedStats(int atk, int def, int speed, float crit)
	{
		_atkLabel.Text   = $"攻击  {atk}";
		_defLabel.Text   = $"防御  {def}";
		_speedLabel.Text = $"速度  {speed}";
		_critLabel.Text  = $"暴击  {crit:F1}%";
	}

	void FillHP(int bruise, int maxBruise, int severe, int maxSevere)
	{
		_hpBruiseLabel.Text = $"轻伤 {bruise} / {maxBruise}";
		_hpBruiseBar.MaxValue = maxBruise;
		_hpBruiseBar.Value    = bruise;

		_hpSevereLabel.Text = $"重伤 {severe} / {maxSevere}";
		_hpSevereBar.MaxValue = maxSevere;
		_hpSevereBar.Value    = severe;
	}

	void BuildCompanionList()
	{
		foreach (var child in _companionList.GetChildren())
			child.QueueFree();

		var cm = CycleManager.Instance;
		if (cm == null || cm.ActiveCompanions.Count == 0)
		{
			_companionList.AddChild(new Label { Text = "无同行者" });
			return;
		}

		for (int i = 0; i < cm.ActiveCompanions.Count; i++)
		{
			int idx = i;
			var comp = cm.ActiveCompanions[i];
			var btn = new Button
			{
				Text = comp.Name,
				Flat = true,
				Alignment = HorizontalAlignment.Left
			};
			btn.Pressed += () => ShowCompanion(idx);
			_companionList.AddChild(btn);
		}
	}
}
