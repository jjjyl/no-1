namespace No1.Core;

using Godot;
using No1.Data;
using No1.UI;

public class EventManager
{
	// ── 数据模型 ──

	public class EventLayer
	{
		public string Level;     // shallow | middle | deep
		public string Name;      // 层级名称，如"远处目睹"
		public Godot.Collections.Array Locks;        // ["info", "fragment", "companion", ...]
		public Godot.Collections.Dictionary Conditions;
		public Godot.Collections.Array Effects;
		public Godot.Collections.Array ShrineUnlocks; // 神殿解锁 ID 列表
	}

	public class EventDef
	{
		public string Id;
		public string Name;      // 显示名 "巨龙的威胁"
		public string Category;  // world | major_conditional | minor_conditional | milestone | finale
		public string Location;
		public bool OneShot;
		public int Priority = 100;

		// 扁平模式（无 layers 时使用）
		public Godot.Collections.Dictionary Conditions;
		public Godot.Collections.Array Effects;

		// 分层模式
		public Godot.Collections.Array Layers;  // EventLayer[]
	}

	// ── 状态 ──

	static List<EventDef> _events;
	static HashSet<string> _triggeredThisCycle = new();
	static HashSet<string> _triggeredEver = new();    // 跨轮回全局触发记录（含层级）

	const string FLAG_PREFIX_EVENT = "event:";
	const string FLAG_PREFIX_LAYER = "layer:";

	// ── 加载 ──

	public static void Load()
	{
		_events = new();
		GD.Print("[EventManager] Loading events.json...");

		var file = FileAccess.Open("res://assets/data/events.json", FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PrintErr("[EventManager] FAILED to open events.json!");
			return;
		}

		var jsonText = file.GetAsText();
		GD.Print($"[EventManager] events.json loaded, {jsonText.Length} chars");

		var parseResult = Json.ParseString(jsonText);
		if (parseResult.VariantType == Variant.Type.Nil)
		{
			GD.PrintErr("[EventManager] JSON parse returned null!");
			return;
		}

		var dict = parseResult.AsGodotDictionary();
		if (!dict.ContainsKey("events"))
		{
			GD.PrintErr("[EventManager] JSON missing 'events' key!");
			return;
		}

		var eventsArray = dict["events"].AsGodotArray();
		GD.Print($"[EventManager] Found {eventsArray.Count} event entries");

		foreach (var item in eventsArray)
		{
			var d = item.AsGodotDictionary();
			var id = GetStr(d, "id");
			var hasLayers = d.ContainsKey("layers");

			var ev = new EventDef
			{
				Id       = id,
				Name     = GetStr(d, "name"),
				Category = GetStr(d, "category"),
				Location = GetStr(d, "location"),
				OneShot  = d.ContainsKey("one_shot") && d["one_shot"].AsBool(),
				Priority = d.ContainsKey("priority") ? d["priority"].AsInt32() : 100,
			};

			// 分层 or 扁平
			if (hasLayers)
			{
				ev.Layers = d["layers"].AsGodotArray();
				GD.Print($"  [{id}] layered ({ev.Layers.Count} layers)");
			}
			else
			{
				if (d.ContainsKey("conditions")) ev.Conditions = d["conditions"].AsGodotDictionary();
				if (d.ContainsKey("effects"))    ev.Effects    = d["effects"].AsGodotArray();
				GD.Print($"  [{id}] flat, location={ev.Location}, priority={ev.Priority}");
			}

			_events.Add(ev);
		}

		GD.Print($"[EventManager] Loaded {_events.Count} events total");
	}

	static string GetStr(Godot.Collections.Dictionary d, string key) =>
		d.ContainsKey(key) ? d[key].AsString() : "";

	static Godot.Collections.Dictionary AsDict(Variant v) => v.AsGodotDictionary();

	// ── 周期重置 ──

	public static void ResetCycle() => _triggeredThisCycle.Clear();

	// ── 主入口 ──

