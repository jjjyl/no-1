# Known Issues

## Panel click-through: 打开角色面板点击仍触发地图移动
- **症状**: 点"物品"按钮打开 CharacterPanel 后，同帧点击被 Player3D._Input 捕获，角色移动到点击位置
- **尝试过的方案**: 
  1. CanvasLayer `mouse_filter = STOP` — 无效（_Input 是全局回调，不受 GUI filter 影响）
  2. `IsOverlayOpen` 标记 + `_Input` 里 return — 无效（标记在 `_Ready` 或 `ShowCharacterPanel` 开头设，但 `_Input` 处理顺序早于信号回调或同帧节点 `_Ready`）
- **根因**: Godot 的 `_Input` 在 GUI 事件处理前或同时触发，同帧内无法拦截
- **影响**: 低 — 后续移动方式改 WASD 后自然消失
- **临时规避**: 无
- **目标修法**: 切换为 WASD 移动（design.md 第 30 行），按钮点击不再触发移动
