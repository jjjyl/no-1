# 游戏设计文档

## 视角体系

### 设计原则

游戏有且仅有两个视角类型，覆盖所有场景：

| 视角 | 用途 | 移动 | 交互 |
|---|---|---|---|
| **3D 倾斜桌面** | 大世界导航、区域选择 | ✅ wasd/点击（xz 平面） | 区域触发 + 景观浏览 |
| **3D 第一人称** | 近距离观察、房间探索 | ❌ 固定位置 | 自由环视 + 热点点击 |

**核心决策**：
1. 大世界默认视角为 **3D 倾斜俯视**（如俯瞰桌面沙盘），不再使用纯 2D 俯视
2. 第一人称**不做自由移动**，固定视点纯观察
3. 两个视角共用同一套 Sprite3D 世界数据，只切换摄像机位置和朝向
4. 后续加入的材质纹理和角色立绘需要透视才能充分展示

---

### 视角架构

```
同一个 3D 世界（Sprite3D Billboard 构成）

  默认：倾斜桌面视角（Camera3D 斜上方 45°）
          │
          ├── wasd 移动玩家
          ├── 缩放：滚轮拉近拉远
          └── 旋转：中键拖动旋转桌面角度
          
  按 Tab：第一人称视角（Camera3D 降到地面高度）
          │
          ├── 鼠标自由环视（Yaw + Pitch）
          ├── 不移动，松手/再按 Tab 回到倾斜视角
          └── 用来看地形细节、远处景观、角色立绘
  
  进入节点：房间探索（切换场景）
          │
          ├── 固定视点 + 自由环视
          └── 热点点击交互
```

| 层级 | 场景 | 摄像机 | 交互 |
|---|---|---|---|
| **大世界** | WorldMap (Node3D) | Camera3D 倾斜 45° | 移动 + 缩放 + 旋转 + 区域触发 |
| **大世界观察** | 同上 | Camera3D 地面高度 | 自由环视，不移动 |
| **房间探索** | RoomView (Node3D) | Camera3D 预设位置 | 自由环视 + 热点点击 |

---

### 大世界渲染 — Sprite3D Billboard

所有世界元素用 Sprite3D Billboard 构成，像纸片立牌站在 3D 空间里。透视由 Camera3D 自动提供。

#### 为什么 Sprite3D

- 复用现有 WorldMap 程序化数据（颜色、位置、尺寸全部保留）
- 零新素材开局，后续逐步替换为贴图材质
- 升级路径清晰：纯色 → 贴图 → 完全替换为 3D 模型

#### 2D → 3D 数据映射

| 源数据 | 生成方法 | Sprite3D 位置 |
|---|---|---|
| 天空 | `BuildParallax` far | 巨大色板，高空远处 |
| 太阳 | `BuildParallax` sun | 发光圆，极远处 |
| 山脉剪影 | `BuildParallax` mid | 剪影，中距山脊线 |
| 龙影 | `BuildParallax` DragonShadow | 动画剪影，天边 |
| 地形底色 | `BuildTerrain` base | 巨型色板，铺在地面 |
| 区域色块 ×3 | `AddZoneRect` | Sprite3D，各区域位置 |
| 道路 | `DrawPath` | 长条 Sprite3D，地面 |
| 敌人标记 | `BuildEnemyPlaceholders` | 红色 Sprite3D，各区域 |
| 区域名 | `BuildZoneLabels` | Label3D，悬在区域上方 |
| 角色（未来） | VRoid 导出 | Sprite3D 立绘 / VRM 模型 |

#### 纹理

- 初期：4×4 纯色 `ImageTexture`，颜色取源 `ColorRect.Color`
- 后期：替换为材质贴图（草地、沙地、石板等 tile 纹理）

#### 升级路径

```
阶段 1：纯色 Sprite3D（现在）
阶段 2：Sprite3D + 贴图纹理（加入材质后）
阶段 3：区域地标用 3D 模型替代（建筑/树木）
阶段 4：角色以 VRoid VRM 模型站在世界中
```

---

### 房间探索 — 固定视点 + 热点交互

场景数据结构（待细化）：

```
RoomDef {
    Id, CameraPos, AmbientColor,
    WallDef[], PropDef[], HotspotDef[],
    OnEnterEvents[]
}

HotspotDef {
    Position, Size, Label,
    Type: describe/pickup/dialogue/transition/combat/rest
    Data: 描述文本 / NPC ID / 目标房间 / 物品 ID
}
```

