namespace No1.Combat;

using System.Collections.Generic;
using Godot;
using No1.Core;
using No1.UI;

public static class CombatEvents
{
	static List<EventDef> _events = new();
	static Queue<EffectDef> _queue = new();
	static HashSet<string> _firedOnce = new();
	static CombatUI _ui;

	public static void Init(string jsonPath, CombatUI ui)
	{
		_events.Clear();
		_queue.Clear();
		_firedOnce.Clear();
		_ui = ui;

		if (string.IsNullOrEmpty(jsonPath)) return;
		var file = FileAccess.Open(jsonPath, FileAccess.ModeFlags.Read);
		if (file == null) return;

		var data = Json.ParseString(file.GetAsText()).AsGodotDictionary();
		if (!data.ContainsKey("events")) return;

		foreach (var item in data["events"].AsGodotArray())
		{
			var d = item.AsGodotDictionary();
			var evt = new EventDef
			{
				Id = S(d, "id"),
				Once = !d.ContainsKey("once") || d["once"].AsBool(),
			};

			var t = d["trigger"].AsGodotDictionary();
			evt.Trigger = new TriggerDef
			{
				Type      = S(t, "type"),
				Source    = S(t, "source"),
				Skill     = S(t, "skill"),
				Target    = S(t, "target"),
				StatusId  = S(t, "status_id"),
				State     = S(t, "state"),
				MinDamage = t.ContainsKey("min") ? t["min"].AsInt32() : 0,
				Threshold = t.ContainsKey("threshold") ? t["threshold"].AsSingle() : 0,
				Round     = t.ContainsKey("round") ? t["round"].AsInt32() : 0,
			};

			if (d.ContainsKey("conditions"))
			{
				evt.Conditions = new();
				foreach (var c in d["conditions"].AsGodotArray())
				{
					var cd = c.AsGodotDictionary();
					evt.Conditions.Add(new ConditionDef
					{
						Type     = S(cd, "type"),
						Target   = S(cd, "target"),
						StatusId = S(cd, "status_id"),
						State    = S(cd, "state"),
						Value    = cd.ContainsKey("value") ? cd["value"].AsInt32() : 0,
						ValueF   = cd.ContainsKey("value") ? cd["value"].AsSingle() : 0,
						Op       = S(cd, "op"),
					});
				}
			}

			evt.Effects = new();
			foreach (var ef in d["effects"].AsGodotArray())
			{
				var ed = ef.AsGodotDictionary();
				evt.Effects.Add(new EffectDef
				{
					Type     = S(ed, "type"),
					Actor    = S(ed, "actor"),
					Target   = S(ed, "target"),
					SkillId  = S(ed, "skill_id"),
					Speaker  = S(ed, "speaker"),
					Text     = S(ed, "text"),
					BuffId   = S(ed, "buff_id"),
					Duration = ed.ContainsKey("duration") ? ed["duration"].AsInt32() : 0,
					EnemyId  = S(ed, "enemy_id"),
					Count    = ed.ContainsKey("count") ? ed["count"].AsInt32() : 0,
					ItemId   = S(ed, "item_id"),
					Amount   = ed.ContainsKey("amount") ? ed["amount"].AsInt32() : 1,
					VarName  = S(ed, "name"),
					VarValue = ed.ContainsKey("value") ? ed["value"].AsString() : "",
				});
			}

			_events.Add(evt);
		}
		GD.Print($"[CombatEvents] Loaded {_events.Count} events");
	}

	static string S(Godot.Collections.Dictionary d, string k) => d.ContainsKey(k) ? d[k].AsString() : "";

	public static void Fire(string triggerType, FireContext ctx)
	{
		foreach (var evt in _events)
		{
			if (!MatchTrigger(evt.Trigger, triggerType, ctx)) continue;
			if (evt.Once && _firedOnce.Contains(evt.Id)) continue;
			if (!EvalConditions(evt.Conditions, ctx)) continue;

			_firedOnce.Add(evt.Id);
			GD.Print($"[CombatEvents] Fired: {evt.Id}");
			foreach (var ef in evt.Effects)
				_queue.Enqueue(ef);
		}
	}

