namespace No1.Data;

public enum BlessingType { 察知, 战意, 旅人 }

public class BlessingData
{
	public BlessingType Type { get; }
	public string Name { get; }
	public string Description { get; }
	public Dictionary<string, float> Modifiers { get; }

	BlessingData(BlessingType t, string n, string d, Dictionary<string, float> m)
	{ Type = t; Name = n; Description = d; Modifiers = m; }

	public static readonly BlessingData Insight = new(BlessingType.察知, "察知",
		"遇敌率减半，战利品更丰富。",
		new() { ["encounter_rate"] = -0.5f });

	public static readonly BlessingData Valor = new(BlessingType.战意, "战意",
		"力量 +3。",
		new() { ["power"] = 3f });

	public static readonly BlessingData Wanderer = new(BlessingType.旅人, "旅人",
		"闪避率 +15%，遇敌率降低。",
		new() { ["dodge"] = 15f, ["encounter_rate"] = -0.3f });

	public static BlessingData Get(BlessingType t) => t switch
	{ BlessingType.察知 => Insight, BlessingType.战意 => Valor, BlessingType.旅人 => Wanderer, _ => throw new ArgumentOutOfRangeException() };

	public static IReadOnlyList<BlessingData> All => new[] { Insight, Valor, Wanderer };
}
