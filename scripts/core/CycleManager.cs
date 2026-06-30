namespace No1.Core;

using Godot;
using No1.Data;

public partial class CycleManager : Node
{
	public static CycleManager Instance { get; private set; } = null!;
	public int CurrentCycle { get; private set; } = 1;
	public BlessingType? SelectedBlessing { get; private set; }
	public CharacterStats PlayerStats { get; private set; } = null!;
	public int CurrentNodeIndex { get; set; }
	public int CurrentRegionIndex { get; set; }
	public ulong WorldSeed { get; set; }
	public string[] OverridePaths { get; set; } = System.Array.Empty<string>();
	public List<ulong> RecentSeeds { get; set; } = new();
	public List<CompanionState> ActiveCompanions { get; private set; } = new();
	public string PendingEnemyScene;
	public string PendingBattleEvents;
	public Inventory PlayerInventory { get; private set; } = null!;
	public int FragmentCount { get; private set; }
	public int Money { get; set; }
	public bool SkipStartEvents;
	public Vector3 LastWorldPosition;
	const int MaxRecentSeeds = 5;

	HashSet<string> _flags = new();           // 轮回级
	HashSet<string> _accountFlags = new();    // 账号级（跨轮回不重置）

	const string SavePath = "user://save.json";

	public override void _Ready()
	{
		CompanionState.LoadRegistry();
		EnemyState.LoadRegistry();
		EventManager.Load();
		Instance = this;
		LoadAccount();
		if (Money == 0) Money = 100;
		CreatePlayer();
		PlayerInventory = new Inventory(PlayerStats, maxItems: 10);
		LoadInventory();
	}

	void CreatePlayer()
	{
		PlayerStats = new CharacterStats
		{
			DisplayName = "玩家",
			Power = 6, Body = 6, Agility = 6, Heart = 5, Fortune = 6
		};
		PlayerStats.FullHeal();
	}

	public void SelectBlessing(BlessingType type)
	{
		SelectedBlessing = type;
		CreatePlayer();
		foreach (var (key, val) in BlessingData.Get(type).Modifiers)
		{
			if (key == "encounter_rate") continue;  // 元变量自己处理
			PlayerStats.ApplyModifier(key, val);
		}
		PlayerInventory.ReapplyEquipmentModifiers();
	}

	public float GetBlessingModifier(string key)
	{
		if (SelectedBlessing == null) return 0f;
		return BlessingData.Get(SelectedBlessing.Value).Modifiers.TryGetValue(key, out var v) ? v : 0f;
	}

	public float GetEncounterRate()
	{
		float rate = 0.4f;
		if (SelectedBlessing != null)
			if (BlessingData.Get(SelectedBlessing.Value).Modifiers.TryGetValue("encounter_rate", out var m))
				rate *= 1f + m;
		return Math.Max(0.1f, rate);
	}

	public void EnterWorld(ulong seed = 0, string[] overridePaths = null)
	{
		if (seed == 0) seed = (ulong)System.Random.Shared.NextInt64();
		WorldSeed = seed;
		OverridePaths = overridePaths ?? System.Array.Empty<string>();
		RecentSeeds.Insert(0, seed);
		if (RecentSeeds.Count > MaxRecentSeeds)
			RecentSeeds = RecentSeeds.Distinct().Take(MaxRecentSeeds).ToList();
		PlayerStats.FullHeal();
		ActiveCompanions.Clear();
		_flags.Clear();
		EventManager.ResetCycle();
		CurrentNodeIndex = 0;
		CurrentRegionIndex = 0;
	}

	public void JoinCompanion(string name)
	{
		var comp = CompanionState.Meet(name);
		if (comp == null) return;
		ActiveCompanions.Add(comp);
	}

	public void AddFragment(int count = 1)
	{
		FragmentCount += count;
		GD.Print($"[CycleManager] FragmentCount: {FragmentCount}");
		SaveAccount();
	}

	// ── 轮回级 flag ──

