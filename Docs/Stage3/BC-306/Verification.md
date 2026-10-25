# BC-306 会话接线与定向验证

## 范围与结论

验证者：**subagent worker**；2026-10-25；Unity **6000.6.7f1** / CLI **1.0.0-beta.14**（沿用父代理已确认版本，未升级）。HEAD main / `bdb20b0e509276be1d4818c09638e5f419e463e4`。批次精确起点 `Temp/BC305306-Before`；本integration lane另外保留 `Temp/BC306-Integration-Before/{Hashes.json,Tracked.patch,Status.txt}`。

本lane只接线已交接的 `BaseFortificationState`，增加配置及集成专项；没有重写地图模型/BC305、修改Controller/View、场景、ProjectSettings、开展307以后、提交/推送或勾选总计划/总看板。BC304与305既有未提交成果、地图组件和 `.vsconfig` 保留。

**实际编译成功；本lane 26唯一最终Passed（20 Edit + 6 Play）**：新16 Edit + 2 Play，相邻4 Edit + 4 Play。不是全量回归、BC309组合回归、自然通关或人工体验验收。`Integration-Audit.json`按FullName去重；23项有逐项最终JSON，另3项激活修正的逐项完成JSON未及时获取，但精确filter请求与原始Console完成汇总 **3/3 Passed** 均保留，详见下文。不把running JSON伪装为完成，也没有因此机械重跑已成功项。

本阶段实现/专项无当前执行阻塞；后续由父代理fresh独立review及验收。所有规则仍是项目默认，未经原版核实。

## 最小实现与接缝

仅3个既有文件相对本lane起点变化（路径相对工程根）：

- `Assets/BattleCity/Scripts/Pickups/PickupDefinition.cs`：新增 `shovelDurationTicks`，默认 `BaseFortificationState.DefaultDurationTicks` 即1200；与既有持续时间一起作正数校验。删除已不适用的“Shovel未来实现”定义注释。
- `Assets/BattleCity/Scripts/Core/ArenaSession.cs`：新增只读 `BaseFortification`。仅同时启用基地与Pickups时构造组件，把持续时间数值快照交给它；无Pickups不隐式开启，无基地仍消费Shovel新收据但不创建隐形墙。复用Controller已有启动配置快照/新Session路径，不增加第二时间源。
- `Assets/BattleCity/Data/Pickups.asset`：通过Editor API `SetDirty` / `SaveAssetIfDirty`保存 `shovelDurationTicks=1200`。seed303、helmet600、clock600、maximumLives9、carrier索引3/10/17保持，原始API回读见 `Integration-Asset-Saved.json`。

新增：`Assets/BattleCity/Tests/EditMode/ShovelPickupIntegrationTests.cs`、`Assets/BattleCity/Tests/PlayMode/ShovelPickupIntegrationPlayModeTests.cs`，以及Unity Editor导入生成的两个meta；没有手写YAML。

### 单tick顺序

1. 既有坦克决策/移动/开火、全部弹道与伤害、Clock/头盔扣时不变。
2. 既有玩家生命/复活和**一次** `EnemyWave.Step`提交出生与当前预留，先作原有终局裁决。
3. 尚未终局时，重新收集当前全部活坦克及当前wave reservation的bounds，调用组件 `Step` **一次**，再进入掉落和新收据。
4. 仅本次 `TryCollect`成功的Shovel分支重新收集此时占用并 `Activate`；不重放 `LastCollected`。首次快照/重复刷新/毁钢再补/延期/原布局恢复均由已冻结组件负责。
5. 终局（伤害导致失败/胜利，或手雷后同tick胜利）的共享清理在停止Clock/命令之后 `Cancel`；`Stop`/`Release`也在停止战斗后 `Cancel`，强制精确恢复并清计时/全部待办。Controller退休旧Session并创建新实例，无模型复用。

因此T末拾取返回1200、不扣本tick；T+1至T+1200完整战斗阶段墙有效，末尾递减到0并恢复。Ready、开始过渡、暂停/失焦暂停不Step；玩家非终局死亡/复活不取消强化。占用由每次调用时的已结算实体收集，不使用历史缓存、不再Step波次、不挤/杀/传送占用者。

