# 敌人独立文件夹规划

> 日期：2026-06-23 | 目标：将敌人从 `scenes/characters/` 分离到独立目录，建立数据驱动管理

---

## 一、目标结构

```
worldSee/
├── scenes/
│   ├── characters/          # 仅保留同伴/玩家角色
│   │   ├── ivy.tscn
│   │   └── (未来的 VRM 角色)
│   │
│   └── enemies/             # 🆕 敌人独立目录
│       ├── base_mob.tscn    # 从 characters/ 移入
│       ├── forest_beast.tscn
│       ├── mine_slug.tscn
│       ├── cliff_hawk.tscn
│       ├── ancient_warrior.tscn
│       ├── crystal_warden.tscn
│       ├── wasteland_hunter.tscn
│       ├── tower_guardian.tscn
│       └── spring_lurker.tscn
│
├── assets/
│   └── data/
│       └── enemies.json     # 🆕 敌人数据定义（镜像 companions.json）
│
├── scripts/
│   ├── core/
│   │   └── EnemyState.cs    # 🆕 敌人注册类（镜像 CompanionState.cs）
│   └── ...
```

---

## 二、需要创建的文件

### 2.1 敌人四大分类

| 分类 | 标识 | 数值 | 出现方式 | 示例 |
|------|------|------|----------|------|
| **一般怪物** `normal` | 普通遇敌 | **运行时浮动**（min~max 随机） | 通用遇敌 `random` 条件触发 | 林间兽、矿道蛞蝓 |
| **精英怪物** `elite` | 更强变体 | 一般怪物的 **1.3× 倍率** + 浮动 | 条件事件、高周目遇敌 | 精英崖鹰、精英荒原猎手 |
| **Boss** `boss` | 固定 Boss | **固定值**（不浮动） | 多层事件的 middle/deep 层 | 符文守卫、古代巨龙 |
| **特殊怪物** `special` | 机制型 | **固定值** | 特定叙事事件 | 菲利克斯的实验体、神秘巨像 |

**核心设计**：
- 一般怪物和精英怪物用 `min`~`max` 范围，`EnemyState.SpawnStats()` 每次随机
- 精英怪物 = 从普通池中提升 `category: "elite"` 并乘 1.3 倍率
- Boss 和特殊怪物直接定值，保证叙事战斗的一致性

### 2.2 `assets/data/enemies.json` — 敌人数据定义

