# BC-202 基础敌人状态机：实现与验证

状态：**ready，候选实现等待父代理fresh只读审查及验收**。只完成BC-202，不代表阶段二/M2完成；未修改总计划复选框或执行看板，未启动BC-203。

## 起点与范围

- 工程`H:/Projects/Unities/BattleCity`，`main / 7de290635ca8b097022b379e911fbef1234baefc`。先读取Git状态，保留既有BC-201未提交源码/测试/证据、父代理计划/看板/Acceptance及`.vsconfig`。`Git-Start-Status.txt`、`Git-Start-HEAD.txt`记录本项起点，不把既有BC-201改动算成本项新增。
- `Start-Hashes.json`记录本项修改前Assets/BattleCity全部文件、BC-201历史证据、计划/看板和`.vsconfig`的SHA256；`Item-Delta.json`比较结果：仅3个既有源码发生本项变更，无原文件缺失，另新增4个源码/测试及Editor生成的4个meta。BC-201历史记录、原场景/资产、计划/看板和`.vsconfig`哈希保持原值。
- `Item-Implementation-Delta.patch`精确列出**相对本项起点**的3个共享源码增量，而不是相对HEAD混合BC-201的diff。该patch的旧内容均已反算SHA256并与起点记录校验一致。新文件直接见源码。
- 无暂存、提交、推送或递归委派。`Git-Staged-Files.txt`为空；`git diff --check`通过。新增源码另做尾空白检查通过。

## 实现契约

### 显式注册与控制所有权

`AddTank`仍返回唯一共享`TankState`，不自动启用AI。调用`RegisterEnemyAI(entityId, nonzeroSeed, parameters, initialDirection)`才把已有存活敌方坦克交给AI；同一实例的Motor、Weapon、生命及碰撞继续使用BC-201能力，没有复制战斗实现。重复注册、玩家注册、死亡注册和不存在ID被拒绝；零种子被拒绝且不占用注册。默认初始意图Down，可显式指定四向。

未注册的非玩家仍可由`SetTankCommand`控制，原有BC-201用例保持原样并全部通过。已注册AI明确拥有该坦克命令，外部SetTankCommand抛出异常而不是静默覆盖。无出生策略、自动生成或波次管理。

### tick开始查询 → 全部决策 → 统一移动

`ArenaSession.Step`先收集**全部**AI的当前局部移动可行方向掩码，期间没有任何坦克移动、武器或地图修改。查询只读取当前Motor/存活坦克矩形及逻辑地图，不读取玩家Command或未来输入。掩码收集完成后，按单调稳定Tank ID调用各自Brain生成命令，然后才进入原有稳定ID移动、武器、子弹扫掠及伤害阶段。后ID决策不能观察早ID本tick已经移动的位置。

决策快照是当前查询结果的值列表，不保留会随移动改变的引用。实际移动仍沿用BC-201顺序互阻：后ID移动可以受前ID已结算矩形阻挡，这不等于允许其决策偷看移动结果。

最小共享扩展：`TankState`只增加内部只读速度查询；`TankMotor`把原Step计算提取成无副作用的`CalculateStep`，内部`PreviewStep`复用**同一**转向/对齐完整路径/扫掠规则。实际Step和查询不能各维护一套碰撞真相。地图、武器、伤害矩阵、时钟、输入和暂停冻结规则未改。

### 轻量状态机与原型默认

`EnemyBrain`仅有Moving / Blocked / Dead：保持当前意图；局部受阻或方向计时到期时从可移动四向按权重抽取新意图；四面封死时保留朝向、每tick重试并继续射击，不无意义地消耗随机数。移动或被困时使用FireHeld，由原有WeaponState统一执行冷却/在场弹数/真实砖墙破坏；不引入AI专用子弹或假命中。死亡不再推进决策次数、随机数、方向计时、移动或新射击；既有在途弹仍按BC-201租约规则自行结束。

所有数值都是**未校准原型值，不是已核实原版参数**：

| 参数 | 默认/算法 |
| --- | --- |
| 初始方向意图 | Down；注册参数可选择四向 |
| 初始方向计时 | MinDirectionTicks，即60tick |
| 后续方向时长 | 60～180整数tick（含两端）；每次活体决策先把剩余计时减1，到0重新抽取 |
| 方向权重 | Down=4；Up/Left/Right各1；只在当前可移动集合中归一化 |
| PRNG | 每个Brain独立xorshift32，明确非零uint种子；左移13、右移17、左移5；不使用UnityEngine.Random |
| 抽样顺序 | 每次重新选向固定先方向、后时长，按模映射；无渲染/系统时间输入 |
| 射击意图 | 存活时持续FireHeld；武器参数继承显式注册TankDefinition，本项未新增敌型配置资产 |
| 校准标记 | 不可变EnemyBrainParameters及RequiresCalibration=true；默认Prototype单例 |

