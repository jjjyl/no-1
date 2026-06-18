# 神殿 3D 场景编辑器搭建指南

> **原则**：场景你搭，逻辑我写。本文档只描述你在 Godot 编辑器里要做什么。
> **最终场景**：`scenes/temple/temple_3d.tscn`，空 Node3D 根节点 + 挂脚本，所有子节点在编辑器里手工摆好。

---

## 一、新建场景

`File → New Scene` → 选 `3D Scene`（根节点 `Node3D`）。

根节点改名 `Temple3D`，挂脚本 `Temple3D.cs`（先建空场景，脚本后面写好后你再挂）。

---

## 二、WorldEnvironment

`Temple3D` 下添加子节点 `WorldEnvironment`。

Inspector → `Environment` → `New Environment`：

| 属性 | 值 | 说明 |
|---|---|---|
| Background → Mode | Color | |
| Background → Color | `#F0EDE6` | 暖白，窗外颜色 |
| Ambient Light → Color | `#1a1510` | 极暗暖色 |
| Ambient Light → Energy | 0.3 | |
| Fog → Enabled | ✓ | |
| Fog → Mode | Depth | |
| Fog → Density | 0.08 | 初始浓雾，代码会动态改 |
| Fog → Color | `#F0EDE6` | 和背景同色 |

其余属性（Tonemap、Glow、SSR 等）全部默认，不调。

---

## 三、DirectionalLight3D

`Temple3D` 下添加子节点 `DirectionalLight3D`：

| 属性 | 值 |
|---|---|
| Transform → Position | (2, 5, 3) |
| Transform → Rotation | (-45, -30, 0) |
| Light → Color | `#ffe8d6` |
| Light → Energy | 0.3 |
| Shadow → Enabled | 关 |

---

## 四、Room（CsgCombiner3D）— 几何体

`Temple3D` 下添加子节点 `CsgCombiner3D`，命名 `Room`。

以下所有几何体都是 `Room` 的子节点。

### 材质定义

**墙/地板材质**（Floor + 四面墙共用）：

在 Inspector 里新建 `StandardMaterial3D`：

| 属性 | 值 |
|---|---|
| Albedo → Color | `#b8b0a6`（暖灰石） |
| Roughness | 0.25 |
| Metallic | 0 |

**屋顶/梁材质**（RoofLeft、RoofRight、Beam 共用）：

新建 `StandardMaterial3D`：

| 属性 | 值 |
|---|---|
| Albedo → Color | `#8a8078`（深暖灰，和墙壁略有区别） |
| Roughness | 0.3 |
| Metallic | 0 |

### 4.1 Floor（地板）

`Room` 下添加 `CsgBox3D`，命名 `Floor`：

| 属性 | 值 |
|---|---|
| Transform → Position | (0, -0.1, 0) |
| Size | (4, 0.2, 4) |
| Material | 墙/地板材质 |

### 4.2 四面墙

四个 `CsgBox3D`，Material 都用"墙/地板材质"。

**WallBack**（后墙）：

| 属性 | 值 |
|---|---|
| Transform → Position | (0, 1.75, -2) |
| Size | (4.2, 3.5, 0.2) |

**WallFront**（前墙）：

| 属性 | 值 |
|---|---|
| Transform → Position | (0, 1.75, 2) |
| Size | (4.2, 3.5, 0.2) |

**WallLeft**（左墙）：

| 属性 | 值 |
|---|---|
| Transform → Position | (-2, 1.75, 0) |
| Size | (0.2, 3.5, 4.2) |

**WallRight**（右墙）：

| 属性 | 值 |
|---|---|
| Transform → Position | (2, 1.75, 0) |
| Size | (0.2, 3.5, 4.2) |

> 墙壁 4.2m 是故意比房间大一点，确保角落无缝。

### 4.3 窗户（镂空减法）

两个 `CsgBox3D`，`Operation = Subtraction`。

**WindowCutLeft**（左墙窗户）：

| 属性 | 值 |
|---|---|
| Operation | **Subtraction** |
| Transform → Position | (-2, 2.0, -0.8) |
| Size | (0.4, 1.2, 0.6) |

**WindowCutRight**（右墙窗户）：

| 属性 | 值 |
|---|---|
| Operation | **Subtraction** |
| Transform → Position | (2, 2.0, 0.8) |
| Size | (0.4, 1.2, 0.6) |

