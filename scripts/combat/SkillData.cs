namespace No1.Combat;

using Godot;

public class SkillDef
{
	public string Id, Name, Owner, Type;
	public float Power = 1f;
	public string Effect;
	public string Target;
	public bool RemoveDebuff;
	public string DamageType;   // "physical" / "spirit"
	public int Cost;            // 精力消耗
}

public static class SkillData
{
	static Dictionary<string, List<SkillDef>> _byOwner = new();

	public static void Load()
	{
		_byOwner.Clear();
		var file = FileAccess.Open("res://assets/data/skills.json", FileAccess.ModeFlags.Read);
		if (file == null) return;

		var dict = Json.ParseString(file.GetAsText()).AsGodotDictionary();
		foreach (var item in dict["skills"].AsGodotArray())
		{
			var d = item.AsGodotDictionary();
			var s = new SkillDef
			{
				Id           = S(d, "id"),
				Name         = S(d, "name"),
				Owner        = S(d, "owner"),
				Type         = S(d, "type"),
				Power        = d.ContainsKey("power") ? d["power"].AsSingle() : 1f,
				Effect       = S(d, "effect"),
				Target       = S(d, "target"),
				RemoveDebuff = d.ContainsKey("remove_debuff") && d["remove_debuff"].AsBool(),
				DamageType   = S(d, "damage_type"),
				Cost         = d.ContainsKey("cost") ? d["cost"].AsInt32() : 0,
			};
			if (!_byOwner.ContainsKey(s.Owner))
				_byOwner[s.Owner] = new();
			_byOwner[s.Owner].Add(s);
		}
		GD.Print($"[SkillData] Loaded {_byOwner.Sum(kv => kv.Value.Count)} skills");
	}

	static string S(Godot.Collections.Dictionary d, string k) => d.ContainsKey(k) ? d[k].AsString() : "";

	public static List<SkillDef> For(string owner)
	{
		return _byOwner.TryGetValue(owner, out var list) ? list : new();
	}
}