	/// <param name="onStatus">地图状态栏回调</param>
	public static void CheckEvents(string location, CycleManager cm, Action<string> onStatus)
	{
		GD.Print($"[EventManager] CheckEvents at '{location}' — {_events.Count} events loaded, cycle={cm.CurrentCycle}");

		int checked_ = 0, triggered_ = 0;
		foreach (var e in _events.OrderByDescending(e => e.Priority))
		{
			checked_++;
			// 地点匹配
			if (e.Location != "*" && e.Location != location)
			{
				GD.Print($"  [{e.Id}] SKIP: location '{e.Location}' != '{location}'");
				continue;
			}

			// 一次性事件
			if (e.OneShot && _triggeredThisCycle.Contains(e.Id))
			{
				GD.Print($"  [{e.Id}] SKIP: already triggered this cycle");
				continue;
			}

			if (e.Layers != null && e.Layers.Count > 0)
			{
				// ── 分层事件 ──
				GD.Print($"  [{e.Id}] checking {e.Layers.Count} layers...");
				var layer = SelectDeepestLayer(e, cm);
				if (layer == null)
				{
					GD.Print($"  [{e.Id}] SKIP: no layer passed");
					continue;
				}

				GD.Print($"[EventManager] {e.Id} ({e.Name}) → layer:{GetStr(layer, "level")} ({GetStr(layer, "name")})");
				if (e.OneShot) _triggeredThisCycle.Add(e.Id);

				var effects = layer.ContainsKey("effects") ? layer["effects"].AsGodotArray() : new Godot.Collections.Array();
				GD.Print($"    executing {effects.Count} effects");
				ExecuteEffects(effects, cm, onStatus);

				// Process shrine unlocks
				if (layer.ContainsKey("shrine_unlocks"))
				{
					var unlocks = layer["shrine_unlocks"].AsGodotArray();
					var inv = cm.PlayerInventory;
					if (inv != null)
					{
						foreach (var unlockItem in unlocks)
						{
							var unlockId = unlockItem.AsString();
							ItemDef def;
							try { def = ItemDef.Get(unlockId); }
							catch (ArgumentException) { def = null; }

							if (def != null)
							{
								inv.AddItem(unlockId, 1);
								onStatus?.Invoke($"神殿解锁: {def.Name}");
							}
							else
							{
								cm.SetAccountFlag($"shrine:{unlockId}");
								GD.Print($"[EventManager] Shrine unlocked: {unlockId} (set account flag)");
							}
						}
					}
				}

				triggered_++;
				// 标记层级完成
				var levelId = $"{FLAG_PREFIX_LAYER}{e.Id}:{GetStr(layer, "level")}";
				cm.SetFlag(levelId);
				_triggeredEver.Add(levelId);
			}
			else if (e.Conditions != null)
			{
				// ── 扁平事件（兼容旧格式）──
				GD.Print($"  [{e.Id}] checking flat conditions...");
				if (!CheckConditions(e.Conditions, cm))
				{
					GD.Print($"  [{e.Id}] SKIP: conditions failed");
					continue;
				}

				GD.Print($"[EventManager] {e.Id} TRIGGERED at {location}");
				if (e.OneShot) _triggeredThisCycle.Add(e.Id);

				var fx = e.Effects ?? new();
				GD.Print($"    executing {fx.Count} effects");
				ExecuteEffects(fx, cm, onStatus);
				cm.SetFlag($"{FLAG_PREFIX_EVENT}{e.Id}");
				triggered_++;
			}
		}

		GD.Print($"[EventManager] CheckEvents done: checked {checked_}, triggered {triggered_}");
	}

	// ── 层级选择：取最深满足条件的层 ──

