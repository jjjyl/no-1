namespace No1.Core;

using Godot;
using System.Collections.Generic;

/// <summary>
/// 敌人视觉配置静态访问器 — 从 enemy_visuals.json 加载。
/// 战斗 UI 用 battle_portrait（高清），地图用 spritesheet（像素）。
/// </summary>
public class EnemyVisualDef
{
	public string Spritesheet;
	public string BattlePortrait;
	public string Archetype;
	public float Scale = 1f;

	static Dictionary<string, EnemyVisualDef> _cache;

	public static EnemyVisualDef Get(string enemyId)
	{
		if (_cache == null) LoadAll();
		return _cache.TryGetValue(enemyId, out var def) ? def : null;
	}

	static void LoadAll()
	{
		_cache = new();
		var file = FileAccess.Open("res://assets/data/enemy_visuals.json", FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PrintErr("[EnemyVisualDef] enemy_visuals.json NOT FOUND");
			return;
		}
		var root = Json.ParseString(file.GetAsText()).AsGodotDictionary();
		foreach (var (key, val) in root)
		{
			if (key.AsString().StartsWith("_")) continue;
			var d = val.AsGodotDictionary();
			_cache[key.AsString()] = new EnemyVisualDef
			{
				Spritesheet     = S(d, "spritesheet"),
				BattlePortrait  = S(d, "battle_portrait"),
				Archetype       = S(d, "archetype"),
				Scale           = (float)(d.ContainsKey("scale") ? d["scale"].AsDouble() : 1.0),
			};
		}
		GD.Print($"[EnemyVisualDef] Loaded {_cache.Count} enemy visuals.");
	}

	static string S(Godot.Collections.Dictionary d, string k)
		=> d.ContainsKey(k) ? d[k].AsString() : "";
}
