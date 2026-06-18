namespace No1.Data;

public enum ItemType { Consumable, Equipment }
public enum EquipmentSlot { Weapon, Armor, Accessory }

public class ItemDef
{
	public string Id { get; }
	public string Name { get; }
	public string Description { get; }
	public ItemType Type { get; }
	public int Rarity { get; }
	public EquipmentSlot Slot { get; }
	public Dictionary<string, float> Modifiers { get; }
	public string Effect { get; }

	ItemDef(string id, string name, string desc, ItemType type, int rarity = 1,
		EquipmentSlot slot = EquipmentSlot.Weapon, Dictionary<string, float> modifiers = null,
		string effect = "")
	{
		Id = id; Name = name; Description = desc; Type = type; Rarity = rarity;
		Slot = slot; Modifiers = modifiers ?? new(); Effect = effect;
	}

	// ── Consumables ──────────────────────────────────

	public static readonly ItemDef SmallPotion = new("small_potion", "小型回复药",
		"恢复30点擦伤。", ItemType.Consumable, 1, effect: "heal_bruise:30");

	public static readonly ItemDef LargePotion = new("large_potion", "大型回复药",
		"恢复60点擦伤。", ItemType.Consumable, 2, effect: "heal_bruise:60");

	public static readonly ItemDef EnergyElixir = new("energy_elixir", "精力药",
		"恢复30点精力。", ItemType.Consumable, 2, effect: "restore_energy:30");

	public static readonly ItemDef FullHealHerb = new("full_heal_herb", "全愈草",
		"完全恢复生命与精力。", ItemType.Consumable, 4, effect: "heal_all");

	public static readonly ItemDef SevereHeal = new("severe_heal", "重伤药",
		"恢复10点重伤。", ItemType.Consumable, 3, effect: "heal_severe:10");

	// ── Weapons ──────────────────────────────────────

	public static readonly ItemDef IronSword = new("iron_sword", "铁剑",
		"力量 +2", ItemType.Equipment, 1, EquipmentSlot.Weapon,
		new() { ["power"] = 2f });

	public static readonly ItemDef SpiritStaff = new("spirit_staff", "灵杖",
		"心 +3", ItemType.Equipment, 2, EquipmentSlot.Weapon,
		new() { ["heart"] = 3f });

	public static readonly ItemDef FlameBlade = new("flame_blade", "炎刃",
		"力量 +3，暴击 +5", ItemType.Equipment, 3, EquipmentSlot.Weapon,
		new() { ["power"] = 3f, ["crit_rate"] = 5f });

	// ── Armor ────────────────────────────────────────

	public static readonly ItemDef LeatherArmor = new("leather_armor", "皮甲",
		"体 +2", ItemType.Equipment, 1, EquipmentSlot.Armor,
		new() { ["body"] = 2f });

	public static readonly ItemDef SilverMail = new("silver_mail", "银铠",
		"体 +2，物理减伤 +2", ItemType.Equipment, 2, EquipmentSlot.Armor,
		new() { ["body"] = 2f, ["def_flat"] = 2f });

	public static readonly ItemDef ShadowRobe = new("shadow_robe", "影衣",
		"敏 +3，闪避 +8", ItemType.Equipment, 3, EquipmentSlot.Armor,
		new() { ["agility"] = 3f, ["dodge"] = 8f });

	// ── Accessories ──────────────────────────────────

	public static readonly ItemDef LuckyCharm = new("lucky_charm", "幸运符",
		"运 +3", ItemType.Equipment, 1, EquipmentSlot.Accessory,
		new() { ["fortune"] = 3f });

	public static readonly ItemDef EnergyRing = new("energy_ring", "精力戒指",
		"精力上限 +10", ItemType.Equipment, 2, EquipmentSlot.Accessory,
		new() { ["energy_max"] = 10f });

	public static readonly ItemDef SpeedBoots = new("speed_boots", "疾风靴",
		"敏 +2，速度 +3", ItemType.Equipment, 2, EquipmentSlot.Accessory,
		new() { ["agility"] = 2f, ["speed"] = 3f });

	// ── Lookup ───────────────────────────────────────

	public static ItemDef Get(string id) => All.FirstOrDefault(i => i.Id == id);

	public static IReadOnlyList<ItemDef> All => new ItemDef[]
	{
		SmallPotion, LargePotion, EnergyElixir, FullHealHerb, SevereHeal,
		IronSword, SpiritStaff, FlameBlade,
		LeatherArmor, SilverMail, ShadowRobe,
		LuckyCharm, EnergyRing, SpeedBoots,
	};

	public static IReadOnlyList<ItemDef> Consumables => All.Where(i => i.Type == ItemType.Consumable).ToList();
	public static IReadOnlyList<ItemDef> Equipment => All.Where(i => i.Type == ItemType.Equipment).ToList();
}