	static Godot.Collections.Dictionary SelectDeepestLayer(EventDef ev, CycleManager cm)
	{
		Godot.Collections.Dictionary best = null;
		int bestDepth = -1;

		var depthOrder = new Dictionary<string, int> { ["shallow"] = 0, ["middle"] = 1, ["deep"] = 2 };

		foreach (var item in ev.Layers)
		{
			var layer = item.AsGodotDictionary();
			if (!layer.ContainsKey("conditions")) continue;

			// 层级 one_shot 检查
			var layerId = $"{FLAG_PREFIX_LAYER}{ev.Id}:{GetStr(layer, "level")}";
			if (layer.ContainsKey("one_shot") && layer["one_shot"].AsBool() && cm.HasFlag(layerId))
			{
				GD.Print($"  [{ev.Id}] layer:{GetStr(layer, "level")} SKIP: one_shot already triggered this cycle");
				continue;
			}

			var conds = layer["conditions"].AsGodotDictionary();
			var locks = layer.ContainsKey("locks") ? layer["locks"].AsGodotArray() : null;

			// 锁检查
			if (locks != null && !CheckLocks(locks, cm))
			{
				GD.Print($"  [{ev.Id}] layer:{GetStr(layer, "level")} SKIP: locks failed");
				continue;
			}

			if (!CheckConditions(conds, cm))
			{
				GD.Print($"  [{ev.Id}] layer:{GetStr(layer, "level")} SKIP: conditions failed");
				continue;
			}

			var level = GetStr(layer, "level");
			if (depthOrder.TryGetValue(level, out var d) && d > bestDepth)
			{
				best = layer;
				bestDepth = d;
			}
		}

		if (best != null)
		{
			GD.Print($"  [{ev.Id}] selected layer: {GetStr(best, "level")}");
		}

		return best;
	}

	// ── 锁检查 ──

	static bool CheckLocks(Godot.Collections.Array locks, CycleManager cm)
	{
		foreach (var item in locks)
		{
			var lockType = item.AsString();
			switch (lockType)
			{
				case "companion":
					// 需要任意同伴 → 检查 ActiveCompanions 非空
					if (cm.ActiveCompanions.Count == 0) return false;
					break;
				case "fragment":
					// 需要相关碎片 → 当前用 flag 代理
					// 具体碎片 ID 由 layer conditions 里的 has_fragment 检查
					break;
				case "info":
					// 信息锁 → 由 layer conditions 里的 flag_set 检查
					break;
				case "prerequisite":
					// 前置锁 → 由 layer conditions 里的 event_triggered 检查
					break;
				case "item":
					// 持有物锁 → 由 layer conditions 里的 has_item 检查
					break;
				// "none" or empty → pass
			}
		}
		return true;
	}

	// ── 条件检查 ──

	static bool CheckConditions(Godot.Collections.Dictionary conds, CycleManager cm)
	{
		foreach (string key in conds.Keys)
		{
			var val = conds[key];
			switch (key)
			{
				case "companion_not_in_party":
					if (cm.ActiveCompanions.Any(c => c.Name == val.AsString())) return false;
					break;
				case "companion_in_party":
					if (!cm.ActiveCompanions.Any(c => c.Name == val.AsString())) return false;
					break;
				case "min_cycle":
					if (cm.CurrentCycle < val.AsInt32()) return false;
					break;
				case "min_favor":
					var fd = val.AsGodotDictionary();
					var comp = cm.ActiveCompanions.FirstOrDefault(c => c.Name == fd["companion"].AsString());
					if (comp == null || comp.Favor < fd["value"].AsInt32()) return false;
					break;
				case "player_hp_below_pct":
					float max = cm.PlayerStats.MaxBruiseHP + cm.PlayerStats.MaxSevereHP;
					float cur = cm.PlayerStats.BruiseHP + cm.PlayerStats.SevereHP;
					if (cur / max >= val.AsSingle() / 100f) return false;
					break;
				case "flag_set":
					if (!cm.HasFlag(val.AsString())) return false;
					break;
				case "flag_not_set":
					if (cm.HasFlag(val.AsString())) return false;
					break;
				case "account_flag_set":
					if (!cm.HasAccountFlag(val.AsString())) return false;
					break;
				case "account_flag_not_set":
					if (cm.HasAccountFlag(val.AsString())) return false;
					break;
				case "event_triggered":
				{
					// 检查某事件/层级是否已完成（跨轮回）
					var eventId = val.AsString();
					if (!_triggeredEver.Contains(eventId) && !cm.HasFlag(eventId))
						return false;
					break;
				}
				case "has_fragment":
				{
					// 检查是否收集了某碎片（当前用 flag 代理）
					if (!cm.HasFlag($"fragment:{val.AsString()}"))
						return false;
					break;
				}
				case "has_item":
				{
					// 检查是否持有某物品
					var inv = cm.PlayerInventory;
					if (inv == null || !inv.HasItem(val.AsString()))
						return false;
					break;
				}
				case "random":
					float rate = val.AsSingle();
					if (conds.ContainsKey("blessing_mod"))
						rate *= 1f + cm.GetBlessingModifier(conds["blessing_mod"].AsString());
					if (GD.RandRange(0f, 1f) >= rate) return false;
					break;
			}
		}
		return true;
	}

