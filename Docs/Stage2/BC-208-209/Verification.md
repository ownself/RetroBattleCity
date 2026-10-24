# BC-208 / BC-209 最小查缺交付（writer，待独立review与主代理验收）

## 结论与范围

基线 `main/eb858f3579626b5e66ba253afb94cdb0240118f2`，Unity 6000.6.7f1，当前已连接PID26924/port7800；按已确认最新beta14 CLI使用实际schema，未升级、重启或另开Editor。

- **BC208：**八项边界均找到真实成功原始证据及当前源码兼容依据，见`Coverage.md`、`Raw/reused-cases.json`、`Raw/source-compatibility.json`。精选复用20个唯一历史成功用例，不是重新执行20例。没有为任务名重复造模型专项。
- **BC209：**胜利和基地毁坏复用本轮用户明确的当前版本人工正面反馈（原话见`Contract.md`），不冒称逐项量化/有截图或Console零错。唯一真实自动缺口补为当前Stage01三次出场→生命耗尽Lost→物理R回Ready：一个新增受控PlayMode用例一次 **1/1通过**，直接相邻的真实Update生命/盾专项一次 **1/1通过**。没有重新自然打完胜利，没有全量/历史专项/像素矩阵重跑。
- 源码兼容核对：206最终68文件hash中仅6份有BC207已验收的变化（4个asmdef、Stage01Authoring、ArenaController显示接缝）；所有核心规则及既有测试原样。207生命周期10 Edit/3 Play结果为当前接缝证据。本轮仅新增1测试及Editor生成meta，无生产源码/场景/资产/Importer/设置改动，不更改计划/看板/旧证据。
- 以上是提交审查的证据满足判断，**不自行勾选BC208、BC209或M2验收**；fresh reviewer gate待主代理安排。

## 三流程证据矩阵

| 流程 | 人工 | 历史自动 | 本轮受控自动 | 剩余缺口 |
|---|---|---|---|---|
| 敌军清零胜利 | 用户本轮表示胜利正常；无逐项量化记录 | 206真实共享子弹最后敌清零与同tick冲突；207HUD Won/敌数0 | 无，按契约不重复自然通关 | 本契约下无补测需求；不能据此声称自动自然通关 |
| 基地毁坏失败 | 用户本轮表示基地毁坏正常 | 207复核SessionLifecyclePlayMode真实Update基地弹击毁→Lost→R Ready；206模型完整伤害结算 | 无，不重复用户游玩 | 本契约下无补测需求 |
| 三次出场耗尽失败 | 用户反馈未明确涵盖此项，不扩张 | 206三代小fixture；真实Update单次死亡/复活；207单生命HUD终局 | 新Stage01实际Update三次出场消耗→Lost(仅PlayerLivesExhausted)→键盘R Ready | 已补齐受控整合证据；不是自然游玩 |

人工反馈验证者为用户、本轮2026-10-24、当前HEAD版本，引用范围限Contract所记内容；不伪装成TestRunner数量。旧流程原始证据精确用例见Coverage映射。

## 本轮新增用例与结果

`Assets/BattleCity/Tests/PlayMode/Stage01LifeExhaustionPlayModeTests.cs`：

`BattleCity.Tests.Stage01LifeExhaustionPlayModeTests.RealUpdate_ThreeStage01LivesExhaustedByControlledEnemyBullets_LostThenPhysicalRReady`

- 通过真实Stage01场景和ArenaController.Update推进，不调用AdvanceFrame/Step手动推进世界，不直接写生命/Phase/坦克HP，不改玩家生命、延迟、保护、关卡、伤害或波次规则。
- Enter/R使用InputSystem测试键盘边沿。只受控放置敌方WeaponState炮口，让真正敌弹从玩家中心进入生产共享子弹碰撞/伤害路径；使用玩家定义的临时克隆，参数未修改。此是**受控弹击整合验证，不是自然敌军瞄准/自然游玩**。
- 每代真实死亡扣一次生命，等待60tick复活、120tick保护结束后再发下一弹；中途基地一直存活。终局失败原因精确为PlayerLivesExhausted、通知仅1次、HUD0/LOST、tick冻结。R后新Session Ready/tick0/3生命/20待出场/基地完好。
- 原始结果`Raw/life-result-02.json`：1 total/1 passed/0 failed，case 6.3762453s。`life-start.json`是异步启动的0结果，不算通过；`life-result.json`只是running。
- `Raw/console-after-life.json`真实日志：死亡tick **2/183/364**，生命 **2/1/0**；复活tick **62/243**；frame **12→2687**，tick **1→364**，restartTick0，outcomes1，droppedTicks0。
- 捕获两张实际测试GameView画面`LifeExhausted-Lost.png`与`LifeExhausted-R-Ready.png`，已读取检查Lost/生命0及Ready/生命3可见。测试自己的EndOfFrame屏幕捕获，不是假装独立自然游玩截图；未新增Playing图/强制窗口尺寸。复用207的`Ready-512x480.png`、`Ready-1000x700.png`、`Paused-1000x700.png`与像素对比原始证据，未重复像素矩阵。

