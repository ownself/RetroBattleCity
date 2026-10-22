# 阶段 1 首批验收：BC-100～BC-103

> 当前进度已推进到 BC-104，Space 可发射子弹，见 [BC-104 验收](BC-104/Acceptance.md)。下方“尚未射击”等描述为首批历史范围，不代表当前版本。

> 后续回归 PH1-002：用户发现侧向转弯无响应。已定位为超过2px对齐范围时永久拒绝转向；补丁改为逐步接近对齐线后转弯。修复前新增65例中25例复现失败；按用户要求，修复后不运行完整测试，由用户人工复测。后续 PH1-003：用户人工测试后反馈“测试过了，看上去还不错”，本次转向问题关闭，BC-102 恢复完成。补丁编译通过；后续 BC-104 批次中65项转向回归已全部通过，完整战斗回归仍留 BC-108。下方 31/31、2/2 为首批历史结果，不是本补丁重新运行的结果。

## 结论与范围

**BC-100～BC-103 通过本批验收；阶段 1 / M1 尚未完成。**

本批交付可运行的移动试验场，不是第一关，不包含武器、子弹、砖墙破坏、爆炸、敌人或胜负流程。BC-104～BC-108 保持待办，人工操作手感与完整战斗回归留在 BC-108，不以自动化输入冒充人工体验。

- Unity 6000.6.7f1，现有 URP 17.6.0、Input System。
- 回退点：本地标签 `bc-stage1-start-8f0a033`，指向 `8f0a033`（Phase 0 implementation）。未提交本批修改，未建立远程仓库。
- 原始 PNG、语义切图、PixelBaseline 场景、Build Settings 均未更改；原有未跟踪 `.vsconfig` 保持不动。

## 运行与操作

打开 `Assets/BattleCity/Scenes/CombatArena.unity` 并进入 Play Mode，点击 Game 视图取得焦点：

- WASD / 方向键：四方向连续移动。
- Escape：暂停 / 恢复。失焦自动暂停，重新取得焦点后须手动按 Escape 恢复。
- 暂停期间按住或暂停前仍按住的战斗键，**释放后重新按下才恢复响应**，避免恢复时误移动、误射击。
- Space：已接入逻辑输入命令，但 **BC-104 尚未实现，不会发射子弹**。
- 本批尚无暂停 HUD；可从 `ArenaController.Session.Clock.Paused` 检查暂停状态。

试验场资产：`Assets/BattleCity/Data/CombatArena.asset`。出生中心 `(24,24)`；地图 208×208px。包含砖墙、钢墙、16px 可通过通道、8px 不可通过通道、24px 宽通道以及地图边缘测试区域。合成布局不冒称 Stage 01。

## 实现职责

| 代码 | 职责 |
| --- | --- |
| `Scripts/Core/LogicClock.cs` | 双精度累计时间、固定 60Hz、整数 tick、每帧最多 8 tick，丢弃超额积压并计数；暂停清空余量 |
| `Scripts/Input/PlayerInputBuffer.cs` | 最新物理键优先、别名独立排序、同次采样 Up→Left→Down→Right；短按射击边沿保留到一个 tick，暂停抑制输入 |
| `Scripts/Input/KeyboardInputSource.cs` | Input System 键盘状态到纯逻辑按钮，无旧版 Input API |
| `Scripts/Core/ArenaSession.cs` | 输入/暂停→固定逻辑→移动的更新顺序，失焦暂停；不依赖 MonoBehaviour 或 Transform |
| `Scripts/Map/MapState.cs` | 从资产复制唯一地图状态；8px 地形与 4px 砖墙掩码；半开边界、矩形占用查询及轴向扫掠 |
| `Scripts/Map/ArenaMapView.cs` | 独立地表背景与障碍 Tilemap；仅投影地图数据，不反向提供碰撞状态 |
| `Scripts/Tanks/TankMotor.cs` | 16×16px 坦克，连续移动、8px 转向网格/最多 2px 修正、校验完整吸附路径；移动时每 8 tick 切换履带帧 |
| `Scripts/Core/ArenaController.cs` | 场景绑定、渲染帧采样与时间适配；缓存方向/动画 Sprite，只在显示层四舍五入像素 |
| `Editor/Phase1Arena.cs` | 通过编辑器 API 显式生成资产/场景；复用 M0 实际验收过的 URP 像素相机，不手写场景 YAML |

没有 Rigidbody2D、Collider2D 或 TilemapCollider2D，没有每碎砖一个 GameObject。`MapState` 尚未公开破墙修改入口，显示局部更新与掩码破坏在 BC-106 接入。地图视图当前显式支持 Empty / Brick / Steel；其他地形不静默显示为空。

## 自动化测试

证据：[`EditMode-results.json`](EditMode-results.json)、[`PlayMode-results.json`](PlayMode-results.json)。

### EditMode：31/31

包含阶段 0 原有 7 项及本批新增 24 个用例（含参数化案例）：