参数范围与权重总和有校验，避免非法区间和求和溢出。InitialSeed、RandomState、Direction、DirectionTicksRemaining、DecisionTicks及State可读取/测试。不做NavMesh、目标路线搜索、基地寻路或玩家未来输入预测。局部随机AI可能在复杂地形中徘徊，不保证最短路或最终到达基地。

### 暂停与渲染时间

AI只在现有60Hz逻辑Step内推进，不新增Unity Update/Time.time/系统时间计时源。暂停不调用Step，方向计时/PRNG/决策次数和战斗一起冻结；恢复后重新从当前状态决策，不补算暂停壁钟时长。已有SetPaused清空命令语义保留，AI恢复后的正常tick会重新产生命令。

## 本项变更文件

路径相对`Assets/BattleCity/`：

- 新增`Scripts/AI/EnemyBrain.cs`、`EnemyBrainParameters.cs`及Editor导入生成meta。
- 最小修改`Scripts/Core/ArenaSession.cs`：显式注册、命令所有权、移动前全量查询/稳定ID决策。
- 最小修改`Scripts/Tanks/TankMotor.cs`、`TankState.cs`：共用无副作用移动预览、内部只读速度。
- 新增`Tests/EditMode/EnemyBrainTests.cs`及meta：29个实际用例。
- 新增`Tests/PlayMode/EnemyBrainPlayModeTests.cs`及meta：1个真实ArenaController.Update专项。
- 本目录：起点/增量、原始编译与测试结果、发现集合核对、Console、恢复现场、命令及验证记录。

所有meta均由运行Editor导入自动生成；未手写序列化资产，未修改场景/Prefab/TankDefinition/LevelDefinition资产或ProjectSettings。

## 实际验证

先读取Unity CLI skill、integration及playmode-verification-loop reference。CLI `1.0.0-beta.14`检查为最新，原Editor Unity`6000.6.7f1`、PID26924、端口7800、原工程ready；见`CLI-VersionCheck.json`、`Editor-Connection.json`。从实际分组目录发现命令，再读取实际schema；本版本使用`script recompile`、`test run`、`console get`等分组名称，未照抄旧版示例名。schema与关键命令见`CommandSchemas.json`、`Commands.md`。

**所有unity command调用均带`--caller plugin --skill unity-cli --project-path H:/Projects/Unities/BattleCity --format json`。**

| 最终验证 | 本轮实际结果 | 原始证据 |
| --- | --- | --- |
| 实际源码编译 | completed，failed=false，errors=[]，compilationFailed=false | `Recovery-Compile-Result.json`、`Release-Compile-Result.json` |
| 全部自有EditMode | **256/256通过**；0失败/跳过/不确定 | `Release-EditMode-Results.json` |
| 全部自有PlayMode | **38/38通过**；0失败/跳过/不确定 | `Release-PlayMode-Results.json` |
| 发现/执行集合 | **294发现=294执行=294唯一名称**，missing/extra/nonPassed均空 | `TestDiscovery.json`、`Test-SetComparison.json` |
| BC-202新增 | EditMode29、PlayMode1，共30项；不累计重复专项运行次数 | 上述集合报告及源文件 |

以上256/38是本项最终实际重新执行的全量，不是引用BC-201的227/37。全部既有264项均包含在294项中，包括65项转向、38项扫掠、33项破砖、阶段0/1地图/输入/武器/显示、BC-108跨delta及BC-201共享坦克回归。夹具详细数量见集合报告。

29项新增EditMode覆盖：显式控制保留/同一共享实例/非法注册；原型参数校验；4个种子的精确PRNG/方向权重/含端点时长；四方向边界长程运行；四方向砖/钢遇阻；横纵16px窄通道与尽头折返；动态坦克互阻1000tick；后ID看不到前ID本tick腾空位置；不读取当前待执行玩家命令；四面封死可射开真实砖墙；死亡冻结；暂停500个超长渲染delta后恢复仅1tick；同种子2000tick逐tick位置/朝向/履带/射击/随机/计时状态一致，且Unity全局Random不变；模拟30/60/144渲染delta各12秒均与720tick参考完全一致、DroppedTicks=0。最后三项是**输入渲染delta模拟，不冒称硬件fps性能基准**。

### 原始失败、修复和一次批准的环境恢复

