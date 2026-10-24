# BC-201 候选实现与本轮验证

状态：**ready，等待父代理独立审查和验收**。本文件不代表BC-201已验收，不勾选总计划/执行看板，不开始BC-202。工程起点`main / 7de290635ca8b097022b379e911fbef1234baefc`，无提交、推送或暂存。

## 设计与范围

- 新增`TankState`，把稳定实体ID、阵营、唯一Motor/Weapon、16px Bounds、配置血量、通用保护开关和幂等死亡收束到同一逻辑实例。玩家/其他坦克均消费`TankCommand`，移动、动画、武器、受击和死亡不按阵营复制代码。配置数值在构造时复制；不修改TankDefinition资产。
- `ArenaSession.PlayerTank`是共享实例；保留原`Player`、`PlayerWeapon`、`LastCommand`接口及输入/60Hz时钟/暂停语义。玩家仍由既有PlayerInputBuffer生成命令；`AddTank`及`SetTankCommand`仅提供显式注册和外部控制接缝，不是AI、出生动画或波次系统。非玩家命令方向/FireHeld持续有效，FirePressed仅消费一次；暂停清空命令，暂停期间命令不排队。
- 会话内ID自1递增，玩家固定1；无ID复用、死亡移除或自动生成。非法/重叠注册不消耗ID。死亡实例保留以供读取，但不再移动、射击、成为目标或阻挡其他坦克。已有在途弹保留原Owner租约，继续按既有BulletWorld生命周期回收（非生命/复活功能）。
- 同tick先按ID依次移动；每个移动查询当前其他存活坦克的逻辑矩形。再按ID执行全部武器，生成移动后目标快照；全部子弹在同一旧快照上扫掠完成后，才结算地图与坦克伤害。一个目标同tick被多弹击中时所有弹都消费于原目标，不因较早死亡穿到后方；血量不下溢，只在存活→死亡时请求一次既有Explosion表现。
- `MapState.CanOccupy/MoveAxis`接受可选动态矩形，复用原半开/epsilon和连续轴扫掠；Motor的对齐终点、完整修正路径、渐进修正及前进都使用同一查询。所有阵营坦克互阻，精确擦边可通过，不引入Rigidbody/Collider或第二碰撞真相。
- `CombatRules.CanHitTank`统一自身/同阵营忽略、敌对命中规则，BulletWorld与受击阶段共用。通用保护状态消耗入射敌弹但不扣血；此处无自动保护时长/复活/道具策略。
- 唯一新增场景显示接入：主玩家死亡后隐藏既有SpriteRenderer。原场景/Prefab/asset均未改；场景仍只展示原玩家。真实敌方逻辑实例在新增测试中运行，不用伪造目标代替共享能力。

明确未实现：真实AI、波次、敌人视图、基地阻挡/受击/失败、玩家生命数/复活、会话胜负/重开、HUD、计分或阶段三能力。基地仍保留BC-200已有边界，不宣称第一关完整可玩。

## 变更文件

源码与测试（完整本轮文件清单另见`ChangedFiles.txt`）：

- `Scripts/Tanks/TankState.cs`及Editor自动生成meta：共享能力和死亡状态。
- `Scripts/Tanks/TankMotor.cs`：统一HalfSizePixels、动态占用路径、停止动画及非法出生坐标校验。
- `Scripts/Map/MapState.cs`：可选动态占用与连续互阻。
- `Scripts/Combat/BulletHit.cs`、`BulletWorld.cs`：统一阵营/自身过滤；其余扫掠排序与破砖算法保持原样。
- `Scripts/Combat/WeaponState.cs`：炮口半宽改引用同一TankMotor常量，数值仍10px，不改射击手感。
- `Scripts/Core/ArenaSession.cs`、`ArenaController.cs`：共享实例接入、稳定顺序、同tick伤害和必要死亡显示。
- `Tests/EditMode/TankStateTests.cs`及meta：新增24个实际逻辑用例。
- `Tests/PlayMode/TankStatePlayModeTests.cs`及meta：新增1个实际Update/敌命令/死亡显示/暂停用例。
- `Tests/EditMode/CombatEffectsTests.cs`：旧“命中不等于死亡”测试改为明确2HP非致死输入，并新增1HP及存活断言；原特效数量/种类/过期/弹回收断言全部保留。致死与同tick多弹幂等由新增用例覆盖，不删除或弱化断言。
- 本目录：本轮原始JSON、截图、集合比对、命令索引及验证记录。

