namespace No1.Combat;

using System.Collections.Generic;

public class TriggerDef
{
	public string Type;       // on_skill_used, on_damage_dealt, on_hp_below, on_state_applied, on_state_removed, etc.
	public string Source;     // "player", "any", name
	public string Skill;      // skill id
	public string Target;     // target name
	public string StatusId;   // status id (for on_state_applied / on_state_removed)
	public string State;      // state name
	public int MinDamage;     // minimum damage threshold
	public float Threshold;   // hp percentage threshold
	public int Round;         // round number
}

public class ConditionDef
{
	public string Type;       // hp_below, hp_above, favor_above, has_status, alive, enemy_count, round
	public string Target;     // character name
	public string StatusId;   // status id (for has_status)
	public string State;      // state name
	public int Value;         // integer value
	public float ValueF;      // float value
	public string Op = "eq";  // eq, lt, lte, gt, gte
}

public class EffectDef
{
	public string Type;       // show_dialogue, force_action, add_enemy, add_ally, apply_buff, grant_item, set_var, log_event
	public string Actor;      // who performs the forced action
	public string Target;     // target of the effect
	public string SkillId;    // skill id for force_action
	public string Speaker;    // dialogue speaker name
	public string Text;       // dialogue text or log text
	public string BuffId;     // buff id for apply_buff
	public int Duration;      // buff duration in rounds
	public string EnemyId;    // enemy scene path for add_enemy
	public int Count;         // count for add_enemy
	public string ItemId;     // item id for grant_item
	public int Amount = 1;    // amount for grant_item
	public string VarName;    // variable name for set_var
	public string VarValue;   // variable value for set_var
}

public class EventDef
{
	public string Id;
	public TriggerDef Trigger;
	public List<ConditionDef> Conditions = new();
	public List<EffectDef> Effects = new();
	public bool Once = true;
}

public class EventBundle
{
	public List<EventDef> Events = new();
}

public class FireContext
{
	public string Source;      // "player", companion name, enemy display name
	public string Target;      // same
	public string SkillId;     // skill id
	public string StatusId;    // status id (for on_state_applied / on_state_removed)
	public int Damage;         // damage dealt
	public float HpPct;        // 0-1 hp percentage
	public string StateName;   // state name
	public int Round;          // current round number
}