组件已批准的8px对齐且16px基地足迹在界内前置条件保持（Stage01 104,8符合），未扩大LevelDefinition通用校验。地图仍唯一逻辑真相，普通/最高级弹沿用既有Steel规则；ArenaMapView原revision增量投影无需修改，也未提前支持307地形显示。

## 本lane唯一执行集合与原始证据

所有下表前缀均在本目录；请求文件包含mode/filter/async元信息，完成JSON保留真实原件。

| 唯一集合 | 数量 | 原始结果/最终依据 |
| --- | ---: | --- |
| 新Shovel Edit，未受夹具修正影响部分 | 11 | `Integration-Edit-Result.json`，首轮16项中11 Passed / 5 Failed |
| 新Shovel激活占用夹具修正 | 3 | `Integration-Activation-Fix-Request.json`精确filter `Shovel_Activation`；`Integration-Activation-Fix-Result.json`真实running；`Integration-Console-Delta.json` seq1889/1890完成3 total / 3 passed / 0 failed |
| 新Shovel恢复占用夹具修正 | 2 | `Integration-Expiry-Fix-Result.json` |
| 新Shovel Play | 2 | `Integration-Play-Result.json` |
| 305手雷最后敌人终局、基地失败拒收 | 1 + 1 | `Integration-Grenade-Terminal-Adjacent-Result.json`、`Integration-Grenade-Base-Adjacent-Result.json` |
| 303终局清槽拒绝新拾取 | 1 | `Integration-Pickup-Terminal-Adjacent-Result.json` |
| 波次伤害后出生、下tick才行动 | 1 | `Integration-Wave-Birth-Adjacent-Result.json` |
| 305真实Clock/Grenade Update及重开 | 2 | `Integration-Clock-Grenade-Play-Adjacent-Result.json` |
| 304真实三奖励Update/显示/暂停 | 1 | `Integration-Player-Pickup-Play-Adjacent-Result.json` |
| 302最高级真实弹破钢/Tilemap与重开 | 1 | `Integration-Upgrade-Steel-Play-Adjacent-Result.json` |

合计 **26唯一最终Passed**。这些是本lane新执行集合，不把305原24项、map原49项或304原118项算成本次重跑。只复测改动接缝相关项，不全量重复组件/砖显示/升级/生命/波次历史成功集合。重复collector完成日志不重复计数。

### 新Edit覆盖

- 实际1200完整未来tick精确端点、配置数值快照、当前残砖mask5而非固定关卡布局恢复、收据不重放。
- 重复拾取刷新非叠加，不覆盖首次mask7快照；毁钢不会每tick主动再补，重复拾取才补。
- Ready/Start过渡、暂停/失焦与恢复无额外扣时。
- 活玩家阵营坦克、活敌坦克、wave当前预留分别延后激活；wave出生结算后仍由新活坦克占用，不造墙入体；离开后安全tick补钢。
- 到期残砖恢复分别受活坦克、wave预留延期；暂停保留恢复待办，释放活坦克后恢复准确mask，无到期后再补钢。
- 非终局玩家死亡/复活继续全局强化；基地失败与手雷同tick胜利都恢复并取消计时/待办，终局不再推进。
- 无基地消费收据但地图revision不变；无Pickups不隐式组件；0/-1配置拒绝；作者资产1200且仍待原版校准。
- Shovel与Clock/Helmet倒计时独立，Grenade仍只批杀敌人、不取消Shovel，未增加拾取分数。

## 真实帧/tick、Tilemap与生命周期

新Play加载真实Stage01 additive场景，使用Input System临时Keyboard、真实掉落算法有界选择Shovel类型/可达位置，用A键和 `ArenaController.Update`实际移动拾取。没有反射伪收据、生产作弊键或持久观察器。

`Integration-Console-Delta.json` seq1906：**frame13→38、tick0→1201**。拾取后1200，暂停期间frame继续而tick/强化剩余不变；恢复后实际Update至少3tick，剩余严格随tick差递减。为有界专项，中段通过公开 `ArenaSession.AdvanceFrame`推进至剩1，**不是20秒自然运行**；最后到期由真实Controller.Update推进及Render，确认逻辑mask5与实际Tilemap缓存partial Tile一致、sprite顶点8个，钢阶段为对应steel Tile，TilemapRenderer enabled。没有截图/用户手感/像素缩放声明。