```json
{
  "enemies": [
    {
      "id": "base_mob",
      "name": "基础魔种",
      "category": "normal",
      "desc": "普通的魔粒聚合体，随处可见的威胁。",
      "scene": "res://scenes/enemies/base_mob.tscn",
      "zone": "*",
      "rarity": 1,
      "stats": {
        "power":   { "min": 3, "max": 5 },
        "body":    { "min": 2, "max": 4 },
        "agility": { "min": 3, "max": 5 },
        "heart":   { "min": 1, "max": 3 },
        "fortune": { "min": 1, "max": 3 }
      },
      "hp": {
        "bruise": { "min": 12, "max": 18 },
        "severe": { "min": 10, "max": 18 }
      },
      "speed": { "min": 5, "max": 7 }
    },
    {
      "id": "forest_beast",
      "name": "林间兽",
      "category": "normal",
      "desc": "在林地边缘游荡的魔化野兽，速度极快但脆弱。",
      "scene": "res://scenes/enemies/forest_beast.tscn",
      "zone": "林地边缘",
      "rarity": 1,
      "stats": {
        "power":   { "min": 4, "max": 6 },
        "body":    { "min": 1, "max": 3 },
        "agility": { "min": 6, "max": 8 },
        "heart":   { "min": 1, "max": 2 },
        "fortune": { "min": 2, "max": 4 }
      },
      "hp": {
        "bruise": { "min": 9, "max": 14 },
        "severe": { "min": 5, "max": 10 }
      },
      "speed": { "min": 7, "max": 9 }
    },
    {
      "id": "mine_slug",
      "name": "矿道蛞蝓",
      "category": "normal",
      "desc": "吸附在废矿壁上的巨大软体魔种，防御力高但行动迟缓。",
      "scene": "res://scenes/enemies/mine_slug.tscn",
      "zone": "废矿入口",
      "rarity": 1,
      "stats": {
        "power":   { "min": 2, "max": 4 },
        "body":    { "min": 5, "max": 7 },
        "agility": { "min": 1, "max": 3 },
        "heart":   { "min": 1, "max": 3 },
        "fortune": { "min": 1, "max": 2 }
      },
      "hp": {
        "bruise": { "min": 18, "max": 24 },
        "severe": { "min": 8, "max": 14 }
      },
      "speed": { "min": 3, "max": 5 }
    },
    {
      "id": "cliff_hawk",
      "name": "崖鹰",
      "category": "normal",
      "desc": "在断崖台地盘旋的魔化猛禽，闪避率极高。",
      "scene": "res://scenes/enemies/cliff_hawk.tscn",
      "zone": "断崖台地",
      "rarity": 2,
      "stats": {
        "power":   { "min": 3, "max": 5 },
        "body":    { "min": 1, "max": 3 },
        "agility": { "min": 7, "max": 9 },
        "heart":   { "min": 1, "max": 3 },
        "fortune": { "min": 3, "max": 5 }
      },
      "hp": {
        "bruise": { "min": 8, "max": 12 },
        "severe": { "min": 5, "max": 10 }
      },
      "speed": { "min": 8, "max": 10 }
    },
    {
      "id": "ancient_warrior",
      "name": "亡灵战士",
      "category": "normal",
      "desc": "从古战场残骸中重聚的古代士兵，平衡而坚韧。",
      "scene": "res://scenes/enemies/ancient_warrior.tscn",
      "zone": "古战场",
      "rarity": 2,
      "stats": {
        "power":   { "min": 4, "max": 6 },
        "body":    { "min": 3, "max": 5 },
        "agility": { "min": 3, "max": 5 },
        "heart":   { "min": 2, "max": 4 },
        "fortune": { "min": 2, "max": 4 }
      },
      "hp": {
        "bruise": { "min": 15, "max": 20 },
        "severe": { "min": 10, "max": 14 }
      },
      "speed": { "min": 4, "max": 6 }
    },
    {
      "id": "crystal_warden",
      "name": "晶石守卫",
      "category": "normal",
      "desc": "由结晶洞穴的晶石活化而成的守卫，擅长精神攻击。",
      "scene": "res://scenes/enemies/crystal_warden.tscn",
      "zone": "结晶洞穴",
      "rarity": 2,
      "stats": {
        "power":   { "min": 2, "max": 4 },
        "body":    { "min": 2, "max": 4 },
        "agility": { "min": 2, "max": 4 },
        "heart":   { "min": 6, "max": 8 },
        "fortune": { "min": 3, "max": 5 }
      },
      "hp": {
        "bruise": { "min": 12, "max": 16 },
        "severe": { "min": 10, "max": 16 }
      },
      "speed": { "min": 4, "max": 6 }
    },
    {
      "id": "wasteland_hunter",
      "name": "荒原猎手",
      "category": "normal",
      "desc": "在荒原边缘徘徊的巨型捕食者，攻击力极高。",
      "scene": "res://scenes/enemies/wasteland_hunter.tscn",
      "zone": "荒原边缘",
      "rarity": 3,
      "stats": {
        "power":   { "min": 6, "max": 9 },
        "body":    { "min": 3, "max": 5 },
        "agility": { "min": 2, "max": 4 },
        "heart":   { "min": 1, "max": 3 },
        "fortune": { "min": 2, "max": 4 }
      },
      "hp": {
        "bruise": { "min": 12, "max": 18 },
        "severe": { "min": 8, "max": 14 }
      },
      "speed": { "min": 4, "max": 6 }
    },
    {
      "id": "spring_lurker",
      "name": "泉底潜伏者",
      "category": "normal",
      "desc": "藏在圣泉底部的魔种，能在净化之力中生存说明它不一般。",
      "scene": "res://scenes/enemies/spring_lurker.tscn",
      "zone": "圣泉",
      "rarity": 3,
      "stats": {
        "power":   { "min": 4, "max": 6 },
        "body":    { "min": 2, "max": 4 },
        "agility": { "min": 5, "max": 7 },
        "heart":   { "min": 3, "max": 5 },
        "fortune": { "min": 3, "max": 5 }
      },
      "hp": {
        "bruise": { "min": 12, "max": 16 },
        "severe": { "min": 8, "max": 12 }
      },
      "speed": { "min": 6, "max": 8 }
    },
    {
      "id": "tower_guardian",
      "name": "符文守卫",
      "category": "boss",
      "desc": "忘却之塔的守护者——由古代符文构成的活体机关。",
      "scene": "res://scenes/enemies/tower_guardian.tscn",
      "zone": "忘却之塔",
      "rarity": 4,
      "stats": {
        "power": 6, "body": 6, "agility": 3, "heart": 5, "fortune": 3
      },
      "hp": {
        "bruise": 25, "severe": 20
      },
      "speed": 4
    },
    {
      "id": "ancient_dragon",
      "name": "古代巨龙",
      "category": "boss",
      "desc": "世界痛苦的化身——不是敌人，是旧时代遗留下来的记忆之躯。",
      "scene": "res://scenes/enemies/ancient_dragon.tscn",
      "zone": "*",
      "rarity": 5,
      "stats": {
        "power": 12, "body": 10, "agility": 3, "heart": 8, "fortune": 5
      },
      "hp": {
        "bruise": 40, "severe": 30
      },
      "speed": 3
    }
  ]
}
```

