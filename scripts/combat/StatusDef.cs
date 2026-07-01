namespace No1.Combat;

using Godot;
using System.Collections.Generic;

/// <summary>
/// 状态效果定义 — 从 status_effects.json 加载。
/// Effects 字典的 key 对应 CharacterStats.ApplyModifier 的 modifier key。
/// </summary>
public class StatusDef
{
	public string Id;
	public string Name;
	public string Type;          // "buff" | "debuff"
	public Dictionary<string, int> Effects = new();
	public int TickDamage;
	public int DefaultDuration;
	public string IconColor;
	public string Description;

	static Dictionary<string, StatusDef> _registry;

	public static void Load()
	{
		_registry = new();
		GD.Print("[StatusDef] Loading status_effects.json ...");

		var file = FileAccess.Open("res://assets/data/status_effects.json", FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PrintErr("[StatusDef] status_effects.json NOT FOUND");
			return;
		}

		var root = Json.ParseString(file.GetAsText()).AsGodotDictionary();
		if (!root.ContainsKey("statuses"))
		{
			GD.PrintErr("[StatusDef] No 'statuses' key in json");
			return;
		}

		foreach (var (key, val) in root["statuses"].AsGodotDictionary())
		{
			var d = val.AsGodotDictionary();
			var def = new StatusDef
			{
				Id              = key.AsString(),
				Name            = S(d, "name"),
				Type            = S(d, "type"),
				DefaultDuration = d.ContainsKey("default_duration") ? d["default_duration"].AsInt32() : 3,
				IconColor       = S(d, "icon_color"),
				Description     = S(d, "description"),
				TickDamage      = d.ContainsKey("tick_damage") ? d["tick_damage"].AsInt32() : 0,
			};

			if (d.ContainsKey("effects"))
			{
				foreach (var (ek, ev) in d["effects"].AsGodotDictionary())
					def.Effects[ek.AsString()] = (int)(ev.AsDouble());
			}

			_registry[def.Id] = def;
		}

		GD.Print($"[StatusDef] Loaded {_registry.Count} status effects.");
		foreach (var s in _registry.Values)
			GD.Print($"  [{s.Id}] {s.Name} ({s.Type}) dur={s.DefaultDuration} tickDmg={s.TickDamage}");
	}

	static string S(Godot.Collections.Dictionary d, string k) => d.ContainsKey(k) ? d[k].AsString() : "";

	public static StatusDef Get(string id)
	{
		if (string.IsNullOrEmpty(id)) return null;
		return _registry.TryGetValue(id, out var def) ? def : null;
	}

	public static IReadOnlyCollection<StatusDef> All => _registry?.Values;
}
