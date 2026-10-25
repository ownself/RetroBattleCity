# BC-305 定向实现与验证交接

## 范围与结论

验证者：**subagent worker**，2026-10-25；Unity 6000.6.7f1 / CLI 1.0.0-beta.14（父代理已检查最新）。起点 main / `bdb20b0e509276be1d4818c09638e5f419e463e4`；精确实施起点 `Temp/BC305306-Before`，包括 BC304 未提交成果。

后续owner冷启动的Unity skill精确位置：`H:/Projects/Unities/unity-agent-plugin/skills/unity-cli/SKILL.md`；reference目录为 `H:/Projects/Unities/unity-agent-plugin/skills/unity-cli/references/`。运行CLI前先完整读取skill及相关reference，本轮使用 `integration-advanced.md` 与 `playmode-verification-loop.md`；命令参数仍需以实际Editor发现结果为准，不能直接照抄旧文档命令名。

本轮只实现 BC305 Clock / Grenade single-seam，未实现306、未改MapState、未开展307以后、未改契约或勾选总计划/看板，未提交/推送。实际编译、14新Edit + 2新Play及8相邻死亡/终局专项均成功，**24唯一Passed**（22 Edit、2 Play），去重与最新受影响项证据见 `Audit.json`。这不是全量回归、完整组合回归、人工试玩或自然通关；是否验收由父代理与独立review裁决。规则仍为项目默认，未经原版核实。

## 实现与文件边界

相对本轮起点，只有6个既有文件改变，另4个测试/Editor生成meta：

- `Scripts/Pickups/PickupDefinition.cs`：新增正数校验的 `clockDurationTicks = 600`；会话配置快照。
- `Scripts/Pickups/PickupState.cs`：公开只读 `ClockDurationTicks` / `ClockRemainingTicks`；独立全局计时，不随玩家死亡清除；重复刷新不叠加。
- `Scripts/Core/ArenaSession.cs`：仅新成功收据分派Clock/Grenade；所有敌方（AI和外控，含新出生）跳过决策选项查询、brain.Decide/RNG、Move/履带与Fire/武器冷却；仍加入碰撞/弹道目标。玩家、在途弹、效果、波次正常推进。手雷枚举此时活敌，走共享死亡、计分和爆炸，不制造子弹命中/掉落。
- `Scripts/Tanks/TankState.cs`：经父代理批准增补 `internal bool TryKill()`，只在alive→dead返回true；ApplyHit保留来源/保护/伤害规则后以它提交致死转换，手雷直接调用。死亡停止移动、保留原在途弹lease。
- `Scripts/AI/EnemyWaveState.cs`：经父代理批准增补 `internal RefreshAliveCount(int)`，仅校验并刷新当前活敌快照；不改变预留、生成、待生成、间隔、准备或RNG，不再次Step波次。
- `Data/Pickups.asset`：通过Editor API `SaveAssetIfDirty`仅保存必要clock字段600；原seed303/helmet600/maximumLives9/携带序列保持。
- 新增 `Tests/EditMode/ClockGrenadePickupTests.cs` 与 `Tests/PlayMode/ClockGrenadePickupPlayModeTests.cs` 及Unity生成的meta。

这里与上文省略的路径均相对 `Assets/BattleCity/`。已有BC304 Controller、PlayerLife、测试、文档等保持起点hash；没有回退既有未提交改动。

## tick顺序与306插入位置

本轮顺序：

1. 根据**本tick开始时**Clock剩余值决定是否跳过敌方模拟；玩家仍正常输入/移动/射击。
2. 所有弹道统一扫描，地图/效果/伤害/幂等死亡结算；头盔和全局Clock在这一段之后递减。
3. 玩家生命/复活、一次 `EnemyWave.Step` 完成出生；新坦克本tick不参与早先模拟。
4. 拾取前 `SettleOutcome()` 保持既有终局门禁：本tick弹伤已导致失败或胜利，不再掉落/拾取/奖励。
5. 尚未终局时处理弹伤携带者掉落、新 `TryCollect` 收据；Clock刷新到快照600（本次不扣），Grenade只杀当时活敌（包括刚提交出生者，不杀预留/待出生），刷新波次活敌快照，再 `SettleOutcome()`。
6. 两处终局入口汇合到同一清理/通知通路：暂停、清命令/槽/玩家效果/全局效果，一次终局事件。失败判定仍由SessionFlowState优先；不出现先胜利再奖励或下tick才胜利。

后续306接线锚点（本轮**未写306代码/占位器**）：当前 `StepGlobalEffects()` 位于伤害后、出生前、收据前，可按同样“未来完整tick”语义扣时；涉及占用的地形提交须使用出生及wave reservation已结算后的状态（现有wave Step后/收据前的接缝）；Shovel新收据分派位于同一个switch。终局共用清理块、Stop/Release和新会话构造是清理/退休/重开接缝。306须遵照其组件交接与契约实现快照/占用安全，不应再次Step波次或新增时间源。

## 自动专项证据

