# 敌人与战斗系统操作手册

> 最后更新：2026-06-29 | 本文档供后续 AI/开发者快速理解敌人系统的添加和修改流程。

---

## 一、系统架构总览

```
┌─ 数据层 ─────────────────────────────────────────────────────┐
│ enemies.json        → 战斗数值（power/hp/speed，min/max 浮动）   │
│ enemy_visuals.json  → 精灵表/原型/Scale/阴影                    │
│ enemy_fsm.json      → 状态机 + 动画帧映射                        │
└──────────────────────────────────────────────────────────────┘
         │                        │
         ▼                        ▼
┌─ 注册层 ────────┐    ┌─ 事件层 ──────────────────────────────┐
│ EnemyState.cs   │    │ events.json       → 世界事件（触发战斗）  │
│ .LoadRegistry() │    │   "type":"combat"                      │
│ .Get(id)        │    │   "enemy_id":"xxx"  ← 战斗初始敌人       │
│ .GetRandom()    │◄───│                                         │
│ .SpawnStats()   │    │ battle_events.json → 战斗中事件           │
│ .AsElite()      │    │   "type":"add_enemy"                    │
└─────────────────┘    │   "enemy_id":"xxx"  ← 增援/追加敌人      │
         │             └─────────────────────────────────────────┘
         ▼
┌─ 实体层 ─────────────────────────────────────────────────────┐
│ EnemyBase.cs       → 世界地图敌人行为（FSM + 阴影 + 动画）       │
│ CombatUI.cs        → 战斗界面敌人卡片生成                       │
│   BuildEnemyCards()  → 读取 PendingEnemyScene 创建初始敌人      │
│   AddEnemies(id,n)   → 战斗中动态追加敌人                       │
└──────────────────────────────────────────────────────────────┘
```

---

## 二、添加新敌人的完整流程

### 步骤 1：在 `assets/data/enemies.json` 添加数据

```json
{
  "id": "new_enemy_id",          // 唯一标识（英文小写+下划线）
  "name": "新敌人名称",            // 显示名
  "category": "normal",           // normal | elite | boss | special
  "desc": "描述文字",
  "archetype": "biped_small",     // 原型模板：biped_small | quadruped | floater
  "zone": "所在区域",              // 匹配 events.json 的 location，"*" 表示通用
  "rarity": 1,                   // 1-5，控制高周目出现概率
  
  // normal/elite 用 min/max 范围（运行时每次随机取值）
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
  
  // boss/special 用定值（无需 min/max，直接写数字）
  // "stats": { "power": 6, "body": 6, "agility": 3, "heart": 5, "fortune": 3 },
  // "hp": { "bruise": 25, "severe": 20 },
  // "speed": 4
}
```

**关键规则**：
- `archetype` 必须是三个原型之一（`biped_small` / `quadruped` / `floater`）
- normal/elite 用 min/max 范围；boss/special 用定值
- 不要创建 elite 条目 — 精英是运行时从 normal 用 `AsElite()` 动态生成的（1.3× 倍率）

### 步骤 2：在 `assets/data/enemy_visuals.json` 添加视觉配置

```json
{
  "new_enemy_id": {
    "spritesheet": "res://assets/sprites/enemies/new_enemy.png",
    "archetype": "biped_small",
    "scale": 1.0,
    "sprite_offset": [0, 0, 0],
    "shadow_scale": [0.6, 0.3, 1],
    "shadow_offset_y": -0.8,
    "map_icon": "res://assets/icons/map/dot_enemy_human.png",
    "map_dot_color": "#888888",
    "minimap_blip": "enemy_normal"
  }
}
```

### 步骤 3：在 `assets/data/enemy_fsm.json` 添加状态机和动画

```json
{
  "new_enemy_id": {
    "sprite_layout": { "hframes": 4, "vframes": 6 },
    "anims": {
      "idle":    { "row": 0, "start_col": 0, "frames": 4, "fps": 6,  "loop": true },
      "wander":  { "row": 1, "start_col": 0, "frames": 4, "fps": 8,  "loop": true },
      "rest":    { "row": 2, "start_col": 0, "frames": 2, "fps": 3,  "loop": true },
      "aggro":   { "row": 3, "start_col": 0, "frames": 2, "fps": 10, "loop": true },
      "attack":  { "row": 4, "start_col": 0, "frames": 4, "fps": 12, "loop": false },
      "death":   { "row": 5, "start_col": 0, "frames": 4, "fps": 8,  "loop": false }
    },
    "states": {
      "idle":     { "next": "wander",  "after_sec": [3, 7],  "anim": "idle" },
      "wander":   { "next": "idle",    "after_sec": [4, 12], "anim": "wander", "speed": 1.5, "wander_radius": 7.0 },
      "rest":     { "next": "idle",    "after_sec": [10, 20], "anim": "rest",  "chance_from_idle": 0.10 },
      "aggro":    { "next": "chase",   "on_enter": "player_in_range", "anim": "aggro", "range": 4.0 },
      "chase":    { "next": "combat",  "on_contact": true, "anim": "wander", "speed": 2.8 },
      "combat":   { "on": "battle_start", "anim": "attack" },
      "death":    { "on": "hp_zero",   "anim": "death" }
    }
  }
}
```

**字段说明**：
- `sprite_layout.hframes/vframes`：精灵表的列数和行数（由 Godot Sprite3D 切帧使用）
- `anims.xxx.fps`：每秒帧数
- `anims.xxx.loop`：是否循环播放
- `states.xxx.after_sec`：[最小秒数, 最大秒数]，随机区间
- `states.xxx.speed`：世界地图移动速度（m/s）
- `states.xxx.wander_radius`：闲逛范围半径
- `states.xxx.range`：仇恨检测范围

---

