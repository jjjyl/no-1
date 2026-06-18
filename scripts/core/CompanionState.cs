namespace No1.Core;

using Godot;

public class CompanionState
{
	public string Name;
	public int Power, Body, Agility, Heart, Fortune;
	public int BruiseHP, SevereHP;
	public int Favor;
	public bool Alive = true;
	public int HighestFavor, CyclesMet;

	static Dictionary<string, CompanionState> _registry;

	public static void LoadRegistry()
	{
		_registry = new();
		GD.Print("[CompanionState] Loading registry...");
		var file = FileAccess.Open("res://assets/data/companions.json", FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PrintErr("[CompanionState] companions.json NOT FOUND");
			return;
		}
		GD.Print("[CompanionState] File opened, parsing...");

		var dict = Json.ParseString(file.GetAsText()).AsGodotDictionary();
		foreach (var item in dict["companions"].AsGodotArray())
		{
			var d = item.AsGodotDictionary();
			_registry[d["name"].AsString()] = new CompanionState
			{
				Name    = d["name"].AsString(),
				Power   = (int)d["power"].AsDouble(),
				Body    = (int)d["body"].AsDouble(),
				Agility = (int)d["agility"].AsDouble(),
				Heart   = (int)d["heart"].AsDouble(),
				Fortune = (int)d["fortune"].AsDouble(),
			};
		}
		GD.Print($"[CompanionState] Loaded {_registry.Count} companions");
	}

	public static CompanionState Meet(string name)
	{
		_registry ??= new();
		if (!_registry.TryGetValue(name, out var t)) return null;

		int bonus = Math.Min(t.CyclesMet * 2, 20);
		var cs = new CompanionState
		{
			Name    = t.Name,
			Power   = t.Power,
			Body    = t.Body,
			Agility = t.Agility,
			Heart   = t.Heart,
			Fortune = t.Fortune,
			BruiseHP = 0, SevereHP = 0,  // SpawnStats + FullHeal 时会填充
			Favor   = bonus,
			Alive   = true,
			HighestFavor = t.HighestFavor,
			CyclesMet    = t.CyclesMet + 1
		};
		t.HighestFavor = Math.Max(t.HighestFavor, cs.Favor);
		t.CyclesMet = cs.CyclesMet;
		return cs;
	}

	public CharacterStats SpawnStats()
	{
		var st = new CharacterStats
		{
			DisplayName = Name,
			Power   = Power,
			Body    = Body,
			Agility = Agility,
			Heart   = Heart,
			Fortune = Fortune,
		};
		st.FullHeal();
		return st;
	}
}
