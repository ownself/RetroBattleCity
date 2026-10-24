# BC-203 integration 验证与交接

状态：**实现/自动验证 ready，等待 fresh 独立审查及父代理验收**。不是 BC-203/M2 已验收，不代表用户人工体验通过。

## 起点、所有权与恢复事实

- 起点 main / `7de290635ca8b097022b379e911fbef1234baefc`；BC-201/202、wave/view均是已存在的未提交工作，不归因本 integration writer。开始前记录 `Integration-Starting-State.json`（Git状态与既存文件SHA256）。没有暂存、提交、推送、递归委派或开始BC-204以后。
- 本项仅桥接既有 EnemyWaveState/EnemyTankView，修改 ArenaSession/ArenaController、Stage01Authoring、实际Stage01数据与场景绑定，以及相应集成/受影响回归测试。没有重写波次或视图组件，也没有修改其源码/测试。
- 原 writer 执行达到 **1800000ms（30分钟）超时，进程被终止**，未完成交付，不是任务验收成功。父代理确认旧writer无活跃进程并通过同subagent协议、同模型 `openai/gpt-6.1-sol` 明确批准有限收尾恢复；恢复快照为 `Temp/BC203-Recovery/Tracked.patch`、`Snapshot.json`、`Untracked/`。
- 恢复阶段优先补本文及必要临时配置清理/只读状态核实；不重做组件、不盲目重跑已有全量。最终编译/测试对应实现文件与恢复快照进行哈希核对，恢复核实结果见 `Integration-Recovery-Verification.json`。父代理在超时后更新总计划/执行看板，文档hash变化不归因writer，不回滚。

## 组件API消费与集成差异

### Session

- 保持 `new ArenaSession(level, player)` 无自然波次的纯逻辑行为。新增可选第三参数 `EnemyWaveDefinition wave = null`，显式传入才构造会话独占 `EnemyWaveState`；构造时验证完整映射/序列，配置错误不延迟到部分生成后。
- 消费 wave API：`new EnemyWaveState(level, wave)`、`Step(aliveCount, isOccupied, tryCreate)`、`Reservation`与计时/计数属性；不另建时钟、随机数或死亡计数账本。新增 `EnemyWave`、`AliveEnemyCount`、`GetWaveEnemyDefinition(long)` 供宿主显示/后续任务读取。后者仅为真实成功自然出生的共享坦克返回其定义。
- 原有所有AI观察/决策仍先于任一坦克本tick移动；所有扫掠后结算伤害/死亡，再推进wave一次。创建回调先完成 Brain/TankState 构造和验证，之后一次性登记共享状态、AI、定义及单调ID，再返回true；只有true才由wave提交序列计数。没有在提交后调用可抛异常的外部事件/注册回调。新敌不会倒流进入本tick移动/射击，下一tick才行动。
- 出生占用使用同一 `Map.CanOccupy` / 16×16中心盒 / 半开接触规则，合并地图、基地保留足迹、玩家与存活坦克；忽略wave自己的在途预留以免自阻。建立预留前及最终物化前均检查。预备结束时被玩家占用则持有原请求等待，不挤开玩家、不重置准备、不吞计数。
- 当前只有wave一个出生预留系统，最多一个在途预留。显式 `AddTank` 在启用波次时也不能偷占其预留足迹或使存活敌+预留越限；移动并不被出生提示人为阻挡，因此预备结束必须复查。
- `Total=Spawned+Pending`；`Pending=Unreserved+Reserved`，Pending包含预留；Spawned是累计成功创建数，死亡不扣减。每tick用真实死亡后活体数，不发送可重复死亡事件。`SequenceExhausted`不等于胜利。

### Controller / view

