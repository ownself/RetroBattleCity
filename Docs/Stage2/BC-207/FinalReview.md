# BC-207 独立只读审查

## Review

- **Merge verdict: OK with notes**
- **Finding:** No issues found.
- **Fixed:** 无。本审查未修改项目、文档或 Unity 状态，未执行测试、Unity 命令、提交或递归委派。

在本轮指定范围内，未发现由当前 diff 引起或使其可达的具体 P0/P1/P2 问题。以下 notes 为验证边界，不是缺陷清单，也不代表 BC-208/209 或 M2 验收通过。

## 审查对象与方法

已先读 `AGENTS.md`、`Docs/Stage2/BC-207/Contract.md`、`Verification.md` 及实现交接；检查工作树 diff、新增 HUD/字形/作者脚本、专项测试、Stage01 保存绑定及本轮原始证据。仅沿真实计数、生命、会话替换、显示坐标和验证数量接缝读取必要旧源码，未追溯历史巨量证据。

`watchdog_diff` 只提供 reviewer 启动 HEAD 对应的工作树差量，不能独立审查提交范围。主代理另于本地 `2026-10-24T19:55:00+08:00` 实采并确认 HEAD 仍为请求基线：

`b2b14b9da04ed483c5265986b221172b6207a96d`

## Correct：实现核对

### 1. 剩余敌军不重复计算预约

`Assets/BattleCity/Scripts/UI/ArenaHud.cs:24–25` 使用：

`EnemyWave.PendingCount + ArenaSession.AliveEnemyCount`

未另加 `ReservedCount`，也未使用 wave 的宿主快照 `AliveCount` 代替真实活体数。其依据与模型一致：`EnemyWaveState.cs:37–40` 明确 Pending 包含预约；成功物化后才增加 Spawned，并清除预约（`:97–99`）。

`Assets/BattleCity/Tests/EditMode/ArenaHudTests.cs:37–50` 有实际断言，覆盖预约、占用受阻、物化后总数不变，以及真实共享子弹击杀后归零。运行原始证据 `Raw/runtime-after.json` 另记录 Pending16、Alive4、Reserved0、HUD20，与该公式一致。

### 2. 生命投影符合真实总出场次数语义

`ArenaHud.cs:51` 直接读取 `PlayerLife.RemainingLives`。原模型在 `Assets/BattleCity/Scripts/Core/PlayerLifeState.cs:29–30` 定义其包含当前生命；死亡扣一次（`:75`），复活不再扣减（`:82`）。

因此初始3表示当前1＋备用2；死亡等待时2表示未来两次出场；复活后仍为2；最后死亡为0。布局明确显示 `LIVES CURRENT + RESERVE`（`Assets/BattleCity/Editor/Stage01HudAuthoring.cs:38`），未改写 BC-205 规则。

专项断言见 `ArenaHudTests.cs:53–60`、`Assets/BattleCity/Tests/PlayMode/ArenaHudPlayModeTests.cs:100–118`，包括等待、复活和生命耗尽0的实际 HUD 投影。

### 3. 关卡号使用初始快照

`Assets/BattleCity/Scripts/Core/ArenaController.cs:167–169` 向 HUD 传入私有 `initialLevel.stageNumber`，而非可变作者资产字段。

`ArenaHudPlayModeTests.cs:69–80` 将作者资产关卡号暂改为9，再通过实际 R 输入重开，断言新 Session 的 HUD 仍显示初始关卡1，并恢复作者字段。

### 4. 阶段提示、重开与清理路径完整

- 五阶段映射位于 `ArenaHud.cs:27–36`；Ready 明确 Enter，Paused 明确 Esc，Won/Lost 有结果文本，Playing 清空阶段提示。
- 初始化和正常刷新均推送 HUD；`RestartNow → RenderSession → RenderHud` 在 Session 替换后立即推送新实例（`ArenaController.cs:124–138,156–169`）。
- HUD 没有事件订阅、输入或战斗推进代码；旧 Session 不持有 HUD 回调。
- Controller 禁用/销毁调用 Clear（`ArenaController.cs:180,194`）；HUD 自身禁用/销毁清除 Session 引用并隐藏 Canvas（`ArenaHud.cs:72–79`）。

测试不只检查映射函数：`ArenaHudPlayModeTests.cs:53–78` 经生产 Update 验证 Enter、Playing、Esc、Paused、Lost、R 以及旧新 Session 隔离；`:124–133` 验证禁用、重新启用及销毁 Controller 后的引用和 Canvas 状态。Won/生命耗尽 Lost 由受控真实模型投影测试覆盖。

### 5. uGUI 与游戏参考帧保持同一整数缩放

`Stage01HudAuthoring.cs:13–26` 使用固定256×240的 WorldSpace Canvas、世界原点(-1,-1)、比例1/16和 HUD 排序层；四边框及右32px槽不覆盖地图区域。

实际场景保留 PPU16、256×240、Windowbox、UpscaleRenderTexture 配置（`Assets/BattleCity/Scenes/Stage01.unity:849–853`）。已直接查看三张有效截图，Ready、Paused 和底栏提示均真实可见。