	// ── 效果执行 ──

	static void ExecuteEffects(Godot.Collections.Array effects, CycleManager cm, Action<string> onStatus)
	{
		foreach (var item in effects)
		{
			var e = item.AsGodotDictionary();
			var type = GetStr(e, "type");
			switch (type)
			{
				case "companion_join":
					cm.JoinCompanion(e["name"].AsString());
					break;
				case "companion_leave":
					var leaveName = e["name"].AsString();
					var toRemove = cm.ActiveCompanions.FirstOrDefault(c => c.Name == leaveName);
					if (toRemove != null) cm.ActiveCompanions.Remove(toRemove);
					break;
				case "combat":
					cm.PendingEnemyScene = e["enemy"].AsString();
					break;
				case "set_flag":
					cm.SetFlag(e["flag"].AsString());
					break;
				case "set_account_flag":
					cm.SetAccountFlag(e["flag"].AsString());
					break;
				case "heal_player":
					cm.PlayerStats.BruiseHP = Math.Min(
						cm.PlayerStats.BruiseHP + e["amount"].AsInt32(),
						cm.PlayerStats.MaxBruiseHP);
					break;
				case "message":
				{
					var speaker = e.ContainsKey("speaker") ? e["speaker"].AsString() : "";
					var text = GetStr(e, "text");
					var pos = e.ContainsKey("position") ? e["position"].AsString() : "top";
					var autoHide = e.ContainsKey("auto_hide") ? e["auto_hide"].AsSingle() : 0f;
					DialogueManager.Instance.ShowBanner(speaker, text, pos, autoHide);
					break;
				}
				case "dialogue":
				{
					var spk = e.ContainsKey("speaker") ? e["speaker"].AsString() : "";
					var txt = GetStr(e, "text");
					var dlgPos = e.ContainsKey("position") ? e["position"].AsString() : "center";
					var expr = e.ContainsKey("expression") ? e["expression"].AsString() : null;
					var entry = e.ContainsKey("entry") ? e["entry"].AsString() : "slide_bottom";
					var fx = e.ContainsKey("effect") ? e["effect"].AsString() : null;
					DialogueManager.Instance.ShowFullDialogue(spk, txt, dlgPos, expr, entry, fx);
					break;
				}
				case "status":
				{
					onStatus?.Invoke(GetStr(e, "text"));
					break;
				}
				case "grant_item":
				{
					var itemId = GetStr(e, "item_id");
					var amount = e.ContainsKey("amount") ? e["amount"].AsInt32() : 1;
					var def = ItemDef.Get(itemId);
					var itemName = def?.Name ?? itemId;

					// Read optional target field — if not specified, defaults to player
					if (e.ContainsKey("target") && !string.IsNullOrEmpty(e["target"].AsString()))
					{
						var targetName = e["target"].AsString();
						var comp = cm.ActiveCompanions.FirstOrDefault(c => c.Name == targetName);
						if (comp != null && comp.Inventory != null)
						{
							comp.Inventory.AddItem(itemId, amount);
							onStatus?.Invoke($"[color=#ffcc44]{targetName}获得 {itemName} ×{amount}[/color]");
						}
						else
						{
							// Companion not found or no inventory — fall back to player
							GD.PrintErr($"[EventManager] grant_item target '{targetName}' not found, giving to player");
							cm.PlayerInventory?.AddItem(itemId, amount);
							onStatus?.Invoke($"[color=#ffcc44]获得 {itemName} ×{amount}[/color]");
						}
					}
					else
					{
						// Default: give to player (backward compatible)
						cm.PlayerInventory?.AddItem(itemId, amount);
						onStatus?.Invoke($"[color=#ffcc44]获得 {itemName} ×{amount}[/color]");
					}
					break;
				}
			}
		}
	}
}
