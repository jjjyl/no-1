# No1 — 项目文档

> Godot 4.5 C# | 日式赛璐璐动漫风 | Roguelike 叙事 RPG

---

## 一、设计概述（简版）

### 游戏是什么

**No1** 是一款叙事驱动的 Roguelike RPG。玩家在神殿选择加护后进入世界，探索节点、触发事件、进行回合制战斗。死亡后进入下一周目，周目间保留部分进度。核心驱动力是"在轮回中揭开世界真相"的叙事体验。

### 核心循环

```
神殿（选加护）→ 进入世界 → 探索节点 → 事件/对话/战斗 → 返回 → 死亡（周目+1）
```

### 三种加护

| 加护 | 效果 |
|---|---|
| 察知 | 遇敌率减半，战利品更丰富 |
| 战意 | 力量 +3 |
| 旅人 | 闪避率 +15%，遇敌率降低 |

### 视野架构

- **倾斜桌面**（默认）：3D 俯视世界地图，wasd 移动、滚轮缩放、中键旋转
- **第一人称环视**（Tab）：固定位置，鼠标自由环视，纯观察
- **房间探索**（进节点）：固定视点 + 自由环视 + 热点点击交互

第一人称不做自由移动，服务于观察和探索，不做第一人称战斗。

### 视觉风格

日式赛璐璐动漫风。MToon shader（二段光照 + 描边）。角色用 VRoid Studio 捏制（VRM 格式），场景用程序化几何 + 手绘贴图。目标风格接近"立体绘本"（2D 精灵 + 3D 世界）。

---

## 二、设计详述

### 游戏循环

```
[启动]
  │
  ▼
┌──────────────────────────────────────┐
│              神  殿                   │
│  ┌─────────────────────────────┐     │
│  │   3D 房间：石板 + 三色光芒     │     │
│  │   鼠标自由环视                │     │
│  │   准星对准光芒 → 高亮 → 点击   │     │
│  │   选加护 → 点击石板中央         │     │
│  │       → 进入世界              │     │
│  └─────────────────────────────┘     │
└──────────────────┬───────────────────┘
                   │
                   ▼
┌──────────────────────────────────────┐
│            世界地图                    │
│  ┌─────────────────────────────┐     │
│  │  3D 倾斜桌面（45° 俯瞰）      │     │
│  │  Sprite3D 构成地形/道路/区域  │     │
│  │  wasd 移动玩家标记             │     │
│  │  进入区域 → 触发事件          │     │
│  │  遇敌 → 切到战斗界面           │     │
│  │  Tab → 第一人称观察           │     │
│  └─────────────────────────────┘     │
│                                      │
│  三个区域：                           │
│  • 林地边缘 — 左下                    │
│  • 废矿入口 — 中央                    │
│  • 断崖台地 — 上方                    │
└──────────────────┬───────────────────┘
                   │
          ┌────────┼────────┐
          ▼        ▼        ▼
       事件     对话     [遇敌]
          │        │        │
          │        │        ▼
          │        │   ┌──────────┐
          │        │   │  战斗界面  │
          │        │   │  UI 回合制 │
          │        │   │  攻击/防御 │
          │        │   │  闪避/道具 │
          │        │   └─────┬────┘
          │        │         │
          └────────┴────┬────┘
                        │
                   赢 → 继续探索
                   死 → 周目 +1 → 回神殿
```

### 属性系统

#### 五维基础属性

| 属性 | 简称 | 影响 |
|---|---|---|
| 力 (Power) | P | 物理伤害基准 |
| 体 (Body) | B | HP 上限 + 物理减伤 |
| 敏 (Agility) | A | 闪避率 + 行动速度 |
| 心 (Heart) | H | 精神伤害/减伤 + 治疗 + 精力 |
| 运 (Fortune) | F | 暴击率 |

#### 派生属性（自动计算）

```
ATK        = 力 + 装备加成
最大擦伤HP  = 10 + 体×2 + 加成
最大重伤HP  = 5  + 体×1 + 加成
闪避率      = 敏×1.5% + 加成
暴击率      = 运×1.2% + 加成
```

#### 双轨 HP 系统

- **擦伤 (Bruise)**：轻伤，战斗中可自行恢复，满值为 0 时开始扣重伤
- **重伤 (Severe)**：致命伤，归零则死亡

### 战斗系统