**字段说明**：
- 一般/精英敌人：`stats` 和 `hp` 使用 `{ "min": N, "max": M }` 范围，`speed` 同理
- Boss/特殊敌人：`stats`、`hp`、`speed` 使用定值
- `category` 决定 `EnemyState.SpawnStats()` 的行为：
  - `"normal"` → RNG 在每个范围内随机
  - `"elite"` → 从 normal 池提权后 ×1.3
  - `"boss"` / `"special"` → 返回固定值

总计 **10 种敌人**：8 个 normal + 2 个 boss。不额外定义 elite 条目——精英是运行时从普通敌人升级生成的。

### 2.3 `scripts/core/EnemyState.cs` — 敌人注册类

镜像 `CompanionState.cs` 的设计模式，增加分类和运行时浮动：

```csharp
namespace No1.Core;

using Godot;
using System.Collections.Generic;

public class EnemyState
{
    // ── 静态注册 ──
    static Dictionary<string, EnemyState> _registry;
    
    public static void LoadRegistry()
    {
        // 读取 assets/data/enemies.json
        // 遍历 "enemies" 数组，按 category 解析不同格式
    }
    
    public static EnemyState Get(string id) => _registry.GetValueOrDefault(id);
    
    /// <summary>
    /// 从指定区域随机选一个敌人。按权重筛选：一般 70%，精英 20%，特殊 10%。
    /// rarityCap 限制最高稀有度（周目越高 cap 越高）。
    /// </summary>
    public static EnemyState GetRandom(string zone, int rarityCap = 3, float eliteChance = 0.2f)
    {
        var pool = _registry.Values
            .Where(e => e.Zone == zone || e.Zone == "*")
            .Where(e => e.Rarity <= rarityCap)
            .Where(e => e.Category != "boss")  // boss 不随机出现
            .ToList();
        
        if (pool.Count == 0) return Get("base_mob");
        
        var pick = pool[GD.Randi() % pool.Count];
        
        // 精英概率：复制的 normal 敌人升级为 elite
        if (GD.Randf() < eliteChance && pick.Category == "normal")
            return pick.AsElite();
        
        return pick;
    }

    // ── 实例属性 ──
    public string Id;
    public string Name;
    public string Category;   // "normal" | "elite" | "boss" | "special"
    public string Desc;
    public string ScenePath;
    public string Zone;
    public int Rarity;
    
    // 一般/精英使用 min/max 范围，SpawnStats 时随机
    public int PowerMin,  PowerMax,  BodyMin,  BodyMax;
    public int AgilityMin, AgilityMax, HeartMin, HeartMax, FortuneMin, FortuneMax;
    public int BruiseMin, BruiseMax, SevereMin, SevereMax;
    public int SpeedMin, SpeedMax;
    public float EliteMultiplier = 1.3f;

    // Boss/特殊使用定值（min==max 时退化为定值）
    public bool IsBoss => Category == "boss";
    public bool IsSpecial => Category == "special";
    public bool IsElite => Category == "elite";
    public bool UsesRanges => Category == "normal" || Category == "elite";

    // ── 生成方法 ──
    
    /// <summary>
    /// 从属性范围创建 CharacterStats。
    /// normal/elite: 随机取值；boss/special: 固定值。
    /// cycleModifier: 周目加成（每轮回 +2% 全属性，上限 +30%）
    /// </summary>
    public CharacterStats SpawnStats(int cycleModifier = 1)
    {
        float cycleBonus = 1f + Mathf.Min((cycleModifier - 1) * 0.02f, 0.30f);
        float eliteBonus = IsElite ? EliteMultiplier : 1f;
        float totalMult = cycleBonus * eliteBonus;
        
        var st = new CharacterStats
        {
            DisplayName = Name,
            Power   = Roll(PowerMin,   PowerMax,   totalMult),
            Body    = Roll(BodyMin,    BodyMax,    totalMult),
            Agility = Roll(AgilityMin, AgilityMax, totalMult),
            Heart   = Roll(HeartMin,   HeartMax,   totalMult),
            Fortune = Roll(FortuneMin, FortuneMax, totalMult),
        };
        
        st.MaxBruiseHP = Roll(BruiseMin, BruiseMax, totalMult);
        st.MaxSevereHP = Roll(SevereMin, SevereMax, totalMult);
        st.FullHeal();
        
        return st;
    }
    
    // ── 内部方法 ──
    
    int Roll(int min, int max, float mult)
    {
        if (min == max) return Mathf.RoundToInt(min * mult);  // 定值
        int raw = (int)GD.Randi() % (max - min + 1) + min;
        return Mathf.RoundToInt(raw * mult);
    }
    
    /// <summary>从 normal 敌人创建精英变体（复制 + 标记 + 1.3× 倍率）</summary>
    EnemyState AsElite()
    {
        return new EnemyState
        {
            Id = Id + "_elite",
            Name = "精英" + Name,
            Category = "elite",
            Desc = Desc,
            ScenePath = ScenePath,
            Zone = Zone,
            Rarity = Mathf.Min(Rarity + 1, 5),
            // 复制范围
            PowerMin=PowerMin, PowerMax=PowerMax, BodyMin=BodyMin, BodyMax=BodyMax,
            AgilityMin=AgilityMin, AgilityMax=AgilityMax, HeartMin=HeartMin, HeartMax=HeartMax,
            FortuneMin=FortuneMin, FortuneMax=FortuneMax,
            BruiseMin=BruiseMin, BruiseMax=BruiseMax, SevereMin=SevereMin, SevereMax=SevereMax,
            SpeedMin=SpeedMin, SpeedMax=SpeedMax,
        };
    }
}
```