- Stage01控制器绑定wave资产默认启用；CombatArena仍未绑定、没有自然波次。
- 只为成功出生的真实共享实例建立 EnemyTankView，消费 `Initialize(tank, definition, sprites, sharedMaterial)` / `Render()` / `Clear()`。每渲染帧在逻辑结算后渲染；玩家仍用原renderer。位置/朝向/履带由同一Motor读取，不反写逻辑、不引入Collider/Rigidbody或第二动画时钟。
- 出生预备提示使用已存在语义 `effect.spawn.0..3`，以wave准备剩余tick只读选帧；待占用时停留末帧。核对 SpriteInventory.csv 第274～277行，无新造图或重新切片。
- 死亡清理对应敌方view；禁用隐藏/销毁敌方view与预备提示、暂停会话、清空在途弹并释放Owner租约；重启用仍暂停，仅从当前活体重绑。销毁宿主组件（不仅整个GameObject）也销毁会话绑定的BulletView/EffectView子对象，不留下旧实例引用。不是完整会话重开。

## Stage01原型配置与作者recipe

- `LevelDefinition.enemySequence` 唯一提供20个 `enemy.basic.prototype`，`maxActiveEnemies=4`；没有在wave资产复制总数/上限。Stage01Authoring.Populate可重建同一原型序列。
- 新 `EnemyBasicPrototype.asset`：白色基本敌前缀 `tank.white.enemy.basic`，移动60px/s，弹速180px/s，冷却15tick，在场弹1，HP1，score0（未实现计分），requiresCalibration=true。沿用已有共享TankDefinition能力，未宣称原版精确参数。
- 新 `Stage01EnemyWave.asset`：映射上述唯一TankDefinition、显式seed=1、preparationTicks=60、spawnIntervalTicks=120、requiresCalibration=true。作者recipe通过Editor API创建/验证资产并复制原CombatArena harness绑定Stage01；未保存/修改源CombatArena。
- **父代理冻结的端点**：Step#1建立年龄0预留，其后60tick准备，首次成功出生 **#61**；成功后等120tick，在#181建立下一预留，再准备60tick，第二次成功 **#241**，无阻成功间距180tick。不是“每120tick直接创建”，也不是原版事实；阻塞则逐战斗tick复查同一请求，成功才开始间隔。
- 保留26×26/676格、原始参考PNG、布局Y翻转/出生坐标、像素相机/PPU16/既有材质/Tile资源。旧Stage01空序列历史断言替换为20基本敌、合法定义、原型标记和实际绑定的更强断言；保留全部地图/PNG/半开边界验证。
- BC-200旧中心钢墙PlayMode用例明确隔离成无波次ArenaSession，保留真实键盘采样与钢墙断言；新专项加载默认Stage01且真实Update自然出生，绝非所有测试禁掉波次。

## 实际编译、测试与执行集合

所有 `unity command` 均带 `--caller plugin --skill unity-cli --project-path H:/Projects/Unities/BattleCity --format json`。CLI beta.14；原Unity6000.6.7f1 PID26924 / port7800。先读unity-cli skill/运行reference并从实际Editor发现schema。Pipeline0.9.1升级提示仅记录，未升级/换协议/重启。

| 实际执行 | 原始结果 | 结果 |
| --- | --- | --- |
| `script recompile` → `script recompile_status`（最终实现） | `Integration-Final-Compile-Request.json` / `Integration-Final-Compile-Result.json` | completed、failed=false、errors=[]、compilationFailed=false |
| `test run --mode editor --filter EnemyWaveIntegrationTests --async_tests true` → `test status` | `Integration-Logic-Request.json` / `Integration-Logic-Result.json` | 新独立集成13/13 Passed |
| 首版 `test run --mode playmode --filter EnemyWaveIntegrationPlayModeTests --async_tests true` | `Integration-Play-Request.json` / `Integration-Play-Result.json` | 初始4/4 Passed；之后增加宿主组件销毁专项并完成最终全量 |
| 最终 `test list --mode all` | `Integration-Final-Tests-Discovered.json` | 354发现 |
| 最终 `test run --mode editor --async_tests true` → `test status` | `Integration-Release-EditMode-Request.json` / `Integration-Release-EditMode-Result.json` | **299/299 Passed** |
| 最终 `test run --mode playmode --async_tests true` → `test status` | `Integration-Release-PlayMode-Request.json` / `Integration-Release-PlayMode-Result.json` | **55/55 Passed** |
| 独立比较发现/执行集合 | `Integration-Test-Set-Comparison.json` | 354发现=354唯一执行；漏执行/额外/重复/非Passed均空 |