- 回合制
- 玩家和敌人轮流行动
- 选项：攻击 / 防御 / 闪避 / 道具
- 进度条显示"擦伤"和"重伤"状态
- 事件驱动：战斗中可触发特殊对话和分支

### 叙事系统

- **EventManager** 驱动：JSON 配置文件定义事件
- 事件分多层结构（shallow/middle/deep），逐步揭示剧情
- 支持条件判断（属性检查、flag 检查、物品检查）
- 支持多种效果（对话、属性修改、物品获取、遇敌、解锁神殿内容）
- **DialogueManager** 驱动 NPC 对话：支持自由文本输入（NLP 模式）和预设选项

### 进度系统

| 机制 | 范围 | 说明 |
|---|---|---|
| 周目（Cycle） | 跨轮回 | 死亡后 +1，部分神殿内容随周目解锁 |
| Flag（轮回级） | 当前周目 | 事件触发记录，死亡后清除 |
| Flag（账号级） | 永久 | 跨轮回持久，用于解锁剧情线 |
| 碎片（Fragment） | 永久 | 关键剧情道具，累积解锁真结局 |
| 金币（Money） | 永久 | 神殿商店购买道具 |
| 背包 | 永久 | 装备和道具保持 |

### 视角设计

#### 为什么不做第一人称移动

1. 零素材需求：无需碰撞系统、AI 寻路、地图边界处理
2. 与节点驱动玩法契合：探索 = 选择观察点，不需要自由走动
3. 开发量是自由移动的 1/5
4. 核心体验是"看"和"选择"，不是"走"

#### 为什么选 3D 倾斜桌面而非纯俯视

1. 后续加入材质贴图和角色立绘需要透视才能充分展示
2. 有纵深感的世界比平面更有沉浸感
3. 可以从 2D 纯色数据逐步升级到 3D 完整场景
4. Sprite3D Billboard 方案让 2D 数据和 3D 视角共存

---

## 三、实现概述（简版）

### 技术栈

| 层 | 技术 |
|---|---|
| 引擎 | Godot 4.5 |
| 语言 | C# |
| 渲染 | MToon Shader (VRM 标准动漫渲染) |
| 角色 | VRoid Studio → VRM → godot-vrm 插件导入 |
| 贴图 | 手绘风格 (Kalponic / ambientCG)，标准 PNG |
| 场景 | CSG 几何体（快速原型） + MeshInstance3D（精细控制） |
| 叙事 | JSON 数据驱动事件系统 |
| 对话 | NLP 集成（Ollama 本地 / API） |

### 项目结构

```
scripts/
  core/           — 游戏核心系统
    GameManager.cs     场景切换
    CycleManager.cs    周目/存档/flag 管理
    EventManager.cs    事件触发引擎
    MapNodeData.cs     世界节点数据
    Inventory.cs        背包系统
    CompanionState.cs   伙伴系统
    ShopData.cs        商店数据
  data/
    BlessingData.cs    加护数据定义
    ItemData.cs        物品数据定义
  temple/
    Temple3D.cs        神殿场景控制器（相机、交互、唤醒动画）
    CentralSlab.cs     石板交互（加护选择、进入世界）
    GuideLight.cs      引导光效
    TempleEvolution.cs 神殿随周目变化
  world/
    WorldMap3D.cs      3D 世界地图（Sprite3D Billboard 构建）
    Player3D.cs        3D 玩家（wasd 移动）
    WorldMap.cs        [旧] 2D 世界地图（待替换）
    PlayerController.cs [旧] 2D 玩家
    CameraFollow.cs    [旧] 2D 相机跟随
    ITerrainProvider.cs 地形数据接口
    FlatTerrainProvider.cs 平面地形实现
    WorldMaterials.cs  世界材质管理
    ShopNPC.cs         NPC 商店
  combat/
    CombatEvents.cs    战斗事件处理
    CombatEventDef.cs  战斗事件定义
    SkillData.cs       技能数据
  ui/
    TempleUI.cs        [旧] 2D 神殿 UI
    CombatUI.cs        战斗界面
    MapUI.cs           地图界面
    DialogueManager.cs 对话管理（Autoload）
    FullDialogue.cs    全屏对话
    BannerPopup.cs     横幅提示
    ShopUI.cs          商店界面
    InventoryPanel.cs  背包面板
    CharacterPanel.cs  角色面板
    CharacterHpBars.cs HP 条显示
    TargetSelectPopup.cs 目标选择
    GiftTargetSelect.cs  赠礼选择
    ItemTargetSelect.cs  道具使用选择
    CombatItemMenu.cs    战斗道具菜单
  CharacterStats.cs   角色属性类

scenes/
  temple/temple_3d.tscn   神殿 3D 场景（当前主场景）
  world/world_map.tscn    世界地图场景
  combat/combat.tscn      战斗场景
  map/map.tscn           地图 UI
  ui/                    各种 UI 场景
  characters/            角色预制

assets/
  png/                   贴图纹理
  texture/               纹理资源
  material_3d/           MToon 材质预设 (.tres)
  data/                  数据文件 (JSON)

addons/
  vrm/                   godot-vrm 插件 v2.0.1
  Godot-MToon-Shader/    MToon 动漫 shader v3.4.0
  ambientcg/             贴图导入工具
```

