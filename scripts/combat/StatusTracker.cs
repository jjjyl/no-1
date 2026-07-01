namespace No1.Combat;

using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 运行时状态追踪器 — 挂在每个 CharacterStats 上，管理所有临时 buff/debuff。
/// 
/// 生命周期：
///   Apply(id, dur, src) → 对 owner 调用 ApplyModifier → 日志
///   TickRound()        → 全部 dur-- → 到期 Remove → 日志
///   Remove(id)         → 对 owner 调用 RemoveModifier → 日志
///   HasStatus(id)      → 条件检测
/// </summary>
public class StatusTracker
{
	public CharacterStats Owner { get; }
	public event System.Action<ActiveStatus, bool> OnStatusChanged; // (status, isApplied)

	List<ActiveStatus> _active = new();
	string _ownerName;

	public StatusTracker(CharacterStats owner, string ownerName = "?")
	{
		Owner = owner;
		_ownerName = ownerName;
	}

	// ── 查询 ──

	public bool HasStatus(string statusId) => _active.Any(s => s.StatusId == statusId);
	public IReadOnlyList<ActiveStatus> GetAllActive() => _active.AsReadOnly();
	public int Count => _active.Count;

	// ── 应用 ──

	/// <param name="statusId">状态定义 ID</param>
	/// <param name="duration">持续回合数（-1=永久）</param>
	/// <param name="source">来源标识（如 "古代巨龙"），用于日志</param>
	/// <returns>创建的 ActiveStatus，失败返回 null</returns>
	public ActiveStatus Apply(string statusId, int duration, string source = "")
	{
		var def = StatusDef.Get(statusId);
		if (def == null)
		{
			GD.PrintErr($"[StatusTracker] Unknown status: '{statusId}' on {_ownerName}");
			return null;
		}

		// 已存在的同 ID 状态：刷新 duration（取较大值）
		var existing = _active.FirstOrDefault(s => s.StatusId == statusId);
		if (existing != null)
		{
			existing.Duration = System.Math.Max(existing.Duration, duration);
			GD.Print($"[StatusTracker] {_ownerName}: 刷新 [{statusId}] {def.Name} → dur={existing.Duration}");
			OnStatusChanged?.Invoke(existing, true);
			return existing;
		}

		var active = new ActiveStatus
		{
			StatusId   = statusId,
			Name       = def.Name,
			Type       = def.Type,
			Duration   = duration,
			Source     = source,
			TickDamage = def.TickDamage,
			Effects    = def.Effects,
		};

		// 应用于 CharacterStats
		foreach (var (key, val) in def.Effects)
			Owner.ApplyModifier(key, val);

		_active.Add(active);

		string perm = duration < 0 ? " (永久)" : "";
		GD.Print($"[StatusTracker] {_ownerName}: + [{statusId}] {def.Name} ({def.Type}) dur={duration}{perm} src={source}");
		OnStatusChanged?.Invoke(active, true);
		return active;
	}

	// ── 移除 ──

	public void Remove(string statusId)
	{
		var active = _active.FirstOrDefault(s => s.StatusId == statusId);
		if (active == null) return;

		// 回退 CharacterStats 修改
		foreach (var (key, val) in active.Effects)
			Owner.RemoveModifier(key, val);

		_active.Remove(active);
		GD.Print($"[StatusTracker] {_ownerName}: - [{statusId}] {active.Name} 已移除");
		OnStatusChanged?.Invoke(active, false);
	}

	public void RemoveAll()
	{
		foreach (var s in _active.ToList())
			Remove(s.StatusId);
	}

	// ── 回合推进 ──

	/// <summary>回合末调用。倒计时 → 过期移除 → tick 伤害。</summary>
	/// <returns>本回合过期或 tick 的事件列表 (status, reason: "expired"|"tick")</returns>
	public List<(ActiveStatus Status, string Reason)> TickRound()
	{
		var events = new List<(ActiveStatus, string)>();

		var expired = _active.Where(s => s.Duration > 0).ToList();
		foreach (var s in expired)
		{
			s.Duration--;
			GD.Print($"  [StatusTracker.Tick] {_ownerName}: [{s.StatusId}] {s.Name} dur={s.Duration}");

			if (s.Duration <= 0)
			{
				events.Add((s, "expired"));
			}
		}

		// 先移除所有过期状态
		foreach (var (s, reason) in events)
			if (reason == "expired")
				Remove(s.StatusId);

		// Tick 伤害 — 固定伤害，不经过闪避/防御
		foreach (var s in _active.Where(a => a.TickDamage > 0).ToList())
		{
			Owner.TakeDirectDamage(s.TickDamage);
			GD.Print($"[StatusTracker.Tick] {_ownerName}: [{s.StatusId}] {s.Name} tick → {s.TickDamage} 直接伤害");
			events.Add((s, "tick"));
		}

		return events;
	}

	// ── 内部类 ──

	public class ActiveStatus
	{
		public string StatusId;
		public string Name;
		public string Type;        // "buff" | "debuff"
		public int Duration;       // -1 = 永久
		public string Source;
		public int TickDamage;
		public Dictionary<string, int> Effects = new();
	}
}