第二个Play连续2次重开：分别退休active强化和**到期占位恢复待办**的旧Session，确认旧地图强制恢复mask7、计时/待办全清且再AdvanceFrame返回0；新Session Ready/1200配置启动快照/剩余0/无旧收据/波次未生成，Stage01原完整Brick与对应Tilemap Tile立即一致。故Stop/Release/重开均有实际Controller路径证据。

相邻305实际Update再次确认Clock冻结、暂停、手雷计分和退休清理；304实际Update确认星星/头盔/生命与HUD；最高级真实弹破钢显示/重开专项通过，不以数据断言冒称显示接线完成。

## 首轮失败、编译、Console与现场

- `Integration-Compile-Request.json` / `Integration-Compile-Result.json`，`Integration-Compile-Fix-Request.json` / `Integration-Compile-Fix-Result.json`：两次实际编译均completed / failed=false / compilationFailed=false，未发生C#编译失败或连接恢复。
- 首轮5个占用测试失败原件 `Integration-Edit-Result.json`保留：夹具中心80的坦克右界恰为88，和x11环格左界88**贴边无正面积重叠**，正确组件允许补墙。一次明确有界修正夹具中心为88，不改生产规则/断言目的。之后激活3/3、恢复2/2完成成功；未反复换方法或重跑其余11成功项。
- 激活修正请求后即时status还是running；其后独立恢复专项覆盖了共享status文件，因此少了该3项逐个最终JSON。原始精确请求+running响应+Console3/3完成原件仍在，Audit明确标记summary级证据，未杜撰结果文件。没有为了JSON闭环机械复跑成功3项。
- 初次本地保护hash脚本有Python路径转义SyntaxError，立即用Path.as_posix一次修正；未作用于Unity、不改源码/用户资产，工具原始失败保留于本会话记录。
- Console cursor **1875→2047**，172条全log，**本lane新增warn/error=0**；完整增量 `Integration-Console-Delta.json`。首轮NUnit失败是测试结果，不伪称首轮全绿。`Integration-Console-Final.json` 当前compiling=false / compilationFailed=false / consoleErrors=0 / consoleWarnings=0，历史buffer error12/warn1原样保留，未清Console。
- `Integration-Final-State.json`、`Integration-Final-Settings.json`：Stage01 **stopped / not paused / clean**，runInBackground=false，compiling=false。未改后台设置、autotick或ProjectSettings；所有测试键盘由TearDown移除、additive场景卸载，无临时观察器。测试执行中的真实frame/tick由断言及Console证明。

## Hash、源码与保护证据

`Integration-Audit.json`包含最终全部BattleCity源码SHA256、新增源文件/meta哈希、两层起点比对及逐个唯一测试证据定位：

- 对integration起点 **1755项既有hash**：仅前述3个既有文件改变，**1752项相同、0缺失**。含BC304/305源码和测试/meta、BaseFortificationState/MapState及组件专项、既有Docs、.vsconfig、全部ProjectSettings/场景/PNG/地图/波次/升级数据、相机/HUD。
- 对批次 `Temp/BC305306-Before` **296项**：仅7个既有文件为预期305/306变化（Pickups.asset、EnemyWaveState、ArenaSession、MapState、PickupDefinition、PickupState、TankState）；**289项相同、0缺失**。305和map新增实现/测试/meta另列哈希，不错误声称在原296项中。
- `Integration-Diff-Check.txt`为空，`git diff --check`成功。Git既有LF/CRLF转换提示不是Unity警告；未手动覆盖用户设置。`Integration-Staged-Files.txt`为空，无暂存；`Integration-Git-Status.txt`与 `Integration-Current-Tracked.patch`保留当前未提交现场（后者是相对HEAD，含前置304/305允许改动，非声称全部由本lane编写）。

未覆盖：人工试玩/原版校准、自然通关、截图或逐像素缩放、独立构建、长期压力、完整309规则组合；不提前验收这些内容。map组件原49Edit与其保护结论见 `Map-Handoff.md`；305历史实施与24唯一专项见 `../BC-305/Verification.md`，均不混入本lane新执行26项计数。父代理最后更新总计划/看板。