### 2.3 敌人场景文件（8 个新 + 1 个迁移）

所有敌人场景与 `base_mob.tscn` 结构相同——一个 `Node` 根节点挂载 `CharacterStats.cs`：
```
Node "EnemyName"
└── Node "Stats" (CharacterStats.cs)
```

每个 `.tscn` 在 `CharacterStats` 的 `[Export]` 字段中预设不同的基础值，**但实际运行时由 `EnemyState.SpawnStats()` 从 JSON 注入覆盖**。这样场景文件是模板，JSON 是数据源。

如果暂时不创建 8 个独立的 `.tscn` 文件，可以先用一个通用的 `enemy_template.tscn`，通过 `EnemyState.ScenePath` 或代码直接生成。

---

## 三、需要修改的文件

### 3.1 移动场景文件

| 操作 | 源路径 | 目标路径 |
|------|--------|----------|
| 移动 | `scenes/characters/base_mob.tscn` | `scenes/enemies/base_mob.tscn` |

### 3.2 更新路径引用

| 文件 | 行 | 当前值 | 新值 |
|------|-----|--------|------|
| `CombatUI.cs` | 184 | `"res://scenes/characters/base_mob.tscn"` | `EnemyState.Get("base_mob").ScenePath` 或保持 fallback |
| `CombatUI.cs` | 1588-1589 | `$"res://scenes/characters/{enemyId}.tscn"` | `$"res://scenes/enemies/{enemyId}.tscn"` |
| `events.json` | 14 | dragon-threat combat event | `"res://scenes/enemies/base_mob.tscn"` |
| `events.json` | 137 | ancient-battlefield middle | 改为 `"res://scenes/enemies/ancient_warrior.tscn"` |
| `events.json` | 175 | tower-memory middle | 改为 `"res://scenes/enemies/tower_guardian.tscn"` |
| `events.json` | 692 | hermit-encounter | 保持 `base_mob` 或按区域变体 |
| `events.json` | 1173 | crystal-cave middle | 改为 `"res://scenes/enemies/crystal_warden.tscn"` |
| `battle_events.json` | 42 | add_enemy event | 改为使用 `EnemyState` 引用或保持 ID |
| `WorldMap3D.cs` | 639-641 | enemy dot 硬编码位置 | 可后续改为从 enemies.json 读取 |
| `WorldMap.cs` | 221-223 | enemy dot 硬编码位置 | 同上 |

