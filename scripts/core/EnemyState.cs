namespace No1.Core;

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 敌人注册类 — 镜像 CompanionState 模式。
/// 从 enemies.json 加载 10 种敌人，支持 normal/elite（min/max 浮动）和 boss/special（固定值）。
/// </summary>
public class EnemyState
{
	// ── 静态注册 ──

	static Dictionary<string, EnemyState> _registry;

	public static void LoadRegistry()
	{
		_registry = new();
		GD.Print("[EnemyState] Loading registry...");

		var file = FileAccess.Open("res://assets/data/enemies.json", FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PrintErr("[EnemyState] enemies.json NOT FOUND");
			return;
		}

		var dict = Json.ParseString(file.GetAsText()).AsGodotDictionary();
		foreach (var item in dict["enemies"].AsGodotArray())
		{
			var d = item.AsGodotDictionary();
			var state = new EnemyState
			{
				Id       = d["id"].AsString(),
				Name     = d["name"].AsString(),
				Category = d["category"].AsString(),
				Desc     = d["desc"].AsString(),
				Zone     = d["zone"].AsString(),
				Rarity   = (int)d["rarity"].AsDouble(),
				Archetype = d["archetype"].AsString(),
			};

			// Parse stats — 支持 min/max 范围 或 固定值
			var s = d["stats"].AsGodotDictionary();
			if (s["power"].VariantType == Variant.Type.Int || s["power"].VariantType == Variant.Type.Float)
			{
				state.PowerMin = state.PowerMax = (int)(s["power"].AsDouble());
				state.BodyMin  = state.BodyMax  = (int)(s["body"].AsDouble());
				state.AgilityMin = state.AgilityMax = (int)(s["agility"].AsDouble());
				state.HeartMin   = state.HeartMax   = (int)(s["heart"].AsDouble());
				state.FortuneMin = state.FortuneMax = (int)(s["fortune"].AsDouble());
			}
			else
			{
				state.PowerMin   = (int)s["power"].AsGodotDictionary()["min"].AsDouble();
				state.PowerMax   = (int)s["power"].AsGodotDictionary()["max"].AsDouble();
				state.BodyMin    = (int)s["body"].AsGodotDictionary()["min"].AsDouble();
				state.BodyMax    = (int)s["body"].AsGodotDictionary()["max"].AsDouble();
				state.AgilityMin = (int)s["agility"].AsGodotDictionary()["min"].AsDouble();
				state.AgilityMax = (int)s["agility"].AsGodotDictionary()["max"].AsDouble();
				state.HeartMin   = (int)s["heart"].AsGodotDictionary()["min"].AsDouble();
				state.HeartMax   = (int)s["heart"].AsGodotDictionary()["max"].AsDouble();
				state.FortuneMin = (int)s["fortune"].AsGodotDictionary()["min"].AsDouble();
				state.FortuneMax = (int)s["fortune"].AsGodotDictionary()["max"].AsDouble();
			}

			// Parse HP
			var h = d["hp"].AsGodotDictionary();
			if (h["bruise"].VariantType == Variant.Type.Int || h["bruise"].VariantType == Variant.Type.Float)
			{
				state.BruiseMin = state.BruiseMax = (int)(h["bruise"].AsDouble());
				state.SevereMin = state.SevereMax = (int)(h["severe"].AsDouble());
			}
			else
			{
				state.BruiseMin = (int)h["bruise"].AsGodotDictionary()["min"].AsDouble();
				state.BruiseMax = (int)h["bruise"].AsGodotDictionary()["max"].AsDouble();
				state.SevereMin = (int)h["severe"].AsGodotDictionary()["min"].AsDouble();
				state.SevereMax = (int)h["severe"].AsGodotDictionary()["max"].AsDouble();
			}

			// Parse speed
			if (d.ContainsKey("speed"))
			{
				var sp = d["speed"];
				if (sp.VariantType == Variant.Type.Int || sp.VariantType == Variant.Type.Float)
				{
					state.SpeedMin = state.SpeedMax = (int)(sp.AsDouble());
				}
				else
				{
					var spd = sp.AsGodotDictionary();
					state.SpeedMin = (int)spd["min"].AsDouble();
					state.SpeedMax = (int)spd["max"].AsDouble();
				}
			}

			_registry[state.Id] = state;
		}

		GD.Print($"[EnemyState] Loaded {_registry.Count} enemies");
	}

	public static EnemyState Get(string id)
	{
		if (string.IsNullOrEmpty(id)) return null;
		return _registry.TryGetValue(id, out var e) ? e : null;
	}