### Autoload 单例

| 名称 | 作用 |
|---|---|
| GameManager | 场景切换、技能数据加载 |
| CycleManager | 周目管理、flag、存档、加护选择 |
| DialogueManager | NPC 对话系统、全屏对话模式 |

---

## 四、实现详述

### 场景流程

```
GameManager._Ready()
  → LoadTemple() → ChangeSceneToFile("temple_3d.tscn")

Temple3D._Ready()
  → 相机唤醒动画（从低处升起）
  → 启用鼠标捕获 + 准星
  → 注册石板交互事件

用户操作：
  悬停三个凹槽 → 光芒高亮 → 显示加护名
  点击凹槽 → 选中加护 → 显示描述
  点击石板中央 → 触发 EnterWorld
    → CycleManager.SelectBlessing()
    → CycleManager.EnterWorld()
    → GameManager.GoToScene("world_map.tscn")

WorldMap3D._Ready()
  → 读取地形数据（FlatTerrainProvider）
  → 构建 Sprite3D 视觉元素（天空/山脉/地形/道路/区域/标签）
  → 构建区域触发器（Area3D + BodyEntered）
  → 构建玩家（Player3D + 菱形标记）
  → 构建相机（倾斜 45° + 缩放旋转）

世界交互：
  玩家进入区域 → Area3D.BodyEntered → 触发 EventManager
  遇敌 → 设置 PendingEnemyScene → _Process 检测 → 切到战斗场景
  Tab → 第一人称环视（相机降高度 + 鼠标自由旋转）

战斗结束：
  胜利 → 继续探索
  死亡 → CycleManager.OnDeath() → 周目+1 → 回神殿
```

### 事件系统架构

```
EventManager (静态类)
  │
  ├─ Load()     — 从 JSON 加载事件定义
  ├─ CheckEvents(zoneName, cm, callback)
  │    └─ 遍历事件 → 检查条件 → 触发效果
  │
  ├─ EventDef   — 事件定义
  │    ├─ Id, Name, Category, Location
  │    ├─ Conditions (属性检查、flag、物品)
  │    ├─ Effects  (对话、属性修改、遇敌、解锁)
  │    └─ Layers   (shallow/middle/deep 分层)
  │
  ├─ Cycle-state
  │    ├─ _triggeredThisCycle — 本周目已触发
  │    └─ _triggeredEver      — 全局已触发
  │
  └─ Flag 系统
       ├─ SetFlag / HasFlag (轮回级)
       └─ CycleManager.SetAccountFlag (永久)
```

### MToon 材质系统

```
Material Pipeline:
  贴图 PNG → MToon ShaderMaterial → .tres 文件 → 拖到 MeshInstance3D

关键参数：
  _MainTex      主贴图（拖 PNG）
  _ShadeTexture 阴影贴图（同 PNG → 暗部有材质）
  _ShadeColor   阴影色调（同色相 + 低亮度）
  _ShadeToony   色阶强度（0-1，越高越动漫）
  _BumpMap      法线贴图（凹凸感，需配套主贴图的 NormalGL）
  _BumpScale    凹凸强度
  _OutlineWidth 描边粗细（动漫核心）
  _OutlineColor 描边颜色（黑色）
  _MainTex_ST   贴图平铺 (X,Y 重复次数, Z,W 偏移)

材质预设 (assets/material_3d/)：
  cavaWall.tres     石墙（CaveWallGrey.png, mtoon_cull_off）
  toon_floor.tres   地板（Rock062, NormalGL + AO）
  toon_roof.tres    屋顶
  toon_slab.tres    石板
  toon_outline.tres 描边材质
  glass_window.tres 玻璃窗
  SlabBody.tres     石板底座
  GuideParticle.tres 引导粒子
```