### 3.3 CycleManager.cs 优化（可选）

```csharp
// 当前: 字符串引用
public string PendingEnemyScene;

// 建议: 类型化引用
public string PendingEnemyId;     // "base_mob" / "tower_guardian"
// 使用时: EnemyState.Get(cm.PendingEnemyId).ScenePath
```

### 3.4 EventManager.cs 优化（可选）

```csharp
// 当前: 读取完整路径
case "combat":
    cm.PendingEnemyScene = e["enemy"].AsString();

// 建议: 支持两种格式
case "combat":
    if (e.ContainsKey("enemy_id"))
        cm.PendingEnemyId = e["enemy_id"].AsString();
    else
        cm.PendingEnemyScene = e["enemy"].AsString();  // 向后兼容
```

---

## 四、区域敌人对照表

| 节点 | 区域 | 一般怪物 (70%) | 精英 (20%) | Boss/特殊 (固定) |
|------|------|---------------|------------|-------------------|
| 0 | 林地边缘 | 林间兽 | 精英林间兽 (1.3×) | — |
| 1 | 废矿入口 | 矿道蛞蝓 | 精英矿道蛞蝓 | — |
| 2 | 断崖台地 | 崖鹰 | 精英崖鹰 | — |
| 3 | 古战场 | 亡灵战士 | 精英亡灵战士 | 古代巨龙 `boss` |
| 4 | 结晶洞穴 | 晶石守卫 | 精英晶石守卫 | — |
| 5 | 荒原边缘 | 荒原猎手 | 精英荒原猎手 | — |
| 6 | 忘却之塔 | — | — | 符文守卫 `boss` |
| 7 | 圣泉 | 泉底潜伏者 | 精英泉底潜伏者 | — |
| * | 通用 | 基础魔种 | 精英魔种 | — |

**遇敌逻辑**（在 `EventManager.CheckEvents` 中）：
1. 玩家进入区域 → `normal_encounter` 事件检查 `random` 条件
2. 条件通过 → `EnemyState.GetRandom(zone, rarityCap, eliteChance)` 选敌
3. `rarityCap` 随周目升高（cycle 1→cap=2, cycle 5→cap=4, cycle 10→cap=5）
4. `eliteChance` 随周目升高（cycle 1→5%, cycle 5→20%, cycle 10→35%）
5. 叙事事件的 middle/deep 层固定指定 `"enemy_id":"tower_guardian"` 等 Boss
6. 特殊怪物由特定事件链触发，不走随机遇敌

---

## 五、实施步骤

