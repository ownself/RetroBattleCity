# BC-204 基地生命周期验证与交接

状态：**实现与实际自动验证 ready，待 fresh 只读审查及父代理验收**。仅 BC-204，不代表 BC-205/206、M2 或用户人工体验通过。

## 起点、归因与实现边界

- main / `7de290635ca8b097022b379e911fbef1234baefc`；开始 UTC 2026-10-24T05:48:30Z。已验收但未提交的 BC-201～203 和既存 `.vsconfig` 全部保留；`Start.json` 保存起点 Git 状态及既存 modified/untracked 文件 SHA256，不把相对 HEAD 的总 diff 归为本项。`Protected-Start.json` 另记实际绑定前的素材/资产/共享算法 hash。
- 仅新增小型 `Core/BaseState.cs`：16×16、位置从 Level.baseCenterPixels 独占快照；预留 `long.MaxValue` 目标 ID，坦克分配明确禁止使用此 ID。没有独立显示组件、Collider/Rigidbody、第二套事件框架。
- `ArenaSession(..., wave = null, enableBase = false)` 保持旧两/三参数用法默认无基地；CombatArena 无绑定、无隐形基地。Stage01 通过显式 Controller.baseRenderer 绑定启用真实基地。
- 完好/废墟都进入既有统一 blockers，用同一 Map.CanOccupy 半开规则阻挡玩家/敌人移动、显式注册和自然出生。**废墟保留阻挡是项目原型，不宣称原版规则**。
- 活基地加入既有 BulletTarget 旧快照；沿用原扫掠、距离和 Base 优先级，玩家/敌方弹均可毁坏，不套用坦克阵营/自身过滤。全部弹先扫掠，再结算；同 tick 多弹均消费旧目标，但 BaseState.ApplyHit 只一次返回毁坏、只一次死亡爆炸。
- BC-206 接缝：`BaseFailureRequested` 是永久锁存的只读请求，`BaseDestroyed` 每实例仅发布一次，且在所有本 tick 坦克伤害与出生结算后发布。晚订阅者读取锁存请求；没有完整胜负状态机、失败后冻结或跳过剩余伤害。最后敌人与基地同 tick 死亡仍全部结算，后续统一判败。
- Controller 只读投影 `base.alive` / `base.dead`；没有往基地 GO 添加脚本。Stage01Authoring recipe 和当前场景显式绑定一致；实际场景仅用 Editor API 保存引用/名称。未重写波次、AI、坦克或子弹算法，未修改关卡数据、PNG、布局、相机、材质或 20 基本敌配置。

## 实际命令、编译与最终回归

CLI beta.14 自检最新；Unity 6000.6.7f1，原 PID26924 / port7800。先读 unity-cli skill、运行验证 reference、项目规则/坐标规范/阶段2及 BC203 验收，并重新发现实际 Editor schema。所有 `unity command` 带 `--caller plugin --skill unity-cli --project-path H:/Projects/Unities/BattleCity --format json`。

| 执行 | 原始证据 | 结果 |
| --- | --- | --- |
| `script recompile` → `script recompile_status` | Compile-Request/Result.json | completed，failed=false，errors=[] |
| 新逻辑专项 `test run --mode editor --filter BaseStateTests --async_tests true` → `test status` | Targeted-Edit-Request/Result.json | 21/21 Passed |
| 加入 PlayMode 测试后实际编译 | Compile2-Request.json、Compile2-Recovered-Result.json、Final-Compile.json | completed，failed=false，errors=[]，compilationFailed=false |
| 新实际运行专项 `test run --mode playmode --filter BaseStatePlayModeTests --async_tests true` → `test status` | Targeted-Play-Request/Result.json | 3/3 Passed |
| 最终 `test list --mode all` | Final-Discovered.json | 378 项发现 |
| 最终 `test run --mode editor --async_tests true` → `test status` | Final-Edit-Request/Result.json | **320/320 Passed** |
| 最终 `test run --mode playmode --async_tests true` → `test status` | Final-Play-Request/Result.json | **58/58 Passed** |
| 按 FullName 独立比较发现/执行集合 | Test-Set-Comparison.json | **378发现=378唯一执行**；漏项/额外/重复/非Passed均空 |

最终失败/跳过/不确定均0。新21逻辑+3显示/真实Update用例覆盖：双阵营四向、同 owner ID 不过滤、高速防穿透、砖/钢先命中、坦克较近及同距基地优先、同 tick 双弹消费/仅一次爆炸通知/其他坦克仍死亡、毁坏后穿弹但持续移动/显式出生/自然出生阻挡、暂停入射弹和恢复、默认无基地、实例/作者位置快照隔离、Stage01 重载、CombatArena 不启用。全部使用真实共享 WeaponState 和 BulletWorld，不构造假 hit。

旧 Stage01 地图/PNG/676格/相机/战斗/波次断言保留，旧 display-only 断言强化为显式绑定共享基地和无独立伤害组件；波次基地占用专项明确 enableBase:true。未引用 BC203 的299/55或354作为本版本结果。

## 失败与恢复（不隐去）