以上源码路径均相对`Assets/BattleCity/`。主代理已有`Docs/BattleCity-Implementation-Plan.md`和`Docs/Stage2/Execution-Board.md`改动，以及既存`.vsconfig`，未由本writer改动。

## 实际编译与测试

CLI检查为`1.0.0-beta.14`且latest相同，Unity `6000.6.7f1`、同一已运行Editor/Pipeline ready；见`CLI-VersionCheck.json`、`Editor-Connection.json`。先读Unity CLI skill和运行验证reference，从实际目录发现分组命令和schema；保留`CommandSchemas.json`。没有使用外部代理、batch Editor、重启或其他执行协议。

所有`unity command`都附带：

```text
--caller plugin --skill unity-cli --project-path H:/Projects/Unities/BattleCity --format json
```

实际关键命令：

```text
unity command script recompile [上述公共参数]
unity status --until-ready --timeout 60 --project-path H:/Projects/Unities/BattleCity --format json
unity command script recompile_status [上述公共参数]
unity command test run [上述公共参数] -- --mode EditMode --filter BattleCity.Tests --async_tests true
unity command test run [上述公共参数] -- --mode PlayMode --filter BattleCity.Tests --async_tests true
unity command test status [上述公共参数]
unity command test list [上述公共参数] -- --mode all
```

完整逐条参数见`Commands.md`和对应JSON的parameters；编译请求异步启动后显式读取completed，不把status ready或compiling当编译通过。

| 验证 | 本轮实际最终结果 | 原始结果 |
| --- | --- | --- |
| 最终源码显式编译 | completed，failed=false，errors=[] | `ReleaseCompile-Result.json` |
| 全部自有EditMode | **227/227通过**，0失败/跳过 | `Release-EditMode-Results.json` |
| 全部自有PlayMode | **37/37通过**，0失败/跳过 | `Release-PlayMode-Results.json` |
| 执行集合核对 | **264发现=264执行**，missing/extra均空 | `TestDiscovery.json`、`Test-SetComparison.json` |

最终227中新增共享专项24；37中新增共享专项1，共新增25项，不把专项重复运行累计为新增数量。最终全量包括：Phase1移动/时间/暂停24、转向65、武器15、扫掠38、破砖33、Stage01地图6、渲染delta战斗6、逻辑特效9、Phase0 7，以及PlayMode Arena5、砖显示22、特效显示5、真实Update回归1、Stage01实际场景3和新增共享1。完整各夹具计数在`Test-SetComparison.json`。这些是本轮实际结果，**未引用历史226/226或117/117冒充本轮回归**。

### 保留的非成功过程

1. 初版新增专项22/22通过（`TankState-Results.json`）。随后补2个同tick死亡/在途弹用例后24项中23通过：`Death_ReleasesMovementOccupancyButNotOutstandingProjectileLease`预期玩家y64实际y48，因为测试发射者的Down命令仍持续移动，真实互阻把玩家挡住。仅在测试射击准备后显式发送Idle，保留y64及回收断言；生产逻辑未为该失败修改。旧失败在`TankState-Final-Results.json`。
2. 初次全量EditMode226/227，旧`TankHit_IsNotAutomaticallyADeathExplosion`把1HP坦克受击仍只显示Hit当作前置条件。BC-201现在确实结算死亡，新增Explosion使数量为2。将该测试命名为`NonlethalTankHit_IsNotAutomaticallyADeathExplosion`，配置2HP并断言受击后1HP且存活，保留所有原特效断言。致死由新增测试严格检查只出现一次Explosion；旧失败保存在`EditMode-All-Completed.json`。
3. 修复测试后第一次全量227/227、37/37（`EditMode-All-Final-Results.json`、`PlayMode-All-Results.json`）。最后把武器炮口常量统一为TankMotor.HalfSizePixels后再次显式编译并重跑上述Release最终全量，仍227/227、37/37。所有失败/重跑原始报告保留，没有清Console、删除失败报告或放宽断言。