> X 尺寸 0.4 大于墙厚 0.2，确保完全穿透。

### 4.4 斜顶（人字形屋顶）

两个板子对拼成人字形。脊线沿 X 方向（左墙→右墙）。

**RoofLeft**（Z < 0 的半边）：

| 属性 | 值 |
|---|---|
| Transform → Position | (0, 3.0, -1.0) |
| Transform → Rotation | (26.5°, 0, 0) ← 绕 X 轴 |
| Size | (4.3, 0.15, 2.25) |
| Material | 屋顶/梁材质 |

**RoofRight**（Z > 0 的半边）：

| 属性 | 值 |
|---|---|
| Transform → Position | (0, 3.0, 1.0) |
| Transform → Rotation | (-26.5°, 0, 0) ← 反向绕 X 轴 |
| Size | (4.3, 0.15, 2.25) |
| Material | 屋顶/梁材质 |

**Beam**（屋脊横梁）：

| 属性 | 值 |
|---|---|
| Transform → Position | (0, 3.5, 0) |
| Size | (4.3, 0.12, 0.12) |
| Material | 屋顶/梁材质 |

> **调试**：斜顶角度 ±26.5° 可微调到 ±26° 或 ±27°，肉眼对齐即可。Size 的 Z 如果不够覆盖到墙顶就加大到 2.3。

---

## 五、CentralSlab（中央石板）

`Temple3D` 根下（非 Room 子节点）添加 `Node3D`，命名 `CentralSlab`。

| 属性 | 值 |
|---|---|
| Transform → Position | (0, 0, 0) |
| 脚本 | `CentralSlab.cs` |

### 5.1 石板本体

`CentralSlab` 下添加 `CsgBox3D`，命名 `SlabBody`：

| 属性 | 值 |
|---|---|
| Transform → Position | (0, 0.04, 0) |
| Size | (1.2, 0.08, 0.8) |

Material — 新建 `StandardMaterial3D`：

| 属性 | 值 |
|---|---|
| Albedo → Color | `#d4cfc8`（淡暖灰，比地板亮） |
| Roughness | 0.15（比地板光滑） |
| Metallic | 0 |

### 5.2 三个凹槽（Area3D + 圆柱碰撞体）

三个凹槽三角形排列在石板表面（Y = 0.08）。不做几何凹坑——用光表示即可。Area3D 的圆柱是碰撞检测区域。

**Groove_Insight**（察知，左后）：

添加 `Area3D`，命名 `Groove_Insight`：

| 属性 | 值 |
|---|---|
| Transform → Position | (-0.25, 0.08, -0.1) |

其下添加子节点 `CollisionShape3D`，Shape 选 `CylinderShape3D`：

| 属性 | 值 |
|---|---|
| Height | 0.02 |
| Radius | 0.05 |

**Groove_Valor**（战意，前中）：

添加 `Area3D`，命名 `Groove_Valor`：

| 属性 | 值 |
|---|---|
| Transform → Position | (0, 0.08, 0.15) |

其下添加 `CollisionShape3D` → `CylinderShape3D`：

| 属性 | 值 |
|---|---|
| Height | 0.02 |
| Radius | 0.05 |

**Groove_Wanderer**（旅人，右后）：

添加 `Area3D`，命名 `Groove_Wanderer`：

| 属性 | 值 |
|---|---|
| Transform → Position | (0.25, 0.08, -0.1) |

其下添加 `CollisionShape3D` → `CylinderShape3D`：

| 属性 | 值 |
|---|---|
| Height | 0.02 |
| Radius | 0.05 |

### 5.3 石板中央（确认进入世界的点击区域）

`CentralSlab` 下添加 `Area3D`，命名 `SlabCenter`：

| 属性 | 值 |
|---|---|
| Transform → Position | (0, 0.08, 0) |

其下添加 `CollisionShape3D`，Shape 选 `BoxShape3D`：

| 属性 | 值 |
|---|---|
| Size | (0.8, 0.02, 0.4) |

> 这个盒子盖住石板中央，周边留空隙给三个凹槽的圆柱碰撞体。

### 5.4 凹槽辉光（OmniLight3D）

三个 `OmniLight3D`，放在 `CentralSlab` 下（不在 Area3D 子节点下，方便代码直接引用）。