	/// <summary>
	/// 从指定区域随机选一个敌人。
	/// zone = 区域名（"林地边缘"、"*" 等）
	/// rarityCap = 最高稀有度限制（周目越高 cap 越高）
	/// eliteChance = 精英概率（周目越高越高）
	/// </summary>
	public static EnemyState GetRandom(string zone, int rarityCap = 3, float eliteChance = 0.2f)
	{
		var pool = _registry.Values
			.Where(e => e.Zone == zone || e.Zone == "*")
			.Where(e => e.Rarity <= rarityCap)
			.Where(e => e.Category == "normal")
			.ToList();

		if (pool.Count == 0)
		{
			pool = _registry.Values
				.Where(e => e.Category == "normal")
				.ToList();
		}
		if (pool.Count == 0) return null;

		var pick = pool[(int)(GD.Randi() % (uint)pool.Count)];

		if (GD.Randf() < eliteChance)
			return pick.AsElite();

		return pick;
	}

	public static List<EnemyState> All => _registry.Values.ToList();

	// ── 实例属性 ──

	public string Id;
	public string Name;
	public string Category;   // "normal" | "elite" | "boss" | "special"
	public string Desc;
	public string Zone;
	public int Rarity;
	public string Archetype;  // "biped_small" | "quadruped" | "floater"

	public int PowerMin,  PowerMax,  BodyMin,  BodyMax;
	public int AgilityMin, AgilityMax, HeartMin, HeartMax, FortuneMin, FortuneMax;
	public int BruiseMin, BruiseMax, SevereMin, SevereMax;
	public int SpeedMin, SpeedMax;

	public bool IsBoss    => Category == "boss";
	public bool IsSpecial => Category == "special";
	public bool IsElite   => Category == "elite";

	const float EliteMultiplier = 1.3f;

	// ── 生成方法 ──

	/// <summary>
	/// 创建 CharacterStats，含运行时浮动（normal/elite 随机取值，boss 固定值）。
	/// cycleModifier: 周目数（1 起步），每轮回 +2% 全属性，上限 +30%
	/// </summary>
	public CharacterStats SpawnStats(int cycleModifier = 1)
	{
		float cycleBonus = 1f + Mathf.Min((cycleModifier - 1) * 0.02f, 0.30f);
		float eliteBonus = IsElite ? EliteMultiplier : 1f;
		float totalMult = cycleBonus * eliteBonus;

		var st = new CharacterStats
		{
			DisplayName = Name,
			Power   = Roll(PowerMin,   PowerMax,   totalMult),
			Body    = Roll(BodyMin,    BodyMax,    totalMult),
			Agility = Roll(AgilityMin, AgilityMax, totalMult),
			Heart   = Roll(HeartMin,   HeartMax,   totalMult),
			Fortune = Roll(FortuneMin, FortuneMax, totalMult),
		};

		int desiredBruise = Roll(BruiseMin, BruiseMax, totalMult);
		int desiredSevere = Roll(SevereMin, SevereMax, totalMult);

		// Speed 不受周目加成影响（只受精英倍率）
		int spd = SpeedMin == SpeedMax
			? SpeedMin
			: (int)GD.Randi() % (SpeedMax - SpeedMin + 1) + SpeedMin;
		int desiredSpeed = IsElite ? Mathf.RoundToInt(spd * EliteMultiplier) : spd;

		st.ApplyModifier("bruise_hp", desiredBruise - st.MaxBruiseHP);
		st.ApplyModifier("severe_hp", desiredSevere - st.MaxSevereHP);
		st.ApplyModifier("speed", desiredSpeed - st.Speed);

		st.FullHeal();
		return st;
	}

	int Roll(int min, int max, float mult)
	{
		int raw = min == max
			? min
			: (int)GD.Randi() % (max - min + 1) + min;
		return Mathf.RoundToInt(raw * mult);
	}

	/// <summary>从 normal 敌人创建精英变体（复制属性 + elite 标记 + 1.3× 倍率）</summary>
	public EnemyState AsElite()
	{
		return new EnemyState
		{
			Id       = Id + "_elite",
			Name     = "精英" + Name,
			Category = "elite",
			Desc     = Desc,
			Zone     = Zone,
			Rarity   = Mathf.Min(Rarity + 1, 5),
			Archetype = Archetype,

			PowerMin=PowerMin, PowerMax=PowerMax,
			BodyMin=BodyMin, BodyMax=BodyMax,
			AgilityMin=AgilityMin, AgilityMax=AgilityMax,
			HeartMin=HeartMin, HeartMax=HeartMax,
			FortuneMin=FortuneMin, FortuneMax=FortuneMax,
			BruiseMin=BruiseMin, BruiseMax=BruiseMax,
			SevereMin=SevereMin, SevereMax=SevereMax,
			SpeedMin=SpeedMin, SpeedMax=SpeedMax,
		};
	}
}