## 三、让敌人在战斗中出现的 3 种方式

### 方式 A：世界事件触发战斗（初始敌人）

在 `events.json` 的世界事件中：

```json
{
  "effects": [
    {
      "type": "combat",
      "enemy_id": "forest_beast"    // ← 指定初始敌人 ID
    }
  ]
}
```

**执行流程**：
1. 玩家进入区域 → `EventManager.CheckEvents()` 检查事件
2. 命中后 → 设置 `CycleManager.PendingEnemyScene = "forest_beast"`
3. `WorldMap3D.OnEnterZone()` 检测到非空 → `_combatPending = true`
4. 下一帧 → 加载 `combat.tscn`
5. `CombatUI._Ready()` → `BuildEnemyCards()` → `EnemyState.Get("forest_beast")` → 创建 EnemyGroupSize 个该敌人

> ⚠️ **注意**：不要写 `"enemy": "res://scenes/characters/xxx.tscn"`（旧格式）。新格式用 `"enemy_id": "xxx"`。EventManager 兼容两种写法（优先 enemy_id，回退 enemy）。

### 方式 B：战斗中事件追加敌人（增援）

在 `battle_events.json` 中：

```json
{
  "id": "my_reinforcement",
  "trigger": { "type": "on_round" },
  "conditions": [
    { "type": "round", "value": 3, "op": "gte" }
  ],
  "effects": [
    { "type": "show_dialogue", "speaker": "系统", "text": "更多的敌人出现了！" },
    { "type": "add_enemy", "enemy_id": "base_mob", "count": 2 }
  ],
  "once": true
}
```

**执行流程**：
1. 战斗到第 3 回合 → 条件满足
2. `CombatEvents` 触发事件
3. `CombatUI.AddEnemies("base_mob", 2)` 被调用
4. `EnemyBase.AddEnemies()` → `EnemyState.Get("base_mob").SpawnStats()` × 2

### 方式 C：通过 EnemyState.GetRandom() 随机抽取

```csharp
// 代码中直接调用（例如 WorldMap3D 随机遇敌）
var enemy = EnemyState.GetRandom(
    zone: "林地边缘",        // 区域名
    rarityCap: 3,            // 最高稀有度
    eliteChance: 0.2f        // 20% 概率变精英
);
```

**行为**：
- 从该区域（或 `zone: "*"` 的通配敌人池）筛选 normal 敌人
- 稀有度 ≤ rarityCap
- 有 eliteChance 概率把选中的 normal 升级为 elite（×1.3 倍率）
- boss 永远不会被随机抽出

---

## 四、文件依赖关系

```
添加新敌人需要修改的文件：
  ┌─────────────────┐
  │ 必定修改 (3个)    │
  │ enemies.json     │ ← 战斗数值
  │ enemy_visuals.json│ ← 精灵/阴影/颜色
  │ enemy_fsm.json   │ ← 状态机/动画帧
  └─────────────────┘
  
  ┌─────────────────┐
  │ 按需修改          │
  │ events.json      │ ← 世界事件中引用（方式A）
  │ battle_events.json│ ← 战斗中事件引用（方式B）
  │ WorldMap3D.cs     │ ← 地图上添加敌人点位（敌人ID+坐标）
  └─────────────────┘
  
  ┌─────────────────┐
  │ 不需要修改        │
  │ EnemyState.cs    │ ← 自动从 JSON 加载所有敌人
  │ EnemyBase.cs     │ ← 通用模板脚本，不硬编码任何敌人
  │ CombatUI.cs      │ ← 通过 EnemyState.Get(id) 泛用
  │ EventManager.cs  │ ← 泛用处理
  │ *.tscn           │ ← 3个原型模板已覆盖所有体型
  └─────────────────┘
```

---

## 五、常见问题排查

### Q: 敌人不出现 / 永远是 base_mob
1. 检查 `events.json` 中是否用了 `"enemy_id"` 而不是旧的 `"enemy"`
2. 确认 `WorldMap3D.cs` / `WorldMap.cs` 没有在切场景前清空 `PendingEnemyScene`
3. 查看控制台：`[EnemyState] Loaded N enemies` 确认 JSON 加载成功

### Q: 战斗事件有对话但没有敌人
检查 `battle_events.json` 中该事件是否包含 `"type": "add_enemy"` 效果。只有 `show_dialogue` 不会生成敌人。

### Q: 世界地图上敌人行为异常
- `EnemyBase.cs` 通过 `GetNode("AggroArea")` 查找子节点 —— 确认 `.tscn` 模板中仇恨检测节点命名为 `AggroArea`（不是 `Area3D`）
- 阴影不贴地 → 检查模板中是否有 `GroundRay` 节点且 `TargetPosition` 朝下

---

## 六、数据格式速查

| JSON 字段 | 类型 | 何时使用 | 示例 |
|-----------|------|----------|------|
| `id` | string | 所有敌人 | `"forest_beast"` |
| `category` | `"normal"` / `"elite"` / `"boss"` / `"special"` | 所有敌人 | `"normal"` |
| `archetype` | `"biped_small"` / `"quadruped"` / `"floater"` | 所有敌人 | `"quadruped"` |
| `stats.power` | `{min,max}` 或 `number` | 所有敌人 | `{"min":3,"max":5}` 或 `6` |
| `stats.body` | 同上 | 所有敌人 | — |
| `hp.bruise` | 同上 | 所有敌人 | — |
| `speed` | 同上 | 所有敌人 | — |
| `spritesheet` | path | enemy_visuals.json | `"res://assets/sprites/enemies/xxx.png"` |
| `sprite_layout.hframes` | int | enemy_fsm.json | `4` |
| `anims.idle.row` | int | enemy_fsm.json | `0`（精灵表第 1 行） |