### 神殿 3D 场景结构

```
Temple3d (Node3D)                     — 控制器脚本
├── WorldEnvironment                  — 雾、环境光、背景色
├── DirectionalLight3D                — 主方向光 + 阴影
├── Room (CSGCombiner3D)              — CSG 房间几何体（隐藏，已烘焙）
│   ├── Floor (CSGBox3D)              — rock 地板
│   ├── WallFront/Back/Left/Right     — tiles 墙壁
│   ├── WindowCutLeft/Right           — CSG 减法窗户
│   ├── RoofLeft/Right                — 斜屋顶
│   ├── Beam                          — 横梁
│   └── MeshInstance3D (QuadMesh)     — 玻璃窗
├── CSGBakedMeshInstance3D            — 烘焙结果（3 个表面材质）
├── CentralSlab (Node3D)              — 中央石板
│   ├── SlabBody (CSGBox3D)           — 石板底座
│   ├── Groove_Insight/Valor/Wanderer — 三色凹槽 (Area3D, Layer 1)
│   ├── SlabCenter (Area3D, Layer 2)  — 中央进入区域
│   ├── Glow_* (OmniLight3D)          — 三色光芒
│   └── Label_* (Label3D)             — 加护标签
├── Guide (Node3D)                    — 引导光
│   └── GPUParticles3D                — 粒子特效
├── Camera3D                          — 固定视点（鼠标旋转）
└── Evolution (Node)                  — 神殿进化控制
```

### WorldMap3D 构建流程

```
BuildParallax()
  → far: Sphere sky + sun sprite（极远）
  → mid: Mountain silhouette sprites（中距，Parallax3D）
  → DragonShadow animated sprite（天边掠过）

BuildTerrain()
  → floor plane（地面色板 / 贴图）
  → zone sprites ×3（区域色块，半透明覆盖）
  → road strips（连接区域的小路）

BuildZoneTriggers()
  → Area3D ×3，BodyEntered → EventManager.CheckEvents()

BuildEnemyPlaceholders()
  → red dot sprites（明雷标记）

BuildPlayer()
  → Player3D (CharacterBody3D + Sprite3D diamond + shadow)
  → 初始位置 = 当前节点坐标

BuildCamera()
  → Camera3D + CameraPivot
  → 默认倾斜 45°，可缩放（滚轮）旋转（中键）

BuildUI()
  → ReturnButton（CanvasLayer，返回神殿）
  → ZoneLabels（Label3D Billboard）
```

### 战斗系统

```
CombatUI (Control, CanvasLayer 覆盖)

状态：
  ┌─ EnemyArea ──────────────────────┐
  │ 敌人名 | 擦伤条 | 重伤条 | HP 文字 │
  ├──────────────────────────────────┤
  │        战斗日志 (RichTextLabel)    │
  ├─ PlayerArea ─────────────────────┤
  │ 擦伤条 | 重伤条 | HP 文字 | 属性   │
  ├─ ActionButtons ──────────────────┤
  │ [攻击] [防御] [闪避] [道具]        │
  ├─ ResultPanel ────────────────────┤
  │ 结果文字 | [继续] [返回神殿]       │
  └──────────────────────────────────┘

属性计算：
  攻击伤害 = ATK + 随机波动 - 敌人防御
  防御效果 = 减伤，提升下次行动速度
  闪避判定 = (玩家闪避率 - 敌人命中率) / 100

事件插入：
  战斗中 EventManager 可插入特殊对话/分支
```

---

## 术语表

| 术语 | 含义 |
|---|---|
| 周目 / Cycle | 一次完整的探索轮回，死亡后 +1 |
| 加护 / Blessing | 神殿中选择的三种祝福之一 |
| 节点 / Node | 世界地图上的可探索区域 |
| 碎片 / Fragment | 关键剧情道具，跨周目累积 |
| Flag | 事件触发标记，分轮回级和永久级 |
| 擦伤 / Bruise | 轻伤 HP，先被消耗 |
| 重伤 / Severe | 致命 HP，归零则死亡 |
| CSG | 构造实体几何，Godot 内置的加减法建模 |

---

*文档版本：2026-06-19 | 项目状态：开发中*