**Glow_Insight**（青色）：

| 属性 | 值 |
|---|---|
| Transform → Position | (-0.25, 0.15, -0.1) |
| Light → Color | `#4488cc` |
| Light → Energy | 0.3 |
| Light → Range | 0.25 |
| Shadow → Enabled | 关 |

**Glow_Valor**（金色）：

| 属性 | 值 |
|---|---|
| Transform → Position | (0, 0.15, 0.15) |
| Light → Color | `#ccaa44` |
| Light → Energy | 0.3 |
| Light → Range | 0.25 |
| Shadow → Enabled | 关 |

**Glow_Wanderer**（绿色）：

| 属性 | 值 |
|---|---|
| Transform → Position | (0.25, 0.15, -0.1) |
| Light → Color | `#44aa44` |
| Light → Energy | 0.3 |
| Light → Range | 0.25 |
| Shadow → Enabled | 关 |

### 5.5 悬浮标签（Label3D）

三个 `Label3D` 放 `CentralSlab` 下：

**Label_Insight**：

| 属性 | 值 |
|---|---|
| Transform → Position | (-0.25, 0.28, -0.1) |
| Text | 察知 |
| Visible | 关（初始隐藏） |
| Font Size | 24 / 16 |
| Modulate | `#4488cc` |
| Billboard | **Enabled** |
| Horizontal Alignment | Center |
| Outline Size | 2 |

**Label_Valor**：

| 属性 | 值 |
|---|---|
| Transform → Position | (0, 0.28, 0.15) |
| Text | 战意 |
| Visible | 关 |
| Font Size | 24 / 16 |
| Modulate | `#ccaa44` |
| Billboard | **Enabled** |
| Horizontal Alignment | Center |
| Outline Size | 2 |

**Label_Wanderer**：

| 属性 | 值 |
|---|---|
| Transform → Position | (0.25, 0.28, -0.1) |
| Text | 旅人 |
| Visible | 关 |
| Font Size | 24 / 16 |
| Modulate | `#44aa44` |
| Billboard | **Enabled** |
| Horizontal Alignment | Center |
| Outline Size | 2 |

> Font Size 有两个值是因为 Label3D 在 Godot 4.x 里可能叫 `Font Size`（像素）或需要用 Theme Override。如果在 Theme Override 里设，填 `Font Sizes → Font Size: 16`。如果直接属性里有 `font_size`，填 24。视你的 Godot 版本而定。

---

## 六、Guide（引导者）

`Temple3D` 根下添加 `Node3D`，命名 `Guide`，挂脚本 `GuideLight.cs`。

| 属性 | 值 |
|---|---|
| Transform → Position | (-1.6, 0.6, 1.4) |

> 左前方墙根，距石板约 2m。

### 6.1 粒子系统

`Guide` 下添加 `GpuParticles3D`，命名 `Particles`：

| 属性 | 值 |
|---|---|
| Emitting | ✓ |
| Amount | 30 |
| Lifetime | 2.0 |
| One Shot | 关 |
| Explosiveness | 0 |
| Visibility AABB → Position | (0, 0, 0) |
| Visibility AABB → Size | (2, 2, 2) |

**Draw Passes**：展开 → 添加 `QuadMesh`：

| 属性 | 值 |
|---|---|
| QuadMesh → Size | (0.04, 0.04) |

**Process Material**：新建 `ParticleProcessMaterial`：

| 属性 | 值 |
|---|---|
| Emission Shape → Shape | Sphere |
| Emission Shape → Sphere Radius | 0.06 |
| Gravity → Direction | (0, 0.15, 0) |
| Initial Velocity → Velocity Min | 0.05 |
| Initial Velocity → Velocity Max | 0.25 |
| Scale → Scale Min | 0.5 |
| Scale → Scale Max | 1.5 |
| Color → Color | `#ffaa66`（暖橙） |
| Color → Color Ramp | 新建 GradientTexture1D |

Color Ramp（GradientTexture1D）配置：

- 左端 `#ff8844`，不透明（Alpha = 1）
- 右端 `#ffcc88`，全透明（Alpha = 0）
- 产生边缘柔和的暖光粒子

---

## 七、Camera3D

`Temple3D` 根下添加 `Camera3D`：