最终两组失败/跳过/不确定均0；逐条FullName、Status和Duration在原始JSON中。本项新增13 EditMode+5 PlayMode集成案例；其余包含全部已有项目回归（也包括wave30/view12），不是借组件专项冒充全量，也没有把历史256/38算成本轮结果。较早全量299/54保留在Integration-All-*，不代替最终299/55。

覆盖：默认可选启用/空序列、真实共享Tank+Brain原子提交与下一tick行动、玩家全阻/释放、预备结束再被阻/原请求保持、地图及基地足迹、同tick死亡先释放容量/重复死体不双扣、cap1/4及预留不可偷占、暂停准备/间隔/AI与恢复、最终序列耗尽/仍有活体、固定种子集成重放、作者数组不变、配置尽早失败。PlayMode覆盖默认Stage01真实Update到4敌、实际移动射击/显示对照、真实弹命中死亡清view/补位、禁用重绑/销毁与租约清理、CombatArena无波次。

## 失败、修正与限制（不抹去）

- 没有本项项目源码编译失败或测试断言失败；实际编译与每次已完成测试均通过。后加组件销毁清理后重新完成最终编译、299/55全量。
- 一次发现命令错误拼写为 `editor playmode`，实际group为 `editor.playmode`。返回未知命令，Console seq361产生1条工具错误；原始失败保留 `Integration-Discovery-Unsupported-Group.json`，未算游戏异常或谎称整个Console增量全零。
- 一次临时eval用 `System.Action` 订阅要求 `EditorApplication.CallbackFunction` 的事件，Roslyn拒绝编译（未产生项目源码或注册observer）。保留 `Integration-Run-Resume-Request.json` 与结果pending；修正为实际委托类型后留存 Fixed-Request/Fixed-Observed，observer触发后自行解绑，SessionState临时键已清理。不是通过放宽生产断言解决。
- 初次截图请求768×720会把原生1523×752的screen target缩放变形；保留原结果/图供追溯，不用它宣称像素比例正确。只读查原生尺寸后重新以1523×752截取作为正式观察图。
- 30分钟子任务超时与同协议批准恢复如前述；不能称全流程从未失败。没有基础设施静默重试/重启/关对话框/CLI或Pipeline升级。

## 真实Stage01运行、暂停、截图与Console

- 通过 `editor.playmode set_run_in_background --enable true` 临时允许后台推进，原autoTick=true/16ms保持不变；`editor.playmode start`后按协议 `status --until-ready` 确认原实例。Stage01在准备探针前已自然运行，不冒称Run-Start从tick0开始。
- 独立实际Update探针（未手动Step/AdvanceFrame）：frame **29349→34726**，tick **3764→4447**；4已生成/4活体/16待生/0预留，4个真实EnemyTankView。共享敌人位置/朝向/履带、SpriteRenderer与弹/冷却均在变化；玩家此时已自然死亡，**没有临时复活或隐藏该限制**。
- 暂停：frame **36309→38060**，tick **4522→4522**，除渲染frame外全部探针状态（计数、随机、姿态、冷却、子弹、显示等）相同，见 `Integration-Run-Paused1/2.json`、`Integration-Run-Comparison.json`。
- 恢复observer只观察真实Update，首次推进后立即暂停：frame136102→136103、tick4522→4526（4tick），DroppedTicks **3→3**，没有把长暂停补算。自然运行中重型探针/调度导致此前DroppedTicks到3，如实保留，不声称性能零丢帧；自动PlayMode真实Update专项运行到tick601时DroppedTicks=0。
- 正式截图：[`Screenshots/Stage01-Native-Screen.png`](Screenshots/Stage01-Native-Screen.png)，实际CLI返回路径在Assets/Docs下；已复制PNG到本文目录并经 `AssetDatabase.DeleteAsset("Assets/Docs")` 清理仅本项产生的截图资产/目录，见Capture-Native-Result/Cleanup。较早变形捕获为Screenshots/Stage01-Natural-Enemies.png，仅追溯用。截图目视见敌方白色坦克/弹/破砖；不是人工手感或自动逐像素图像验收。
- Console完整本项增量 **cursor302→404**：102条=101log+1error（上文seq361发现拼写工具错误），0warn，无reset/dropped。`Integration-Console-Final-Since302.json`保留原始；修正命令后 **361→404** 为43log、0error/0warn，见PostDiscovery-Since361。最后groundTruth compilationFailed=false、consoleErrors/Warnings=0；不否认历史buffer错误存在。
- 已退出Play并恢复background=false；Editor non-Play/not-compiling/compilationFailed=false、Stage01 clean，见Integration-Editor-Final-State及恢复阶段复核。临时observer/SessionState键和Assets截图资产均无残留。作者操作产生的recent scene偏好在原运行中已按记录恢复原值/原SHA256（Integration-UserSettings-Restore），恢复阶段不再修改用户recent偏好。
- 原运行的background命令额外生成 `ProjectSettings/Packages/com.unity.pipeline/EditorPipelineConfig.json`（起点不存在、override已false），恢复收尾仅通过Editor API移除本项临时配置/本项空目录，保留用户既有autoTick选择；最终结果见Integration-Recovery-Verification.json。