| 范围 | 唯一成功数 | 原始结果 |
| --- | ---: | --- |
| 新BC305 Edit | 14 | `Edit-Result.json`；最后TryKill整理后只复测受影响9项，见 `Grenade-Death-Final-Result.json`、`Clock-Birth-Death-Final-Result.json`、`Clock-Player-Death-Final-Result.json`、`Terminal-Bullet-Final-Result.json`、`Simultaneous-Death-Final-Result.json` |
| 新BC305 Play | 2 | `Play-Result.json`；死亡整理后只复测真实键盘/Update手雷项1项，`Play-Death-Final-Result.json`；重开项逻辑未受影响不重复 |
| 相邻共享死亡/终局 Edit | 8 | `Tank-Health-Adjacent-Result.json` 2、`Tank-Overkill-Adjacent-Result.json` 1、`Enemy-Armor-Adjacent-Result.json` 1、`Lifecycle-Outcome-Adjacent-Result.json` 4 |

对应 `*-Request.json` 保留mode/filter/async元信息；`Audit.json`按FullName去重并指向最终结果，不将重复受影响项累加。

覆盖要点：

- 600未来完整敌方阶段的每tick端点、601阶段恢复；资产/配置快照、重复刷新非累计、收据不重放、非法0/-1。
- AI决策计数/RNG/方向剩余、外控方向/射击、双方武器冷却、履带帧/位置冻结；出生继续但敌方不推进；冻结坦克可受弹死亡且仍占用阻挡注册空间。
- 玩家移动/射击、敌方在途弹、特效年龄继续；非终局玩家死亡/复活不取消Clock。
- Ready/暂停/失焦不拾取、不扣时；终局与退休/重开清全局效果，新会话读取Controller启动快照、无旧收据/分数/波次污染。
- Armor（含generic protection）批杀；同tick弹杀与手雷不会重复爆炸/计分；后生敌不重放手雷；敌弹仍在，玩家/基地/砖格未被手雷破坏。
- 自然携带者被手雷杀无首次弹伤掉落；预留/待生不杀，AliveCount本tick正确，Total=Spawned+Pending守恒，生成间隔/准备时间没有第二次推进。
- 最后自然敌同tick手雷胜利/事件一次；同tick基地失败拒绝手雷收据，最后敌弹杀+玩家耗尽失败优先；已有弹杀终局不得再领取Clock奖励。

## 真实Update与现场

新Play沿用BC304模式：真实Stage01 additive加载、Input System临时Keyboard，真实掉落算法有界选类型/可达位置，通过A键和 `ArenaController.Update`实际移动拾取Clock/Grenade；无反射伪收据、无生产作弊入口。测试额外注册一辆真实Armor和AI用于受控观察，不冒称自然波次携带者掉落或自然游玩通关。

`Console-Delta.json` seq1701/1835记录：首次frame13→84、最后frame13→92，均tick0→12；Clock冻结AI的pose/RNG/冷却/履带，暂停保留600、恢复后递减，真实Update新Grenade收据将Armor击杀并只计400分；另一个Play专项执行连续2次重开清理。

本轮没有新增截图/人工手感或像素级视觉验收；没有声称覆盖完整组合回归。输入设备由TearDown移除，临时加载场景卸载，无临时观察器。

## 编译、首次工具失败与Console

- `Compile-Status.json`：初次实现+Edit编译 completed / failed=false；`Compile-Final.json` 与最后 `Compile-Death-Final.json` 同为completed / failed=false / compilationFailed=false。无本轮C#编译失败或专项失败。
- 增加Play源码的reload窗口内，`Compile-Play-Status.json` / `Asset-Clock.json`是真实 `PIPELINE_UNREACHABLE`，没有假装执行成功；一次有界等待ready后 `Ready-After-Reload.json` / `Asset-Clock-Saved.json` 成功。
- 最后共享TryKill整理的编译，ready查询撞上短暂“Reloading Domain”对话框（`Ready-Death-Compile.json`），状态请求不可达（`Compile-Death-Status.json`）；一次有界8秒等待后 `Ready-Death-After-Reload.json` / `Compile-Death-Final.json` 成功。没有关对话框/重启/升级/持续恢复；后续专项全部成功。证据脚本有一次本地默认编码读取失败及一次路径转义语法错误，均在本地一次修正，不是Unity测试失败。
- Console起点cursor1688→1847：159条，全log，0新增warn/error；完整增量 `Console-Delta.json`，不是清空Console后的宣称。历史buffer error10/warn1保持。`Console-Final.json` 当前compiling=false / compilationFailed=false / consoleErrors=0 / consoleWarnings=0。
- `Final-State.json`：Stage01 stopped / not paused / clean，runInBackground=false。没有新增后台覆盖或更改ProjectSettings。当前包实际未提供autotick命令，真实帧/tick进展由Play断言和Console实证，不以“进入Play”代替证明。

## 保护与后续门禁

`Audit.json`对比起点296项hash：6既有文件如上发生预期改变，290项完全一致，含全部ProjectSettings/场景/PNG/地图/波次/升级数据与相机/HUD及BC304其余实现。新4个测试/meta单独列hash。`git diff --check`成功（Git有原工作区LF/CRLF转换提示，非Unity警告）；`Staged-Files.txt`为空，无暂存文件。Editor测试期间曾观察到UserSettings/HierarchyWindow.settings瞬时工作区提示，最终git diff/status已无该文件差异，未手工覆盖用户设置。

待父代理fresh独立review后裁决本lane；完整Clock/Grenade/Shovel组合回归留306集成阶段，本轮不勾选总任务、不自行验收。默认规则未原版校准、无人工/截图证据是剩余覆盖边界，不是自动专项失败。