	static bool MatchTrigger(TriggerDef t, string triggerType, FireContext ctx)
	{
		if (t.Type != triggerType) return false;

		switch (triggerType)
		{
			case "on_skill_used":
				if (!MatchSource(t.Source, ctx.Source)) return false;
				if (!string.IsNullOrEmpty(t.Skill) && t.Skill != ctx.SkillId) return false;
				if (!string.IsNullOrEmpty(t.Target) && t.Target != "any" && t.Target != ctx.Target) return false;
				return true;
			case "on_damage_dealt":
				if (!MatchSource(t.Source, ctx.Source)) return false;
				if (!string.IsNullOrEmpty(t.Target) && t.Target != "any" && t.Target != ctx.Target) return false;
				if (t.MinDamage > 0 && ctx.Damage < t.MinDamage) return false;
				return true;
			case "on_hp_below":
				if (!MatchSource(t.Source, ctx.Source)) return false;
				if (ctx.HpPct > t.Threshold) return false;
				return true;
			case "on_state_applied":
			case "on_state_removed":
				if (!MatchSource(t.Source, ctx.Source)) return false;
				if (!string.IsNullOrEmpty(t.StatusId) && t.StatusId != ctx.StatusId) return false;
				return true;
			case "on_enemy_defeated":
			case "on_ally_defeated":
			case "on_turn_start":
			case "on_turn_end":
				return MatchSource(t.Source, ctx.Source);
			case "on_round":
				return t.Round <= 0 || t.Round == ctx.Round;
			case "on_battle_start":
				return true;
		}
		return false;
	}

	static bool MatchSource(string defSource, string ctxSource)
	{
		if (string.IsNullOrEmpty(defSource) || defSource == "any") return true;
		return defSource == ctxSource;
	}

	static bool EvalConditions(List<ConditionDef> conds, FireContext ctx)
	{
		if (conds == null) return true;
		foreach (var c in conds)
		{
			switch (c.Type)
			{
				case "hp_below":
					if (_ui.GetHpPct(c.Target) >= c.ValueF) return false;
					break;
				case "hp_above":
					if (_ui.GetHpPct(c.Target) <= c.ValueF) return false;
					break;
				case "favor_above":
					if (_ui.GetFavor(c.Target) < c.Value) return false;
					break;
				case "alive":
					if (!_ui.IsAlive(c.Target)) return false;
					break;
				case "enemy_count":
					if (!CompareOp(_ui.AliveEnemyCount(), c.Op, c.Value)) return false;
					break;
			case "round":
				if (!CompareOp(ctx.Round, c.Op, c.Value)) return false;
				break;
			case "pending_enemy":
				if (CycleManager.Instance.PendingEnemyScene != c.Target) return false;
				break;
			case "has_status":
				if (!_ui.HasStatus(c.Target, c.StatusId)) return false;
				break;
			}
		}
		return true;
	}

	static bool CompareOp(int actual, string op, int expected)
	{
		return op switch
		{
			"lt"  => actual < expected,
			"lte" => actual <= expected,
			"gt"  => actual > expected,
			"gte" => actual >= expected,
			_     => actual == expected,
		};
	}

	public static bool HasPending() => _queue.Count > 0;

	public static void ProcessNext()
	{
		if (_queue.Count == 0) return;
		var eff = _queue.Dequeue();

		switch (eff.Type)
		{
			case "show_dialogue":
				_ui.ShowDialogue(eff.Speaker, eff.Text);
				break;
			case "force_action":
				_ui.ForceAction(eff.Actor, eff.SkillId, eff.Target);
				ProcessNext();
				break;
			case "add_enemy":
				_ui.AddEnemies(eff.EnemyId, eff.Count);
				ProcessNext();
				break;
			case "add_ally":
				_ui.AddAlly(eff.Target);
				ProcessNext();
				break;
			case "apply_buff":
				_ui.ApplyBuff(eff.Target, eff.BuffId, eff.Duration);
				ProcessNext();
				break;
			case "grant_item":
				CombatLog.AddItem(eff.ItemId, eff.Amount);
				ProcessNext();
				break;
			case "set_var":
				CombatLog.SetVar(eff.VarName, eff.VarValue);
				ProcessNext();
				break;
			case "log_event":
				_ui.Log(eff.Text, "#ffcc44");
				ProcessNext();
				break;
		}
	}
}

public static class CombatLog
{
	static Dictionary<string, int> _items = new();
	static Dictionary<string, string> _vars = new();

	public static void AddItem(string id, int amount)
	{
		_items.TryGetValue(id, out int cur);
		_items[id] = cur + amount;
	}
	public static void SetVar(string key, string value) => _vars[key] = value;

	public static Dictionary<string, int> GetItems() => new(_items);
	public static Dictionary<string, string> GetVars() => new(_vars);
	public static void Clear() { _items.Clear(); _vars.Clear(); }
}