## 资源保留与最终门禁

`Integration-File-Integrity.json`按起点逐文件核对：仅7个授权既存文件发生内容变化（Stage01.asset、Stage01Authoring.cs、Stage01.unity、ArenaSession.cs、ArenaController.cs、Stage01Tests.cs、Stage01PlayModeTests.cs），无丢失。`Integration-Preserved-Resource-Hashes.json`记录地图PNG、派生图及meta、CombatArena、PlayerPrototype/SpriteCatalog、layout、wave/view源码、.vsconfig等保留资源。恢复核实单列父代理更新的总计划/看板，不误归因writer。

最终新增/修改列表、当前哈希、空暂存区及恢复前后状态在 `Integration-Recovery-Verification.json` / `Integration-Final-File-Integrity.json`。源码/资产未在最终测试后修改；无行为的文档补全与临时Editor配置清理不冒称新一轮测试。

## 恢复收尾实际结果

- 原实例仍为PID26924/port7800、ready。恢复没有再次进入Play、重建场景、修改生产源码/资产或重跑测试。
- 与最终测试后留存hash以及父代理恢复快照比较，所有本项实现资产/源码/测试均无差异；故引用已有299/55及354集合结果，不冒称恢复阶段新跑了一遍。
- `Integration-Recovery-Config-Cleanup.json`确认仅删除本轮新建的EditorPipelineConfig.json及其空com.unity.pipeline目录；既有兄弟com.unity.learn.iet-framework目录保留。之后configExists=false，无自动重建。
- `Integration-Recovery-Final-State.json`：playing=false、compiling=false、compilationFailed=false、background=false、autoTick=true/16ms、Stage01 dirty=false、observerKey=absent、temporaryAssets=false。恢复不写用户recent偏好；UserSettings及.vsconfig均匹配原起点SHA256。
- `Integration-Recovery-Console-Since404.json`：同Console session cursor404、0新增条目、无reset/dropped。Git diff --check通过，暂存区空。起点既存文件无丢失；内容变化仅7个授权文件及父代理外部更新的总计划/看板。详细复核见Integration-Recovery-Verification / Integration-Final-File-Integrity。

## 未覆盖 / 下一步

- 未实现基地生命/失败、玩家生命/复活保护、胜负状态、完整重开、HUD/计分；BC-204以后待后续owner。玩家死后仍不复活，场上敌人不会仅因玩家死亡结束会话，这是当前阶段限制。
- 原型AI可能在复杂地形局部绕行/受阻，不保证最短路或最终可达性；所有敌方速度/方向权重/计时及20基本敌序列均未校准原版。
- 未做本版本用户人工体验确认、独立构建、硬件fps性能基准、长期压力或逐像素图像对照；自然运行与自动测试不替代这些。
- 当前完成只交付ready证据，不自行勾总计划/看板、不宣布M2通过。下一门禁：fresh只读审查，再由父代理决定BC-203验收。