- 无项目源码编译错误或测试断言失败。
- 第二次 recompile 请求成功，但首个 status 恰逢 domain reload 返回 `EDITOR_NOT_RUNNING`；`Compile2-Result.json` 保留失败。按文档 `unity status --until-ready` 恢复原 PID26924/7800 ready（Reload-Connection.json），随即保存 Checkpoint 并暂停询问父代理。父代理明确批准同协议继续，再取真实 completed 编译结果。没有循环重试/重启/换模式/升级/强关对话框。
- 本地 Python 首次解析 Capture-schema.json 使用系统 GBK 导致 UnicodeDecodeError；改用明确 UTF-8 后成功。它不是 Unity 项目/Console 错误，未隐藏原工具记录。
- 临时 RuntimeProbe.cs 的 ephemeral 编译有一条 `FindFirstObjectByType` obsolete warning（Runtime-Probe-Request.json diagnostics），不是生产源码编译 warning；观察器仍成功运行并清理。源码未为此改动。

## 真实 Update、暂停与一份画面

- 新专项真实默认 Stage01 + 虚拟键盘 D/Space，真实 Controller.Update 发弹先破保护砖、再毁基地，未调用手动 Step/AdvanceFrame。最终全量 Console 日志：frame **174→722**，tick **0→毁坏31→最终66**，通知1、毁坏时基地爆炸1、sprite=base.dead、revision8、DroppedTicks0。暂停时渲染 frame 仍前进、tick 不变，恢复后继续35 tick且通知不重复。
- 独立 Editor Play 观察：只临时启用 runInBackground；原实际 autoTick=true/16ms **从未修改**（持久 SessionState 键本来未设置，不凭默认键误改用户选择）。没有更改 recent scene 偏好。
- `RuntimeProbe.cs` 是 Docs 下 ephemeral 观察 recipe，不是生产组件。它创建虚拟键盘、订阅一次通知、观察实际 Update，达到毁坏后35tick自动暂停并解绑/移除键盘。原始 `Runtime-Observed.json`：frame **2165→毁坏2346→2614**，tick **289→毁坏320→355**；notifications1、explosionsAtDeath1、failureRequested=true、sprite=base.dead、player=(88,8)、revision42、waveTotal20、spawned2。开始探针前已自然运行到tick289，不冒称从0开始。
- 此独立 CLI 运行 DroppedTicks=10（重型命令/临时编译期间运行），如实披露，不声称性能零丢帧；正式 PlayMode 专项上述DroppedTicks0。截图不替代持续推进证据。
- `Runtime-Paused1/2.json` 记录暂停后 frame 继续变化、tick保持355、dead/request保持，observer=false。
- 一份正式原生 screen 截图：[`Base-Destroyed.png`](Base-Destroyed.png)，1523×752。目视可见底部灰色废墟、真实玩家/敌军与破砖；不是逐像素图像或人工手感确认。CLI 实际保存到 Assets/Docs 后复制证据，临时截图资产通过 Editor API 清理。

## Console、清理与精确文件边界

Console 基线 cursor404/session316dee6ba1864c7dab134a9af37d8ce3，历史buffer已有2error/0warn（不是本项新增）；groundTruth consoleErrors=0。`Console-Delta.json` 保存 **404→474 共70条log，0新增error/warn**，无reset/dropped，包含专项/全量/真实运行日志。上文断连错误与 ephemeral warning分别在CLI/diagnostics层披露，不误称不存在工具问题；未清Console。

最终设置/资源清理与 hash 核对记录见 Cleanup.json、Final-State.json、Final-Integrity.json：playing=false、compiling=false、compilationFailed=false、Stage01 dirty=false、background=false、autoTick=true/16ms、持久autoTick键仍未设置、recent=""与起点一致、observer=false、临时Assets/Docs和Pipeline配置均不存在；既有兄弟设置保留。仅清理本项临时资源，没有重置用户配置。生产源码和场景自最终全量前未再改动。

`Final-Integrity.json` 精确对起点267文件核对：**261未变、6授权既存文件变化、0丢失**。6个是Stage01Authoring.cs、Stage01.unity、ArenaController.cs、ArenaSession.cs、EnemyWaveIntegrationTests.cs、Stage01Tests.cs；另新增BaseState和两个专项源码及Editor生成meta。所有起点BC201～203证据、共享AI/波次/坦克/子弹实现、旧PlayMode测试、总计划/看板/.vsconfig均保持起点hash。67个保护资源中仅授权Stage01场景变化；Stage01.asset、CombatArena、参考/派生PNG及meta、layout、SpriteCatalog、玩家/敌人/波次资产、材质等未变。最终实现源码/资产/测试含meta逐文件SHA256均留存，不以整个HEAD差异误归因。

`git diff --check`通过（仅Git既存LF/CRLF提示，无空白错误），`git diff --cached --name-only`为空，无暂存/提交/推送。`Console-After-Cleanup.json`确认cursor474后0新增、无reset/dropped；暂停两份观察为frame19561→35649、tick355→355。

## 未覆盖与下一门禁

未实现生命/复活、完整胜负/同 tick败优先最终裁决、重开、HUD；由 BC205/206以后负责。未做本版本用户人工体验、独立构建、长期压力、硬件性能基准或逐像素对比；原型阵营/废墟规则和敌人参数均不是原版校准事实。

交付停在 BC-204。下一步 fresh 只读审查，父代理独立核对证据/决定验收及勾选；writer 不修改主看板或总计划、不提交/推送。