| # | 操作 | 内容 | 风险 |
|---|------|------|------|
| 1 | 创建 `scenes/enemies/` | `mkdir` 目录 | 无 |
| 2 | 创建 `enemies.json` | 10 种敌人（8 normal + 2 boss），min/max 范围 + 定值混用 | 无 |
| 3 | 创建 `EnemyState.cs` | `LoadRegistry()`, `Get()`, `GetRandom()`, `SpawnStats()`, `AsElite()`, `Roll()` | 需 build |
| 4 | `CycleManager._Ready()` | 加一行 `EnemyState.LoadRegistry()` | 1 行 |
| 5 | 移动 `base_mob.tscn` | `characters/` → `enemies/` | 需同步改路径 |
| 6 | 更新 `CombatUI.cs` | `BuildEnemyCards()` 改为调用 `EnemyState.Get().SpawnStats()` 而非 `GD.Load<PackedScene>` | 核心改动 |
| 7 | `CombatUI.AddEnemies()` | 改为 `EnemyState.Get(enemyId).SpawnStats()` | 同上 |
| 8 | 更新 `events.json` | `"combat"` effect 新增 `"enemy_id"` 字段，保留 `"enemy"` 向后兼容 | JSON |
| 9 | 更新 `EventManager.cs` | `case "combat"`: 优先读 `enemy_id`，回退读 `enemy` 路径 | 1 个 else-if |
| 10 | 更新 `battle_events.json` | `add_enemy` 改为 `enemy_id` | JSON |
| 11 | 更新 `WorldMap3D.cs` | enemy dot 后续改为从 enemies.json 读取 | 可选 |
| 12 | `dotnet build` + JSON 验证 | 编译通过 + 数据可解析 | — |

**注意**：步骤 5 移动 `.tscn` 后，如果 CombatUI 改为完全通过 `EnemyState.SpawnStats()` 生成敌人（不加载场景），可以完全废弃 `.tscn` 敌人模板。当前 `base_mob.tscn` 只有 14 行——仅仅是 `Node + CharacterStats`——`EnemyState.SpawnStats()` 可以直接在代码中创建等价的 `CharacterStats`，无需场景文件。

---

## 六、与现有系统的兼容性

- `CombatUI.BuildEnemyCards()` — 当前通过 `GD.Load<PackedScene>(path)` 加载场景，改为 `EnemyState.Get(id).SpawnStats()` 后可以完全绕开 `.tscn` 加载
- `CombatUI.AddEnemies()` — 同样可以改为 EnemyState 驱动
- `events.json` — 新增 `"enemy_id"` 字段支持，保持 `"enemy"` 字段向后兼容
- `EventManager.cs` — 仅增 1 个 else-if 分支，行为不变
- `battle_events.json` — add_enemy 改为使用 enemy_id
- 所有其他文件不变

---

## 七、运行时浮动数值机制

### normal/elite 敌人每场战斗独立随机

```
EnemyState.SpawnStats(cycleModifier):
  1. 对每个 stat: RNG(powerMin, powerMax) × cycleBonus × eliteBonus
  2. 对 HP:      RNG(bruiseMin, bruiseMax) × cycleBonus × eliteBonus
  3. 对 speed:   RNG(speedMin, speedMax)
  4. cycleBonus: +2% per cycle, 上限 +30% (cycle ≥ 16)
  5. eliteBonus: 1.3× for elite enemies
```

**示例**：基础魔种在 cycle 3 的遇敌：
```
power:   RNG(3,5) × 1.04 = 3.12 ~ 5.20 → 取整 3~5
bruise:  RNG(12,18) × 1.04 = 12.5 ~ 18.7 → 取整 13~19
speed:   RNG(5,7) = 5~7
```

**示例**：精英林间兽在 cycle 8：
```
power:   RNG(4,6) × 1.14 × 1.3 = 5.93 ~ 8.89 → 取整 6~9
body:    RNG(1,3) × 1.14 × 1.3 = 1.48 ~ 4.45 → 取整 1~4
```

### Boss 敌人固定值

```
tower_guardian: power=6, bruise=25, severe=20 — 每次相同
ancient_dragon: power=12, bruise=40, severe=30 — 每次相同
```

Boss 的 `cycleBonus` 仍然生效（高周目有轻微增强），但基础值固定不随机。

---

## 八、不在此规划中的

- 敌人技能系统（当前敌人无技能）
- 敌人 AI（当前回合制无 AI）
- 世界地图敌人巡逻/生成（当前仅硬编码 3 个红点）
- 敌人立绘/模型（暂无视觉资源）
