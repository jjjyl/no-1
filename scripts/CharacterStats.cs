namespace No1;

using Godot;

public partial class CharacterStats : Node
{
	[Export] public string DisplayName { get; set; } = "未命名";

	// ── 基础属性 ──
	[ExportGroup("基础属性")]
	[Export] public int Power    { get; set; } = 6;   // 力 — 物理伤害基准
	[Export] public int Body     { get; set; } = 6;   // 体 — HP + 物理减伤
	[Export] public int Agility  { get; set; } = 6;   // 敏 — 闪避 + ATB速度
	[Export] public int Heart    { get; set; } = 5;   // 心 — 精神伤害/减伤 + 治疗 + 精力
	[Export] public int Fortune  { get; set; } = 6;   // 运 — 暴击

	// ── 派生属性（只读，从基础属性 + 加成计算）──
	public int  ATK        => Power + GetBonus("atk");
	public int  MaxBruiseHP => 10 + Body * 2 + GetBonus("bruise_hp");
	public int  MaxSevereHP => 5 + Body + GetBonus("severe_hp");
	public int  DefFlat     => Body / 2 + GetBonus("def_flat");
	public int  SpiritDef   => Heart / 3 + GetBonus("spirit_def");
	public float Dodge      => Agility * 1.5f + GetBonus("dodge");
	public int  Speed       => 5 + Agility + GetBonus("speed");
	public int  EnergyMax   => Heart * 3 + GetBonus("energy_max");
	public float CritRate   => Fortune * 1.2f + GetBonus("crit_rate");
	public float CritDMG    => 1.5f;
	public float HealBonus  => Heart * 0.3f;
	public int   EnergyRegen => Math.Max(1, Heart / 3);

	// ── 当前状态 ──
	public int  BruiseHP  { get; set; }
	public int  SevereHP  { get; set; }
	public int  Energy    { get; set; }
	public bool IsDead    => SevereHP <= 0;
	public bool CanAct    => Energy > 0 && !IsDead;

	// ── 派生属性加成（加护/装备等外部修改，不影响基础属性）──
	readonly Dictionary<string, int> _bonuses = new();

	int GetBonus(string key) => _bonuses.TryGetValue(key, out var v) ? v : 0;

	/// <summary>
	/// 统一修改入口。
	/// 基础属性键: power/body/agility/heart/fortune → 修改基础值，派生自动跟随。
	/// 派生加成键: atk/dodge/speed/def_flat/spirit_def/crit_rate/energy_max/bruise_hp/severe_hp → 叠加在派生值上。
	/// </summary>
	public void ApplyModifier(string key, float value)
	{
		int iv = Mathf.RoundToInt(value);
		switch (key)
		{
			case "power":   Power   += iv; break;
			case "body":    Body    += iv; break;
			case "agility": Agility += iv; break;
			case "heart":   Heart   += iv; break;
			case "fortune": Fortune += iv; break;
			default:
				_bonuses.TryGetValue(key, out var cur);
				_bonuses[key] = cur + iv;
				break;
		}
	}

	/// <summary>Reverse of ApplyModifier. Used for unequipping items.</summary>
	public void RemoveModifier(string key, float value)
	{
		int iv = Mathf.RoundToInt(value);
		switch (key)
		{
			case "power":   Power   -= iv; break;
			case "body":    Body    -= iv; break;
			case "agility": Agility -= iv; break;
			case "heart":   Heart   -= iv; break;
			case "fortune": Fortune -= iv; break;
			default:
				_bonuses.TryGetValue(key, out var cur);
				int next = cur - iv;
				if (next <= 0)
					_bonuses.Remove(key);
				else
					_bonuses[key] = next;
				break;
		}
	}

	// ── 生命周期 ──
	public override void _Ready() => FullHeal();

	public void FullHeal()
	{
		BruiseHP = MaxBruiseHP;
		SevereHP = MaxSevereHP;
		Energy   = EnergyMax;
	}

	// ── 伤害 ──

	/// <param name="skillPower">技能乘数，普攻 = 1.0</param>
	/// <param name="isSpirit">true=精神伤害（用心），false=物理伤害（用力）</param>
	public int DealDamage(float skillPower = 1.0f, bool isSpirit = false)
	{
		int baseAtk = isSpirit ? Heart : ATK;
		int baseDmg = Mathf.RoundToInt(baseAtk * skillPower);
		int min = Mathf.RoundToInt(baseDmg * 0.8f);
		int max = Mathf.RoundToInt(baseDmg * 1.2f);
		int raw = (int)GD.RandRange(min, max + 1);

		// 暴击
		if (GD.RandRange(0f, 100f) < CritRate)
			raw = Mathf.RoundToInt(raw * CritDMG);

		return Math.Max(1, raw);
	}

	/// <param name="raw">攻击方的原始伤害值</param>
	/// <param name="isSpirit">true=精神攻击，走 SpiritDef 减伤 + 扣精力</param>
	/// <returns>实际造成的 HP 伤害</returns>
	public int TakeDamage(int raw, bool isSpirit = false)
	{
		// 闪避
		if (GD.RandRange(0f, 100f) < Dodge) return 0;

		int def = isSpirit ? SpiritDef : DefFlat;
		int dmg = Math.Max(1, raw - def);

		// 精神攻击额外扣精力（不受心减伤影响）
		if (isSpirit)
			Energy = Math.Max(0, Energy - Math.Max(1, raw / 2));

		// 双血条扣除
		int b = Math.Min(dmg, BruiseHP);
		BruiseHP -= b;
		if (dmg > b)
			SevereHP = Math.Max(0, SevereHP - (dmg - b));

		return dmg;
	}
}