本轮无编译失败。初次`Compile-Result.json`为compiling，之后`Compile-Completed.json`确认completed；不以初次响应冒称完成。无CLI/模型/运行基础设施失败。

## 真正的运行推进与暂停证据

新增PlayMode测试实际加载CombatArena，经过真实ArenaController.Update消费非玩家TankCommand并扫掠伤害玩家，验证主玩家死亡、隐藏Renderer、只出现一次Explosion、弹回收、无物理碰撞组件；随后暂停等待实时0.25s，帧仍增加，而tick、敌位置、冷却、爆炸Age保持不变。最终Console原始日志：

```text
BC201 realShared frame=1106->1112->1233 tick=0->1->1 playerHP=0 enemy=2:(71.00, 24.00) cooldown=15 explosionAge=0
```

因此不是仅进入Play或仅手动调用Step的证明。共享移动/对齐/武器/多弹/保护/死亡矩阵还由24项真实逻辑实例测试覆盖。

另独立启动原Stage01正常会话，按skill运行循环：后台/autoTick准备→进入Play并ready→显式聚焦/恢复Session→server-side wait frame→wait tick→相机capture→Console→暂停/停止。

- `Live-Frames.json`：frame **2851→2894**，met=true，framesObserved=2。
- `Live-Ticks.json`：tick **511→517**，met=true，framesObserved=2。
- `Live-Pause-Before/After.json`：frame **17611→18976**，tick都为**2185**，paused=true；`Live-PausedFrames.json`另保存暂停期间服务端帧推进。
- `Stage01-Live.png`是768×720实际相机画面；已打开查看，显示既有玩家、地图和完好基地，不声称有可见敌方或胜负功能。截图不是tick推进证据。
- capture命令即使传绝对Docs目标仍返回`Assets/Docs/Stage2/BC-201/Stage01-Live.png`；按其真实路径读取后，经Editor API复制证据至本目录并只删除本轮capture及空目录。见`Live-Capture.json`、`Capture-Cleanup.json`，未留下Assets/Docs临时资产。

独立Stage01截图/推进在最终炮口等值常量收束之前采集；最终源码仍由Release全量中的真实Update/暂停/Stage01/新增共享PlayMode用例重新验证。未把截图冒称最终源码另一次独立场景运行。

## Console、设置与Git

- 基线cursor **77**，session `316dee6ba1864c7dab134a9af37d8ce3`；最终cursor **217**，保存140条增量，**新增error=0、warn=0**，无丢失/游标重置。见`Console-Baseline.json`、`Console-Cursor.json`、`Console-Final-Since77.json`。
- 历史buffer error=1保留为1，不冒称历史日志全无错误。两次Test Runner断言失败在结果JSON中准确保留；其Console通知属于普通log，不据此宣称当时测试通过。
- 最终groundTruth：compilationFailed=false，compiling=false，consoleErrors=0、consoleWarnings=0（`Console-Final-Status.json`）。
- 临时后台开关已还原为Application.runInBackground=false、PlayerSettings.runInBackground=false并SaveAssets；仅本轮产生的Pipeline配置在确认override关闭后移除。autoTick设置时返回already enabled，只使用session-only 16ms，不更改persisted选择。
- `Editor-FinalState.json`：非Play、非Pause、Stage01 clean、临时Pipeline配置不存在。ProjectSettings、场景、资产、UserSettings无本轮残留diff。新代码meta由Editor导入自动生成，没有手写YAML/meta。
- `Git-Final-Status.txt`、`ChangedFiles.txt`、`Git-Diff-Check.txt`、`Git-Staged-Files.txt`记录最终状态；`git diff --check`通过，新增源码尾空白检查通过，`git diff --cached --name-only`为空。Git的LF→CRLF提示是既有行尾配置，不是Unity编译警告。

## 未覆盖与下一门禁

尚未执行本版本用户人工手感确认、独立构建、长期压力、敌方显示/真实AI和波次、生命复活或完整第一关胜负流程；这些既非BC-201实现范围，也不从历史报告借用通过结论。当前单场景只有玩家，新增敌方通过逻辑实例与实际Update专项运行证明，不宣称已交付敌人玩法。

建议下一步：父代理以本轮源码、Release结果、264集合核对及原始失败记录进行fresh只读审查，再独立决定BC-201是否验收。未经该门禁不进入BC-202。