	public void SetFlag(string flag) => _flags.Add(flag);
	public bool HasFlag(string flag) => _flags.Contains(flag);

	// ── 账号级 flag（跨轮回持久）──

	public void SetAccountFlag(string flag)
	{
		_accountFlags.Add(flag);
		SaveAccount();
	}

	public void RemoveAccountFlag(string flag)
	{
		_accountFlags.Remove(flag);
		SaveAccount();
	}

	public bool HasAccountFlag(string flag) => _accountFlags.Contains(flag);

	// ── 存档 ──

	public void SaveAccount()
	{
		var save = new Godot.Collections.Dictionary
		{
			["cycle"] = CurrentCycle,
			["fragmentCount"] = FragmentCount,
			["money"] = Money,
			["accountFlags"] = new Godot.Collections.Array(_accountFlags.Select(s => (Variant)s).ToArray()),
			["recentSeeds"] = new Godot.Collections.Array(RecentSeeds.Select(s => (Variant)s.ToString()).ToArray()),
		};
		if (PlayerInventory != null)
			save["inventory"] = PlayerInventory.Serialize();

		// Save companion inventories
		var compDict = new Godot.Collections.Dictionary();
		foreach (var c in ActiveCompanions)
			if (c.Inventory != null)
				compDict[c.Name] = c.Inventory.Serialize();
		save["companion_inventories"] = compDict;

		var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
		if (file == null)
		{
			GD.PrintErr("[CycleManager] Failed to open save file for writing");
			return;
		}
		file.StoreString(Json.Stringify(save));
		file.Close();
		GD.Print($"[CycleManager] Saved: cycle={CurrentCycle}, accountFlags={_accountFlags.Count}");
	}

	void LoadAccount()
	{
		if (!FileAccess.FileExists(SavePath)) return;

		var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
		if (file == null) return;

		var json = file.GetAsText();
		file.Close();

		var dict = Json.ParseString(json).AsGodotDictionary();
		CurrentCycle = dict.ContainsKey("cycle") ? dict["cycle"].AsInt32() : 1;
		FragmentCount = dict.ContainsKey("fragmentCount") ? dict["fragmentCount"].AsInt32() : 0;
		Money = dict.ContainsKey("money") ? dict["money"].AsInt32() : 100;

		if (dict.ContainsKey("accountFlags"))
			foreach (var item in dict["accountFlags"].AsGodotArray())
				_accountFlags.Add(item.AsString());

		if (dict.ContainsKey("recentSeeds"))
			foreach (var item in dict["recentSeeds"].AsGodotArray())
				if (ulong.TryParse(item.AsString(), out var s))
					RecentSeeds.Add(s);

		GD.Print($"[CycleManager] Loaded: cycle={CurrentCycle}, accountFlags={_accountFlags.Count}");
	}

	void LoadInventory()
	{
		if (!FileAccess.FileExists(SavePath)) return;
		var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
		if (file == null) return;
		var json = file.GetAsText();
		file.Close();
		var dict = Json.ParseString(json).AsGodotDictionary();
		if (dict.ContainsKey("inventory"))
			PlayerInventory.Deserialize(dict["inventory"].AsGodotDictionary());

		if (dict.ContainsKey("companion_inventories"))
		{
			var compDict = dict["companion_inventories"].AsGodotDictionary();
			foreach (var key in compDict.Keys)
			{
				var name = key.AsString();
				var comp = ActiveCompanions.FirstOrDefault(c => c.Name == name);
				if (comp != null && comp.Inventory != null)
					comp.Inventory.Deserialize(compDict[key].AsGodotDictionary());
			}
		}
	}

	public void OnDeath()
	{
		CurrentCycle++;
		Money = 100;
		RemoveAccountFlag("scene:world");
		SaveAccount();
	}

	public void ReturnToTemple()
	{
		PlayerStats.FullHeal();
		SelectedBlessing = null;
		_flags.Clear();
		CurrentRegionIndex = 0;
		RemoveAccountFlag("scene:world");
	}
}