| 属性 | 值 |
|---|---|
| Transform → Position | (0, 1.6, -1.5) |
| Transform → Rotation | (0, 0, 0) |
| Current | 关（代码里 `MakeCurrent()`） |
| Fov | 75 |

> 位置在石板前方 1.5m，Y = 1.6 是站姿眼睛高度。Rotation 全部 0 面朝 Z+（朝石板方向），代码里再调。

---

## 八、Evolution（演化数据节点）

`Temple3D` 根下添加 `Node`（普通 `Node`，不是 `Node3D`），命名 `Evolution`，挂脚本 `TempleEvolution.cs`。

无子节点。无 Transform。纯数据组件。

---

## 九、最终节点树

搭建完成后，场景树的完整结构：

```
Temple3D (Node3D)                     ← Temple3D.cs
├── WorldEnvironment
├── DirectionalLight3D
├── Room (CsgCombiner3D)
│   ├── Floor (CsgBox3D)
│   ├── WallBack (CsgBox3D)
│   ├── WallFront (CsgBox3D)
│   ├── WallLeft (CsgBox3D)
│   ├── WallRight (CsgBox3D)
│   ├── WindowCutLeft (CsgBox3D, Subtraction)
│   ├── WindowCutRight (CsgBox3D, Subtraction)
│   ├── RoofLeft (CsgBox3D)
│   ├── RoofRight (CsgBox3D)
│   └── Beam (CsgBox3D)
├── CentralSlab (Node3D)              ← CentralSlab.cs
│   ├── SlabBody (CsgBox3D)
│   ├── Groove_Insight (Area3D)
│   │   └── CollisionShape3D (Cylinder)
│   ├── Groove_Valor (Area3D)
│   │   └── CollisionShape3D (Cylinder)
│   ├── Groove_Wanderer (Area3D)
│   │   └── CollisionShape3D (Cylinder)
│   ├── SlabCenter (Area3D)
│   │   └── CollisionShape3D (Box)
│   ├── Glow_Insight (OmniLight3D)
│   ├── Glow_Valor (OmniLight3D)
│   ├── Glow_Wanderer (OmniLight3D)
│   ├── Label_Insight (Label3D)
│   ├── Label_Valor (Label3D)
│   └── Label_Wanderer (Label3D)
├── Guide (Node3D)                    ← GuideLight.cs
│   └── Particles (GpuParticles3D)
├── Camera3D
└── Evolution (Node)                  ← TempleEvolution.cs
```

---

## 十、检查清单

- [ ] `Room` 下共 10 个 CSG 子节点（Floor + 4墙 + 2窗户减法 + 2屋顶板 + 1梁）
- [ ] `WindowCutLeft` 和 `WindowCutRight` 的 Operation = Subtraction
- [ ] `CentralSlab` 下共 11 个子节点（1 石板 + 4 Area3D + 3 光 + 3 标签）
- [ ] 三个 `Label3D` 初始 Visible = false，Billboard = Enabled
- [ ] `Guide` 挂 `GuideLight.cs`，`Particles` 下有 QuadMesh + ParticleProcessMaterial
- [ ] `Camera3D` 的 Current = false
- [ ] `Evolution` 是普通 Node，挂 `TempleEvolution.cs`
- [ ] 所有脚本引用的 `[Export]` 字段在场景里拖拽连线完成（脚本写好后做）

---

## 十一、后续脚本对接提醒

等四个 C# 文件写好后，你需要做：

1. `Temple3D.cs` 拖拽引用：`_slab` → CentralSlab，`_guide` → Guide，`_evolution` → Evolution，`_cam` → Camera3D
2. `CentralSlab.cs` 拖拽引用：四个 Area3D、三个 OmniLight3D、三个 Label3D
3. `GuideLight.cs` 拖拽引用：Particles (GpuParticles3D)
4. `TempleEvolution.cs` 拖拽引用：（无，纯读取 CycleManager）

---

## 尺寸汇总

| 元素 | 尺寸 (m) |
|---|---|
| 房间底面 | 4 × 4 |
| 墙高度 | 3.5 |
| 屋顶脊高 | 3.5 |
| 屋顶檐高 | 2.5 |
| 石板 | 1.2 × 0.08 × 0.8 |
| 凹槽圆柱碰撞体 | ∅0.1 × 0.02 |
| 窗户开口 | 0.4 × 1.2 × 0.6 |