1. 初版编译完成，无代码错误，`AI-EditMode-Results.json`为29/29。首个Play专项在CombatArena把敌人放在`(200,104)`，该位置与试验场钢墙`x192..200/y48..144`重叠，按既有合法出生校验抛出ArgumentException。失败保留于`AI-PlayMode-Results.json`，不删除或当作通过。
2. 仅修正测试出生点为同一试验场合法边界`(200,24)`，保留真实移动、转向、射击、暂停/恢复、无补算的全部断言；未改生产逻辑或地图/场景来迁就该测试。修正后`AI-PlayMode-Fixed-Results.json`为1/1通过，最终全量再次覆盖该专项。
3. 修正后第二次显式重编译请求已进入compiling，随后`status --until-ready`返回`STATUS_BLOCKED_BY_DIALOG / Reloading Domain`，再读编译状态返回`EDITOR_NOT_RUNNING`。保留`Compile2-Request.json`、`Compile2-Result.json`、`Recovery-BlockedStatus.json`、`Git-Blocked-Status.txt`。没有静默重试循环、重启Editor、强制关闭对话框或换执行模式。
4. 只读`pipeline list`随后显示原PID26924/7800重新可达、非SafeMode（`Pipeline-Blocked.json`）。联系父代理并获准**一次同协议恢复**，只允许同工程/原PID端口确认→有界status ready→重新确认schema→实际编译结果；若仍阻塞则停止。执行结果`Recovery-Ready.json`确认原实例ready，`Recovery-CommandSchema.json`确认命令，`Recovery-Compile-Result.json`确认completed/failed=false。随后才继续专项与最终全量。不是从未失败的环境，也未把compiling/ready等同编译通过。
5. pipeline list提示包`0.9.0-exp.1→0.9.1-exp.1`可更新，已告知父代理；本项**未升级**CLI/Pipeline、未重启/替换实例、未切换运行协议。

## 真正的Update推进、暂停与恢复

新增PlayMode测试加载既有CombatArena，显式AddTank注册真实共享敌方，再RegisterEnemyAI；测试从未手动调用AdvanceFrame/Step。实际ArenaController.Update负责持续推进。最终Release运行的Console记录：

```text
BC202 realAI frame=666->822->973->982 tick=0->20->21 decisions=20->21 random=2533361647->2533361647 position=(180.00, 24.00)->(179.00, 24.00) cooldown=0 active=1 resumedTicks=1 dropped=0->0
```

- frame666→822：AI实际完成20次决策，敌人从`(200,24)`向外初始意图遇边界后转向，移动至`(180,24)`，真实武器有1发在途弹。
- 暂停实时0.3秒，frame822→973仍增加，而tick=20、位置、Brain决策次数/随机状态/方向计时及武器冷却/弹数均冻结（逐项断言）。
- 恢复后真实Update推进到tick21，决策次数同步+1，位置变`(179,24)`，DroppedTicks仍0，不积压补算。
- 早期修正后专项另有frame12→133→234→241 / tick0→20→21记录，也保留在Console，不混称最终Release那一次。

因此不是仅进入Play或依靠静态截图认定运行。此测试无敌方批量显示，未给Stage01加自然敌人或为截图改场景；本项不需要伪造敌军可见性。

## Console与临时设置

- 基线cursor **217**、session`316dee6ba1864c7dab134a9af37d8ce3`；最终cursor **286**，69条完整增量均为log，**新增error=0、warn=0**，dropped=false/reset=false。见`Console-Baseline.json`、`Console-Final-Since217.json`。历史buffer error=1仍保留，不宣称历史从无错误。
- Test Runner的首个断言/异常失败准确保留在结果JSON中；Pipeline只将其完成通知写普通log，不用Console增量0掩盖测试曾失败。
- `Console-Final-Status.json`：compilationFailed=false、compiling=false，当前Editor Console新增错误/警告0。
- `Editor-StartState.json`与`Editor-FinalState.json`均为runInBackground=false、PlayerSettings.runInBackground=false；最终非Play/非Pause，原Stage01场景clean，未留下临时Pipeline配置。
- 按运行reference调用`editor set_auto_tick enable=true interval_ms=16 persist=false`，返回原本already enabled，仅session-only更新16ms；没有改用户persisted选择。未修改背景设置，不需要恢复项目开关。场景/资产起点哈希与最终增量检查一致；ProjectSettings/UserSettings无Git残留diff，背景开关由前后Editor读取确认一致。
- Git LF→CRLF提示来自既有行尾配置，不是Unity编译或Console警告。

## 未覆盖与下一门禁

未做本版本用户人工手感确认、独立构建、硬件30/60/144fps性能基准或长期压力；不从历史用户反馈借用通过结论。未实现自然波次/总数/上限/出生动画、敌方批量视图、基地失败、玩家生命/复活、会话胜负/重开、HUD或BC-203以后功能。局部AI不承诺复杂地图全局可达性，所有AI参数仍待玩法校准。

建议父代理对本项相对起点delta、新增源码及最终294执行集合进行fresh只读审查，再决定BC-202验收；本writer在BC-202停止，不改总看板或开始下一项。
