namespace No1.Core;

using Godot;
using No1.Data;

public class Inventory
{
    Dictionary<string, int> _items = new();           // itemId -> count
    Dictionary<EquipmentSlot, string> _equipped = new(); // slot -> itemId
    CharacterStats _player;

    public Inventory(CharacterStats player) { _player = player; }

    // ── Basic inventory ops ──

    public void AddItem(string itemId, int amount = 1)
    {
        if (amount <= 0) return;
        _items.TryGetValue(itemId, out int cur);
        _items[itemId] = cur + amount;
    }

    public bool RemoveItem(string itemId, int amount = 1)
    {
        if (!_items.TryGetValue(itemId, out int cur) || cur < amount) return false;
        _items[itemId] = cur - amount;
        if (_items[itemId] <= 0) _items.Remove(itemId);
        return true;
    }

    public int GetCount(string itemId) => _items.TryGetValue(itemId, out int c) ? c : 0;
    public bool HasItem(string itemId) => GetCount(itemId) > 0;
    public IReadOnlyList<string> GetAllItemIds() => _items.Keys.ToList();

    // ── Consumable usage ──
    // Returns null on success, error string on failure

    public string UseItem(string itemId) => UseItemOn(itemId, _player);

    public string UseItemOn(string itemId, CharacterStats target)
    {
        var def = ItemDef.Get(itemId);
        if (def == null) return "未知物品";
        if (def.Type != ItemType.Consumable) return "该物品无法使用";
        if (!HasItem(itemId)) return "物品不足";
        if (target == null) return "无效目标";

        var parts = def.Effect.Split(':', 2);
        var effType = parts[0];
        int amount = parts.Length > 1 && int.TryParse(parts[1], out var a) ? a : 0;

        switch (effType)
        {
            case "heal_bruise":
                target.BruiseHP = Math.Min(target.BruiseHP + amount, target.MaxBruiseHP);
                break;
            case "heal_severe":
                target.SevereHP = Math.Min(target.SevereHP + amount, target.MaxSevereHP);
                break;
            case "restore_energy":
                target.Energy = Math.Min(target.Energy + amount, target.EnergyMax);
                break;
            case "heal_all":
                target.FullHeal();
                break;
            default:
                return $"未知效果: {effType}";
        }

        RemoveItem(itemId, 1);
        return null;
    }

    // ── Equipment ──
    // Returns null on success, error string on failure

    public string EquipItem(string itemId)
    {
        var def = ItemDef.Get(itemId);
        if (def == null) return "未知物品";
        if (def.Type != ItemType.Equipment) return "该物品无法装备";
        if (!HasItem(itemId)) return "物品不足";

        // If slot occupied, unequip first
        if (_equipped.ContainsKey(def.Slot))
        {
            var oldId = _equipped[def.Slot];
            UnequipItem(def.Slot);  // puts old item back in inventory
        }

        // Apply modifiers
        foreach (var (key, val) in def.Modifiers)
            _player.ApplyModifier(key, val);

        _equipped[def.Slot] = itemId;
        RemoveItem(itemId, 1);
        return null;
    }

    public string UnequipItem(EquipmentSlot slot)
    {
        if (!_equipped.TryGetValue(slot, out var itemId)) return "该栏位无装备";

        var def = ItemDef.Get(itemId);
        if (def != null)
        {
            foreach (var (key, val) in def.Modifiers)
                _player.RemoveModifier(key, val);
        }

        _equipped.Remove(slot);
        AddItem(itemId, 1);  // put back in inventory
        return null;
    }

    public string GetEquipped(EquipmentSlot slot) => _equipped.TryGetValue(slot, out var id) ? id : null;
    public bool IsEquipped(string itemId) => _equipped.Values.Contains(itemId);
    public IReadOnlyDictionary<EquipmentSlot, string> GetEquippedAll() => new Dictionary<EquipmentSlot, string>(_equipped);

    // Re-apply all equipped item modifiers to the current player (called after stats reset)
    public void ReapplyEquipmentModifiers()
    {
        foreach (var (slot, itemId) in _equipped)
        {
            var def = ItemDef.Get(itemId);
            if (def != null)
                foreach (var (key, val) in def.Modifiers)
                    _player.ApplyModifier(key, val);
        }
    }

    // ── Serialization ──

    public Godot.Collections.Dictionary Serialize()
    {
        var itemDict = new Godot.Collections.Dictionary();
        foreach (var (id, count) in _items)
            itemDict[id] = count;

        var equipDict = new Godot.Collections.Dictionary();
        foreach (var (slot, id) in _equipped)
            equipDict[slot.ToString().ToLower()] = id;

        return new Godot.Collections.Dictionary
        {
            ["items"] = itemDict,
            ["equipped"] = equipDict,
        };
    }

    public void Deserialize(Godot.Collections.Dictionary dict)
    {
        _items.Clear();
        _equipped.Clear();

        if (dict.ContainsKey("items"))
        {
            foreach (var key in dict["items"].AsGodotDictionary().Keys)
            {
                var k = key.AsString();
                _items[k] = dict["items"].AsGodotDictionary()[key].AsInt32();
            }
        }

        if (dict.ContainsKey("equipped"))
        {
            foreach (var key in dict["equipped"].AsGodotDictionary().Keys)
            {
                var slotStr = key.AsString();
                var id = dict["equipped"].AsGodotDictionary()[key].AsString();
                if (Enum.TryParse<EquipmentSlot>(slotStr, true, out var slot))
                {
                    _equipped[slot] = id;
                    // Re-apply modifiers on load
                    var def = ItemDef.Get(id);
                    if (def != null)
                        foreach (var (k, val) in def.Modifiers)
                            _player.ApplyModifier(k, val);
                }
            }
        }
    }
}