核心要素：
- Camera3D 固定位置，鼠标自由旋转
- CSG 几何体（墙壁/地板/天花板）+ VRM 角色 + 道具
- 准星对准 Area3D → 高亮 → 点击触发交互
- 进出口淡出淡入过渡

---

## 交互系统

### 大世界

| 输入 | 效果 |
|---|---|
| wasd / 点击 | 移动玩家 |
| 滚轮 | 缩放（拉近拉远） |
| 中键拖动 | 旋转桌面视角 |
| Tab 按住 | 切换到第一人称观察 |
| 点击区域标记 | 进入该区域节点 → 切到房间探索 |

### 房间探索

| 输入 | 效果 |
|---|---|
| 鼠标移动 | 自由环视 |
| 悬停 Area3D | 热点高亮 + 显示名称 |
| 左键点击 | 触发交互 |
| Esc / 返回按钮 | 退出房间，回到大世界 |

### 全局覆盖层

CanvasLayer（HUD/DialogueUI/CombatUI/SettingMenu）始终在所有视角之上，不受摄像机切换影响。

---

## 视觉风格

- **日式赛璐璐动漫风**
- 渲染：MToon shader（二段光照 + 描边 + 轮廓线）
- 场景：程序化几何体 + 免费 PBR 贴图 + Toon 光照
- 人物：VRM 动漫角色
- 世界：立体绘本风格（Sprite3D + 透视 + 后期待加贴图材质）
- 引擎：Godot 4

---

## 角色管线

### 工具链

| 层级 | 工具 | 方式 | 导出 |
|---|---|---|---|
| 精细角色（主角/重要 NPC） | VRoid Studio | 从零捏 / Booth 预设 | .vrm |
| 中量角色（次要 NPC/路人） | VRoid Hub | 下载成品 → VRoid 微调 | .vrm |
| 原型/怪物 | Meshy AI | 文字生成 + 自动 rig | .glb |

### 服装组合

使用 VRoid Studio v2.0+ Dress-Up 功能：

```
1. 打开穿衣服的角色 A → 左上角 → "Bulk export worn items as XWear"
2. 打开目标角色 B → Dress-Up 标签 → Add Base Model
3. Add Costume → 选 clothes.xwear → Auto-fitting
4. Delete Mesh 修复穿模 → Export VRM
```

### 建模规格

| 角色类型 | 面数 | 骨骼 | 贴图 | VRoid 导出设置 |
|---|---|---|---|---|
| 主角 | ≤ 30,000 | ≤ 80 | 2048 | Reduce Polygons 不勾 |
| 重要 NPC | ≤ 15,000 | ≤ 60 | 2048 | Reduce Polygons Light |
| 路人 NPC | ≤ 8,000 | ≤ 50 | 1024 | Reduce Polygons Medium |
| 怪物 | ≤ 8,000 | ≤ 40 | 1024 | Meshy 默认 |

**骨骼控制**：SpringBone 是性能瓶颈，头发链 ≤ 4 条、每链 ≤ 6 节点、服装物理非必要则关。

**VRoid 导出设置**：
```
Format：          VRM 1.0
Reduce Polygons： 按上表
Reduce Materials：不勾（破坏 MToon 描边）
Reduce Bones：    不勾（手动在 Hair Editor 里减）
Texture：         Default（主角）/ 1/2（NPC）
```

### Godot 导入

- **godot-vrm 插件** v2.0.1 — VRM 1.0 导入/导出，骨骼/表情/物理保留
- **MToon Shader 插件** v3.4.0 — 日式动漫渲染

---

## 环境准备

### 已安装

| 项目 | 路径 | VRM | MToon |
|---|---|---|---|
| no-1 | `/mnt/d/Godot/no-1/addons/` | v2.0.1 | v3.4.0 |
| LifeSim | `/mnt/d/Godot/LifeSim/addons/` | v2.0.1 | v3.4.0 |

### 启用步骤

1. Godot 打开项目
2. Project → Project Settings → Plugins
3. 启用 "VRM" 和 "MToon Shader"

### 验证

从 VRoid Studio 导出 `.vrm` → 拖入 Godot FileSystem → 应正确显示带 MToon 渲染的动漫角色。

---

## 后续规划

1. 启用 godot-vrm 插件，用 VRoid 导出角色验证管线
2. 重构 WorldMap 为 Node3D + Sprite3D Billboard（替换现有 Node2D）
3. 实现倾斜桌面视角（Camera3D 45° + 缩放 + 旋转）
4. 实现 Tab 切换第一人称观察
5. 搭建第一个 3D 房间原型（固定视点 + 自由环视 + 热点交互）
6. 角色清单：主角 + 首批 NPC
