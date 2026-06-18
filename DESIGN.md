# 游戏设计文档

## 视角体系

### 设计原则

游戏有且仅有两个视角类型，覆盖所有场景：

| 视角 | 用途 | 移动 | 交互 |
|---|---|---|---|
| **2D 俯视** | 大世界导航、区域选择 | ✅ wasd/点击 | 区域触发 |
| **3D 第一人称** | 大世界观察、房间探索 | ❌ 固定位置 | 自由环视 + 热点点击 |

**核心决策**：第一人称不做自由移动（wasd 行走），纯观察。理由：
- 零素材需求（无碰撞、无 AI 寻路、无地图边界处理）
- 与节点驱动玩法天然契合
- 开发量是自由移动的 1/5

---

### 三层视角架构

| 层级 | 视角 | 场景 | 交互 | 状态 |
|---|---|---|---|---|
| L1 大世界导航 | 2D 俯视 | WorldMap (Node2D) | 移动 + 区域触发 | ✅ 已有 |
| L2 大世界观察 | 3D 第一人称 | WorldView3D (Node3D) | 固定位置 + 自由环视 | 🆕 新增 |
| L3 房间探索 | 3D 第一人称 | RoomView (Node3D) | 固定视点 + 自由环视 + 热点 | 🆕 新增 |

L2 和 L3 使用**同一套 3D 视角代码**，仅在数据源和交互层有差异：

| | L2 大世界观察 | L3 房间探索 |
|---|---|---|
| 视点位置 | 玩家当前世界坐标 | 房间预设位置 |
| 场景数据 | 复用 WorldMap 2D 数据 | RoomDef 配置 |
| 几何体 | Sprite3D Billboard | CSG 几何体 + VRM 角色 |
| 交互 | 无 | Area3D 热点点击 |
| 切换方式 | Tab 键按住/松开 | 进入/退出节点 |
| 退出后 | 回到俯视，位置不变 | 回到俯视，标记节点已探索 |

---

### 切换流程

```
  世界地图俯视（Camera2D）
    │
    ├── 按 Tab            → L2 大世界观察（Camera3D）
    │   松手/再按 Tab      → 回到俯视
    │
    └── 进入节点/点击区域  → L3 房间探索（Camera3D + 热点）
        退出节点/点击返回  → 回到俯视
```

**技术要点**：
- L1 ↔ L2 通过 `Camera2D.MakeCurrent()` / `Camera3D.MakeCurrent()` 切换
- L1 → L3 场景切换，加载 `RoomView` 场景
- CanvasLayer（HUD/DialogueUI）始终覆盖在所有视角之上

---

### L2 大世界 3D 观察 — Sprite3D Billboard 方案

**核心思路**：不复刻新素材。现有 WorldMap 的 2D 程序化数据直接映射到 3D 空间。

#### 为什么不用 3D 模型重建
- WorldMap 场景是纯色块程序化生成的（ColorRect、Polygon2D）
- 移到 3D 只是换了个透视方式，数据完全不变
- 用 Sprite3D Billboard → Godot 自动处理透视缩放

#### 2D → 3D 数据映射

| WorldMap 源数据 | 生成方法 | Sprite3D 映射 | 位置 |
|---|---|---|---|
| 天空 | `BuildParallax` far layer | 巨大 Sprite3D 纯色板 | 远处高空 |
| 太阳 | `BuildParallax` sun | Sprite3D 发光圆 | 极远处 |
| 山脉剪影 | `BuildParallax` mid layer Polygon2D | Sprite3D 剪影 | 中距 |
| 龙影 | `BuildParallax` DragonShadow | Sprite3D 动画剪影 | 天边 |
| 地形底色 | `BuildTerrain` base | 巨型 Sprite3D | 地面 |
| 区域色块 | `AddZoneRect` ×3 | Sprite3D 方块 | 地面（三个区域位置） |
| 道路 | `DrawPath` | 长条 Sprite3D | 地面 |
| 敌人 | `BuildEnemyPlaceholders` ColorRect | Sprite3D 红色标记 | 各区域 |
| 区域名 | `BuildZoneLabels` Label | Label3D | 悬在各区域上方 |

#### 纹理生成

每个 Sprite3D 的贴图 = 一个 4×4 像素的纯色 `ImageTexture`，颜色直接取源 `ColorRect.Color`。

```csharp
// 伪代码
ImageTexture ColorTexture(Color c) {
    var img = Image.Create(4, 4, false, ImageFormat.Rgba8);
    img.Fill(c);
    return ImageTexture.CreateFromImage(img);
}
```

#### 透视效果

- Camera3D 天然提供近大远小
- Billboard 确保色块始终正对相机（像纸片立牌）
- 可以加 WorldEnvironment 雾效让远处自然模糊

#### 工作量

```
新增组件： WorldView3D.cs  (~100 行)
修改：     WorldMap.cs 提取数据访问（不删不改，只加 public getter）
```

---

### L3 房间探索 — 固定视点 + 热点交互

详见"场景系统"章节（待补充）。核心要素：

- 视点：固定的 Camera3D 位置，鼠标自由旋转（Yaw + Pitch）
- 场景：CSG 几何体（墙壁/地板/天花板）+ VRM 角色 + 道具
- 交互：准星对准 Area3D → 高亮 → 点击触发
- 房间间切换：走出口 / 点击过渡热点 → 淡出淡入 → 加载下一个房间

---

## 视觉风格

- **日式赛璐璐动漫风**
- 渲染：MToon shader（二段光照 + 描边 + 轮廓线）
- 场景：程序化几何体 + 免费 PBR 贴图 + Toon 光照
- 人物：VRM 动漫角色
- 引擎：Godot 4

---

## 角色管线

### 工具链

| 层级 | 工具 | 方式 | 导出格式 |
|---|---|---|---|
| 精细角色（主角/重要NPC） | VRoid Studio | 从零捏 / Booth 预设 | .vrm |
| 中量角色（次要NPC/路人） | VRoid Hub | 下载成品 → VRoid 微调 | .vrm |
| 原型/怪物 | Meshy AI | 文字生成 + 自动 rig | .glb |

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

1. 启用插件，用 VRoid 导出角色验证管线
2. 搭建第一个 3D 房间原型（固定视点 + 自由环视 + 热点交互）
3. 实现大世界 Tab 切换第一人称观察（Sprite3D Billboard 映射）
4. 角色清单：主角 + 首批 NPC