直接相邻复活接缝只跑一次：
`BattleCity.Tests.PlayerLifeIntegrationPlayModeTests.Stage01_RealUpdateSharedEnemyWeaponDeathHiddenWaitRespawnShieldExpiry_PauseAndFocusFreeze`，`Raw/adjacent-result.json` **1/1通过**，3.3721637s。日志frame13→1416/tick0→181/lives2/dropped0。只增加测试没有生产blast radius，不额外例行跑其他历史集合。Console里Run started 67是发现集合/collector日志，最终精确过滤结果各1，不将其算作67次执行。

计数：历史精选复用20唯一用例；新增1用例执行1次；相邻既有1用例执行1次（已在精选20中），本轮2执行/2成功/0失败；证据并集21唯一用例。另复用2个人工流程反馈和207三张图，无全量全绿声明。

## Console、失败保留及现场

- `Raw/console-before.json` cursor776；最终`Raw/console-final.json` cursor800，同session `316dee6ba1864c7dab134a9af37d8ce3`，24条全部log，dropped/reset=false，无新增warn/error/exception，compilationFailed=false；历史累计error5/warn1保留未清空。
- 非测试错误完整披露：本地宽范围skill路径查找20s超时后即停止，通过206文档精确路径读skill；组合query使用管道字符得到0个Editor命令（`Raw/schemas.json`），随后各单词准确发现schema，未当通过。一次本地JSON解码用了Windows GBK默认导致UnicodeDecodeError，改UTF-8后只读成功。上述非Editor故障，不循环排障。
- `Raw/test-observation.json`是一条在测试结束附近发出的只读观察eval，错误假定已停止Controller的Session非null，CLI报告NullReference；未修改任何对象，未为此重跑测试或追图。以实际测试通过JSON、日志与图为证据，不将失败eval伪装成功运行核验。该命令错误不在Console增量内，所以“Console无新增error”不等于“所有工具命令无失败”。
- 本轮无测试失败，无生产缺陷，无阻塞自动化/等待硬超时，无须用户重复已通过流程。若fresh review发现新缺口，届时只补那个缺口。
- `Raw/editor-before.json`与`Raw/editor-after.json`：用户原来已停止Play、未暂停、Stage01 clean；最终相同，compiling=false。Application及PlayerSettings后台均false，AutoTick原有true/16ms未写，SessionState观测false/-1一致。未切GameView尺寸、未开独立Play现场、未添加observer；临时键盘TearDown移除、临时TankDefinition销毁、测试加性场景卸载、TestRunner临时InitTestScene已自动清理（最终git无残留）。
- `Raw/git-after.txt`、`Raw/staged-files.txt`：index为空，保留既有`.vsconfig`及主代理Contract/Execution-Board；仅新增本测试/meta和本目录worker文档/证据。`git diff --check`通过；对未跟踪新测试额外`git diff --no-index --check /dev/null ...`通过，仅Git LF→CRLF advisory，没有手写YAML/meta。
- 命令参数在原始JSON及`Raw/commands.jsonl`，后者记录从import开始的真实ISO起止/exit/duration；早期schema/preflight命令未纳入该计时log，但原始结果包含参数。`TimeLog.md`记录本轮首个时钟观察起点到文档收口，不虚构净排障时间。

## 待审与最小后续

208八边界、209三流程/帧tick/图/Console/清理均已有本契约要求的证据；没有要求用户补自然胜利、基地毁坏或重复已通过自动用例。请fresh reviewer只读新测试、Coverage复用依据、原始结果与图，再由主代理决定208/209/M2验收；不提交/推送，不进入阶段三。