- 30 / 60 / 144 渲染帧率，每秒均推进 60 tick，移动距离均为 60px。
- 长帧最多 8 tick、丢弃诊断、暂停不补算余量、非法时间拒绝。
- 多方向优先级、释放回退、W/方向键别名独立追踪。
- Input System 真实 KeyboardState 到按钮映射；短射击脉冲只被一个 tick 消费。
- 暂停/恢复不蓄积输入，失焦不自动恢复。
- 地图复制不污染 authoring 资产；半开边界、贴墙、四侧地图边界。
- 高速轴向扫掠不穿砖墙或钢墙；8 / 16 / 24px 通道。
- 转向修正上限、墙角/边界盒体稳定；停止/遇阻不继续滚动履带动画。
- 场景全部地形 Tile 与逻辑数据逐格一致，8px 象限 Y 方向正确；无第二套物理状态。

### PlayMode：2/2

- 真实 `ArenaController.Update` 与渲染帧确实推进逻辑时钟，而非只进入 Play Mode；渲染器实际读取第 1 履带帧和逻辑位置；暂停冻结 tick。
- 两次卸载/重新载入试验场，重新建立出生位置、地图和运行状态，源资产不被运行时修改。

运行命令（本工程 Pipeline 0.9.0 的实际命令名已分组，不能照抄旧版 `eval` / `editor_play` 别名）：

```bash
unity command script recompile --caller plugin --skill unity-cli --format json
unity command script recompile_status --caller plugin --skill unity-cli --format json
unity command script eval --caller plugin --skill unity-cli --format json -- --code 'return BattleCity.Editor.Phase1Arena.Build();'
unity command test run --caller plugin --skill unity-cli --format json -- --mode editor --filter BattleCity --async_tests true
unity command test status --caller plugin --skill unity-cli --format json
unity command test run --caller plugin --skill unity-cli --format json -- --mode playmode --filter BattleCity --async_tests true
unity command test status --caller plugin --skill unity-cli --format json
```

## 实际运行、输入与显示

证据：[`Runtime-input-and-pause.json`](Runtime-input-and-pause.json)、[`Screenshots/arena-768x720.png`](Screenshots/arena-768x720.png)。

- 启用编辑器后台推进作为验证专用覆盖；在运行时临时将 session 视为有焦点，不更改成品失焦策略。
- 通过 `runtime simulate_key` 驱动 Input System，而非直接改 Transform：W 将坦克从 `(24,24)` 移到 `(24,160)`，顶部恰好接触位于 y=168 的砖墙底边，无穿透。
- Escape 暂停后，帧计数 `38202→38905`，逻辑 tick 保持 `5779`。证明渲染仍推进而战斗时钟冻结。
- 暂停时按住 Space 再恢复，`FireHeld=false` 且 `FirePressed=false`；必须释放重新按下才响应。
- 768×720 相机截图为参考分辨率的 3 倍：地图、坦克、砖钢纹理清晰，墙面象限无错位。此截图是相机离屏渲染，不冒称独立窗口测试；非整数黑边基线沿用 M0。
- 验证完退出 Play Mode、移除工具临时生成的 `Assets/Docs` 截图导入目录，证据只留在工程根 `Docs/Stage1/`。恢复后台运行覆盖，不把验证策略写入游戏。

最终 Console 证据：[`Console-final.json`](Console-final.json)。

## 本批发现并处理的问题

1. 生成器在 Single 模式打开/关闭场景前加载的未使用 ScriptableObject 会被 Unity 卸载，导致末尾绑定校验报错。改为先保存地图，完成场景切换后重新加载所有资源引用；完整重建验证通过。
2. 首轮 EditMode 30/31：测试把矩形边缘接触误判为碰撞。按 M0 冻结半开边界修正测试场景，未改碰撞规则，重跑 31/31。
3. 首轮 PlayMode 0/2：`LoadSceneInPlayMode` 后同一帧立刻查询根对象，场景尚未完成激活。测试等待下一 player-loop 帧后查询并校验 loaded 状态，重跑 2/2。
4. 地图主资产名加备注与文件名不一致，产生 Unity 命名警告。主名恢复 `CombatArena`，非第一关的说明移到代码/文档，不留下新增警告。

## 下一步与未覆盖项

- BC-104：武器、炮口偏移、冷却与在场子弹限制；消费 `TankCommand.FireHeld/FirePressed`。
- BC-105～BC-107：子弹扫掠与同距离排序、敌我过滤、4px 局部破墙同步、命中/爆炸生命周期。
- BC-108：完整战斗自动回归和人工贴墙转向/操作手感；真实 30/60/144 帧率运行体验。当前帧率一致性证明为逻辑时间输入测试，不冒称三种实际窗口帧率的人工测试。
- 更完整的输入 Action 资产、重绑定、手柄均未纳入本批；本地键盘由 Input System 薄适配器实现。
- 暂不承诺原版数值、完整关卡或发布版本体验。
