# 物品系统 — 场景接入指南

> 代码已完成。神殿不显示物品。物品/装备管理：世界地图覆盖层 + 战斗快速使用。

---

## 1. combat.tscn — 战斗添加"物品"按钮

### 根节点 node_paths 新增 1 个字段

在 `_escapeBtn` 和 `_continueBtn` 之间插入：

```
_itemBtn = NodePath("MarginContainer/HBoxContainer/Player/PlayerPanel/VBoxContainer/PlayerActions/HBoxContainer/ItemBtn")
```

### 在动作按钮行追加 1 个按钮

路径：`PlayerActions/HBoxContainer/`

在 EscapeBtn 之后添加：

| 节点名 | 类型 | 用途 |
|--------|------|------|
| ItemBtn | Button | 战斗中快速使用消耗品 | text="物品" |

图标 `res://assets/ui/icons/heal.png`（已存在）。

---

## 2. 世界地图 — 无需手动操作

`WorldMap.cs` 的 `BuildReturnButton()` 已通过代码创建"物品"按钮（紧邻"返回神殿"按钮左侧），点击打开 InventoryPanel 覆盖层。不需要改 `.tscn` 文件。

---

## 3. inventory.tscn（已创建，无需手动改动）

`scenes/ui/inventory.tscn` — 物品/装备覆盖层面板，被 `DialogueManager.ShowInventory()` 调用。

- 布局：左装备栏 + 右物品列表 + 下描述/操作按钮
- 关闭按钮自动释放自身和 CanvasLayer
- 功能：浏览物品、使用消耗品、装备/卸下装备