原始 `Raw/pixel-comparison.json` 保留了比较代码及结果：

| 检查 | 结果 |
|---|---:|
| 512×480截图每2×2像素块差异 | 0 |
| 1000×700窗口中心512×480内容与整数截图差异 | 0 |
| 外围黑边非黑像素 | 0 |

非整数窗口内容矩形为 `(244,110,512,480)`。`Raw/visual-size-512-corrected.json`、`visual-size-1000.json` 和 capture 结果表明切换了实际固定 GameView 尺寸，不是仅指定输出图片大小。此处为核阅 writer 留存的实测结果，未由 reviewer 重新计算像素。

### 6. 实际 Stage01 与 recipe 相符

`Assets/BattleCity/Editor/Stage01Authoring.cs:69–74` 创建 HUD、投影初始 Ready 数字、验证并保存场景。当前 Stage01 有 Controller→HUD 引用及完整数字/提示绑定（`Stage01.unity:220–226,588–609`）；提示文本和坐标与 recipe 一致（`:4750–4806,5035–5088`）。

场景脚本 GUID 与新增 `.meta` 相符。`ArenaHudTests.cs:71–100` 对实际保存场景断言绑定、原生数字、CanvasRenderer、尺寸、边界及不遮挡地图。原始作者重建成功结果已核阅。

## Correct：测试及原始证据对账

已逐项核阅以下最终结果中的非零数量、唯一测试全名及 Passed 状态：

| 集合 | Passed / Total |
|---|---:|
| ArenaHudTests | 10 / 10 |
| ArenaHudPlayModeTests | 3 / 3 |
| Stage01Tests | 6 / 6 |
| SessionLifecycleIntegrationTests | 10 / 10 |
| SessionLifecyclePlayModeTests | 3 / 3 |
| Stage01PlayModeTests | 3 / 3 |
| ArenaPlayModeTests | 5 / 5 |
| **合计** | **40 / 40** |

数量口径正确：新增13、既有回归27，不是一次全量运行，也未把纠正重跑重复计入唯一数量。

还核阅了：

- 最终编译结果：completed、failed=false、errors为空。
- 最初 HUD Play 结果确为2/3，失败原始 JSON 仍在；最终3/3单独保存。
- 最终异步启动结果中的0用例明确为 running，未被当作通过。
- 首次作者 wave 空引用、caption 缺 CanvasRenderer 的失败与后续纠正证据均保留。
- 新增测试有计数、真实模型结果、Sprite、Session 身份及非零 caption mesh 的实质断言，不是仅“进入 Play”或无断言冒烟。
- `runtime-before.json → runtime-after.json` 记录 frame22590→27271、tick0→623，支持真实 Update 持续推进。
- `console-observed-interval-final.json` 在 cursor664→776 范围返回无新增 warn/error，且 dropped=false、reset=false；未将历史累计错误声称为零。
- `final-editor-state.json` 记录 stopped、未暂停、Stage01 clean、HUD1个、GameView索引0；临时尺寸清理另有原始结果。

## 证据边界与未覆盖项

1. **本审查没有重跑编译、测试或操作 Unity。** 上述执行结论来自直接核阅本轮原始结果，不能表述为 reviewer 独立复测通过。
2. 27项旧回归早于最终 CanvasRenderer 修正；`Verification.md:73` 已明确披露。最终新增专项重新运行，未发现该修正改变战斗接缝的证据。
3. 未进行用户人工体验、完整第一关胜利和两种失败流程；Won及生命耗尽 Lost 是受控真实模型测试，不冒称 BC-209 全流程体验。
4. 未验证小于256×240窗口，不在当前契约保证范围。
5. 未重新计算全部文件 SHA256；保护资产不变的判断结合了当前 diff 与本轮 `baseline-preservation.json`，不是 reviewer 独立 hash 审计。
6. 场景完整 diff 的工具输出发生截断；已补读当前场景关键绑定、相机、HUD和基地接缝，但不声称逐行重核全部 Tilemap 序列化内容。
7. Unity 生成场景空 `m_Name: ` 行的行尾空格为已披露风格结果，不作为本轮行为缺陷，也未建议手改 YAML。

## 审查计时

| 项目 | 记录 |
|---|---|
| 开始，本地 ISO | **2026-10-24T19:54:07.303+08:00** |
| 开始依据 | 主代理读取 runtime `startedAt=1792842847303` 后提供 |
| 中途本地采样 | 2026-10-24T19:55:00+08:00；距开始52.697秒 |
| 结束，本地 ISO | **待主代理按本 reviewer 的 runtime 终态补录** |
| 完整 elapsed 秒 | **待终态补录：`(endedAt − 1792842847303) / 1000`** |

Reviewer 工具没有本地时钟或 shell 能力，已及时报告；主代理明确要求以 runtime 终态补录，不扩大权限或伪造结束时间。本报告不把中途52.697秒冒充完整审查耗时。

**最终结论：OK with notes。可进入主代理 BC-207 验收处置；不代表后续任务或 M2 已通过。**