# 阶段二剩余任务执行看板

## 授权与起点

- 用户本轮请求：根据 `Docs/BattleCity-Implementation-Plan.md`，利用 subagent 逐一发起并完成阶段二剩余工作。
- 由此前暂停恢复到 BC-201～BC-209；不包含阶段三、提交/推送、发布、清理用户文件。
- 起点：`main` / `7de290635ca8b097022b379e911fbef1234baefc`。
- 起点工作区：仅既存未跟踪 `.vsconfig`，保留不动；之后主代理修改计划及本看板以记录恢复授权。
- Unity CLI `1.0.0-beta.14`，版本检查为最新；目标 Unity `6000.6.7f1`，连接 ready。
- 单一工作区 `H:/Projects/Unities/BattleCity`，单写入者；不建立基于旧 HEAD 的隔离副本，以免遗漏当前资产和编辑器绑定。
- 用户在首次模型调用失败后明确指定：本轮所有 subagent（实现、审查、验证）统一使用 `openai/gpt-6.1-sol`；不自动切换其他模型。

## 执行原则

1. 每项限定范围，完成实现与实际验证后交给只读审查；主代理核对源码、结果和范围，决定验收及勾选。
2. 依次 BC-201、202、203、204、205、206、207、208、209，不因子代理正常退出就视作任务通过。
3. 测试或编译失败须先修复并重验；工具基础设施失败保留现场并报告，不静默更换执行协议。按根目录AGENTS.md新工程实践，长时间无进展、反复失败或无法完成时及时停止受阻自动化，请用户人工介入；不等待硬超时，不以主代理反复批准恢复替代人工求助；允许人工补齐验证并独立记录证据。
4. 需要新产品规则/架构取舍时由主代理处理，超出现有规范的重大选择交用户确认。
5. 场景、Prefab、资产及 Sprite meta 通过 Unity 编辑器 API 修改，不手写序列化 YAML；源代码用文件工具维护。
6. 保留已确认的 60Hz、像素/碰撞、输入/暂停、破砖和扫掠顺序；不用第二套动态刚体碰撞真相。
7. 最终 M2 验收须包含实际持续推进、胜利、两种失败、重开、截图和 Console 增量检查；自动验证不能冒称用户人工体验确认。

## 当前 lane

**2026-10-24 本轮最终覆盖：BC-207已通过主代理验收，阶段二8/10，M2未通过。** 基线main/b2b14b9；single-seam只读HUD，一个worker实现/聚焦验证与一个fresh reviewer均完成。40唯一用例Passed（新13+相关27）、实际tick0→623、两种实际GameView像素比较一致，Console增量无warn/error；fresh Review OK with notes，无具体P0/P1/P2。主代理17文件hash/结果/保护资产核对完成。详见`BC-207/Acceptance.md`，约24分钟分阶段及优化见`TimeLog.md`。Editor保存证据时stopped/clean、临时设置清理；无活跃writer、无提交/推送，不自动开展208/209。下方BC206“最终覆盖”及其他覆盖均为历史。

**最终覆盖：BC206已验收，阶段二7/10，M2未通过。** 用户确认Editor无持续异常后主代理直接完成P2修补：10/10生命周期Edit（新增2例）、3/3生命周期Play、1/1复活Play通过，独立聚焦复审OK。有效证据并集472唯一用例，不是单次472全量；截图未留档如实记录，不再阻塞206。最终Editor stopped/clean/backgroundfalse、增量32Console全log；见BC-206/Acceptance.md、P2-Final-Audit.json。无活跃writer，不自动开展207。以下状态为历史。

**最新覆盖：fresh审查已完成OK with notes，无P0/P1，发现一条有效P2（通知回调内失焦暂停请求被丢弃）。** 主代理拟在本项最小修补/聚焦验证/复审；用户可能正在补两张Game图，按AGENTS.md当前不改源码或Unity现场、无活跃writer，等图或用户明确结束检查再动。见BC-206/FinalReview.md、Review-Disposition.md。

**前一技术核对：470个唯一用例已有有效通过证据，主代理对账完成。** 原407/407Edit、62/63Play，唯一失败旧测试等待真实开始转换帧后1/1复测通过；不冒称一次470全绿。主代理原始集合、68源码、4模型、985保护文件hash均核对，见BC-206/Parent-Audit.json。独立画面eval误用Session.Flow已停止，不恢复或继续自动截图。用户仅需提供Ready/Playing两图，无需读内部属性/执行代码或重复测试。见Closeout-Decision.md。6/10/M2未通过，BC207不开展。

**上一检查点：用户已反馈前述人工检查均无问题，Ready需Enter的疑问已解决，见BC-206/Manual-Check.md。** 主代理已核实Stage01 clean/stopped、后台设置及磁盘恢复；源码冻结，仅启动缺口验证（一次最终全量包含修补后的3项Play，不重复专项）→fresh只读审查，不恢复原实现child、不再次整项委派。若受阻立即人工交接；6/10及M2未通过不变，BC207不启动。

**上一自动化停止记录：BC206集成child在25分钟期限终止，workflow failed。** 模型40/40、集成Edit8/8通过；Play1/3，另2项fixture虚拟键盘引用问题已最小修补但未复测。停止受阻自动化，不自动resume/relaunch；请用户按`BC-206/Manual-Handoff.md`先在Test Runner运行这3项并正常Save Project落盘后台设置。完整回归/独立运行/最终资源Console/审查尚欠，未验收，仍6/10/M2未通过。原始结果、源码及Temp/BC206-Stop-ab68a23d快照保留。下列授权和拓扑仍有效，不代表自动恢复许可。

**授权：用户要求继续实现BC-206；此前暂停206的记录转为历史。** BC200～205已验收6/10，M2未通过。当前基线main/e2383bacf3b661f930f87667ed9d6d3062780f79，起点仅.vsconfig未跟踪。只开展206，不进入207。精确规则见`BC-206/Contract.md`。

拓扑为multi-seam，严格串行单writer，不为并行制造拆分：独立纯状态裁决可单测；现有Session/Controller的tick冻结、通知、初始快照与显示重绑定必须由唯一integration-only owner接线。

| Lane | cwd / 隔离 | 决策 / 文件所有权 | 权限 | 下一门禁 / 交接 | 独立性 |
| --- | --- | --- | --- | --- | --- |
| BC206-model | H:/Projects/Unities/BattleCity；原工作区串行 | 新SessionFlowState.cs（含enum）、SessionFlowStateTests.cs及Editor生成meta；只读其他Assets | 实现模型、实际编译及模型专项；Docs/Stage2/BC-206/Model-Handoff.md；不提交/委派/验收 | 专项通过并冻结API，再启动integration | 仅System依赖的5状态转换/裁决，与Unity装配独立 |
| BC206-integration | 同cwd；模型交接后唯一writer | ArenaSession/Controller、输入、作者recipe/Stage01、必要LogicClock/显示接缝、专项及必要旧测试兼容；不得重写模型 | 完整接入、一次最终回归和短运行，Verification/清理/证据；无勾选权 | 实际验证→fresh只读审查 | 现有tick/重开/显示host共同生命周期，拆并行会重叠Controller/Session所有权 |
| BC206-review | 同cwd只读；writer结束后 | 精确206增量/契约/原始测试与运行证据 | 不修改项目或Unity状态；返回有引用的问题与OK/BLOCK | 主代理直接核对/处置后Acceptance | 独立fresh审查，不以worker ready代替验收 |

任何受阻遵守AGENTS.md及时人工交接；不等硬超时，不反复恢复；所有child显式openai/gpt-6.1-sol。

### 上一检查点（BC205收口时历史）

BC-201～BC-205已通过主代理验收（含此前BC-200，阶段二6/10），M2未通过。用户补充四项人工检查初步均通过并要求继续，见`BC-204/Manual-Check.md`。恢复时用户已提交此前工作，新基线main/d010f12f1a814d3595f8c065eedd7732475cddc7，仅.vsconfig未跟踪；不回退旧HEAD。BC-205模型→integration-only→有限恢复→fresh只读审查均已结束，主代理技术核对完成，精确契约见`Docs/Stage2/BC-205/Contract.md`，正式结论见`Docs/Stage2/BC-205/Acceptance.md`，主代理核对见`Parent-Check.md`。用户已明确总共出场3次且复活护盾有效，计数疑问已解决并勾选BC-205。按用户要求不启动BC-206，不重复运行成功测试或恢复已完成worker。以下保留各项历史边界。

| Lane | repo/cwd | 决策与类型 | 所有权/边界 | 隔离 | 权限 | 下一门禁 | 交接 | 划分依据 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| BC-201 shared tank | BattleCity / H:/Projects/Unities/BattleCity | single-seam：将现有玩家的移动/武器/命中组合收束为共享坦克契约，沿用阵营规则 | Tanks共享状态及其Combat规则、必要Map/Motor动态占用查询和ArenaSession接入；对应测试与Docs/Stage2/BC-201证据 | 当前工作区串行；仅该worker可写实现 | 实现、针对性测试/回归、保存证据；不得提交/推送、递归委派或提前实现后续任务；不勾选总计划 | 实际编译与测试→fresh只读审查→主代理验收 | worker输出+Docs/Stage2/BC-201/Verification.md | 现有ArenaSession直接组合Player Motor/Weapon并建立单一目标快照；模型、互阻和会话接入必须对同一坦克身份/边界保持一致，拆为并行写入会重叠契约且产生人造交接 |

BC-201 可包含坦克通用血量/幂等命中/死亡能力以及可测试的坦克互阻；不包含真实敌人AI、波次、基地失败、玩家生命复活或会话胜负UI。现有玩家 API 尽量兼容以控制回归。

### BC-202 当前所有者与契约

| Lane | repo/cwd | 决策与类型 | 所有权/边界 | 隔离 | 权限 | 下一门禁 | 交接 | 划分依据 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| BC-202 enemy brain | BattleCity / H:/Projects/Unities/BattleCity | single-seam：固定tick下的敌方决策状态与共享TankCommand控制接缝 | Scripts/AI基础敌人状态机/原型参数/显式种子，以及ArenaSession最小注册与tick前决策接入；相应测试和Docs/Stage2/BC-202证据 | 当前工作区串行唯一writer，基于已验收未提交BC-201实现继续 | 实现/测试/证据；只读既有验收记录，不提交/推送、不递归委派、不做BC-203以后 | 编译与确定性/边界/暂停专项→fresh只读审查→主代理验收 | 托管writer输出与Verification.md | 决策需在同一tick开始快照上产出命令，并由ArenaSession在移动前统一执行，拆分会重叠状态/时间契约；不包含独立波次或显示系统 |

- 基础AI使用轻量状态机、方向权重和遇阻转向，持续/定时射击；仅观察当前地图和坦克状态，不读取玩家未来输入、不引入NavMesh/全知最短路。
- 固定初始种子与相同逻辑输入必须可重现；PRNG与AI计时在暂停时冻结；在移动前按稳定ID统一做本tick决策，避免后ID偷看早ID本tick移动结果。
- 不声称原版AI参数已核实。配置/原型默认及其未校准性质写入证据。
- 可通过显式注册真实敌方实例测试生成后的移动/遇阻/射击；自然波次、可见敌军大规模接入属于后续BC-203，不为本项提前改写场景。

### BC-203 当前组件所有者与集成契约

分类：**multi-seam**。波次/出生计数模型和只读敌方显示是独立可测边界；场景/数据绑定另由集成owner负责，禁止一个writer包办所有系统。全部在当前工作区串行运行，一次仅一个writer；不使用基于旧HEAD且遗漏已验收未提交代码的worktree。各组件交接后才启动集成。

| Lane | repo/cwd | 决策/独占边界 | 隔离/权限 | 门禁/交接 | 独立依据 |
| --- | --- | --- | --- | --- | --- |
| BC203-wave | BattleCity / H:/Projects/Unities/BattleCity | 新增AI目录波次状态/配置模型与EditMode专项；不编辑ArenaSession、Controller、TankState、场景/实际资产或其他组件文件 | 当前工作区唯一writer；源码/测试/本项Wave-Handoff证据，不提交/推送 | 实际编译+波次/占用/计数/暂停调用契约专项通过，交付公开API及状态含义 | 生成模型可在无Scene/Renderer下独立验证 |
| BC203-view | 同上 | 新增Tanks目录EnemyTankView及PlayMode专项；不修改已有共享模型/会话/控制器、波次组件、场景/实际资产 | 前一owner交接后串行唯一writer | 显式绑定TankState、TankDefinition、SpriteCatalog/Material，读Motor朝向/履带/像素舍入/死亡状态；编译+显示专项，交付View-Handoff | 只读投影可用显式共享实例独立验证，不决定出生或伤害 |
| BC203-integration | 同上 | 仅桥接已交接波次与视图至ArenaSession/Controller，Editor作者脚本、Stage01实际配置/绑定、集成与受影响回归测试 | 两组件成功后唯一writer；只准必要小修，组件行为缺陷先向父代理报告，不重写组件 | 全量测试/真实Stage01自然出生与可见敌军/暂停/截图/Console，Verification.md→fresh审查→主代理验收 | 是适配/绑定owner，不重新实现独立模型/视图 |

产品/数据契约：

- 沿用Core-Rules原型20敌、场上上限4、出生延迟60tick、波次间隔120tick；仅基本敌人，不宣称原版第一关真实敌型序列。LevelDefinition.enemySequence/maxActiveEnemies为唯一数量来源，新增配置仅承载定义映射/计时/显式种子，不复制另一套总数。
- 死亡先结算，再生成；出生点占用含玩家/存活敌人/待生成预留，阻塞则等待/重试，未成功注册活体不减少待生成数量，不丢计数。活体+预留不得越上限；预留结束仍须复核占用，不能挤开玩家/墙。
- 生成模型允许宿主提供占用查询/成功生成回调，不直接拥有ArenaSession/Renderer；公开待生成、预留、存活/已生成等状态及其计数定义。配置/关卡数组快照，不依赖作者资产被运行时改动。
- 整体固定逻辑tick、固定种子及稳定出生点选择；暂停冻结预留/间隔；接入位置在本tick伤害死亡之后，新敌不在同tick倒流移动/射击。
- BC203-wave实施中向父代理澄清后冻结计时端点（未校准项目原型，不是原版事实）：首次战斗Step建立年龄0的预留，此后完成60个战斗tick再尝试物化；成功物化后才等待120tick，满120tick建立下一次年龄0预留，再准备60tick。无阻时成功物化间距180tick。满准备但占用则逐战斗tick复查，不重置准备、不吞序列/种子/计数；成功才开始间隔。单一在途预留0/1；Pending=Total-Spawned且包含预留；宿主传本tick伤害后的实际活敌数，不以可重复死亡事件维护另一套数量。须用绝对Step调用序号测试端点。
- EnemyTankView只显示已绑定共享实例；初始化时读取TankDefinition对应8个朝向/帧，活体位置按像素显示舍入，不反写逻辑；死亡/禁用/销毁无残留；无Collider/Rigidbody或独立动画/伤害时间源。
- 集成时新建基本敌参数资产及Stage01波次配置，通过Editor API绑定，更新可重建作者recipe；CombatArena继续无自然波次。保留BC-200的676格/参考PNG/出生坐标/相机/破砖验收，不把旧“波次尚空”的历史测试断言当不可更改规则，但替换为更强的新20基本敌/原型标记/绑定断言，不能删除地图验证。
- Stage01旧纯地形测试可显式选择无波次会话以隔离原始契约；新的集成测试必须覆盖默认场景真实自动出生与移动/射击/显示，不能全部禁用自然波次让测试通过。
- 不提前实现基地失败、玩家复活、胜负、重开或HUD。死亡玩家暂不复活是后续任务范围，不掩盖该限制。

### BC-203 集成超时恢复记录

- workflow `1e63a288-44c2-48ec-83b4-b7a8bbed9945`：wave/view成功；integration child `0ace6079-d85f-4a95-ab54-7cf2bf97b5b4`触发1800000ms超时终止，状态failed、process terminal observed；后续审查未启动。
- 主代理已读取失败输出、最后工具记录、集成ArenaSession/Controller及原始验证/运行/设置恢复结果，确认不是等待中的活跃writer。
- main/HEAD未变，保留部分代码/资产/测试/证据；`Temp/BC203-Recovery/Tracked.patch`保存tracked diff，`Snapshot.json`及`Untracked/`保存258个工作文件hash和未跟踪文件快照；`git diff --check`通过。
- 最终交接报告缺失，ProjectSettings下本轮临时Pipeline配置仍在，须由恢复owner核实基线后通过Editor API清理，不冒称完全清理。
- 留存报告有299/299 EditMode、55/55 PlayMode、354集合、实际画面/推进/暂停/恢复，但尚未完成主代理验收或独立审查；仅作为恢复材料，不将worker超时视为功能失败或通过。
- 恢复范围：同一retained子会话/同一openai/gpt-6.1-sol，优先补Verification.md、确认临时设置与资源恢复/源码hash及既有结果对应，然后交付；源码未变不机械重跑已通过组件/全量。不换执行模式、不提交/推送。
- 恢复现已完成：workflow bd6fd6a9-953c-4732-b9fa-4aec489e7086补齐报告/清理临时配置并完成fresh审查OK with notes；主代理独立重算15个集成文件SHA256、354发现/执行集合、Console原始计数和截图后验收BC-203。Console工具拼写error1、DroppedTicks3和未覆盖项保留，详见Acceptance.md。

### BC-204 当前owner与基地契约

分类：**single-seam**，一个基地生命周期纵切片。只新增小型BaseState，接入既有BulletTarget/扫掠/同tick伤害及Controller既有基地renderer投影；不建设独立显示子系统，拆分会让同一基地身份/毁坏标志/失败通知跨owner重叠，产生人造交接。

| Lane | repo/cwd | 独占边界 | 隔离/权限 | 门禁/交接 |
| --- | --- | --- | --- | --- |
| BC204-base | BattleCity / H:/Projects/Unities/BattleCity | 基地状态、ArenaSession必要目标/占用/受击接入、Controller基地renderer读取、Stage01Authoring与实际绑定、专项及受影响回归 | 当前工作区唯一writer；保留BC-201～203未提交已验收实现与证据；不提交/推送、不递归委派、不做BC-205以后 | 实际编译、双阵营/同tick多弹/幂等失败/阻挡/显示/暂停专项及最终回归→fresh审查→主代理验收 |

- Stage01启用真实基地；CombatArena和旧`new ArenaSession(level,player)`纯逻辑用法默认不启用，以免凭Level默认位置给无基地试验场添加隐形障碍。构造/Controller绑定以明确可校验方式选择启用，保持旧API兼容。
- 基地16×16，位于Level.baseCenterPixels；逻辑位置唯一，稳定目标ID不得与坦克/子弹混淆。活基地和毁坏后的废墟均保留16×16坦克/出生阻挡，避免当前位置凭空变成可生成或可穿越地形；本阶段该规则为项目原型，不是原版校准。
- 活基地接受玩家和敌方普通弹，不应用友军/自身坦克过滤；沿用既有Base优先级，全部扫掠旧快照后结算，一次受击即毁坏。同tick多弹均按旧基地目标消费，但毁坏、爆炸及失败请求/通知各只结算一次；后续不再作为可受伤目标。
- 提供给BC-206消费的幂等失败请求/事件即可，本项不做完整会话开始/胜负状态机，不冻结整局或阻止该tick剩余坦克伤害结算，以便后续固定同tick败优先规则。无需提前做玩家生命/复活。
- 显示用已有`base.alive`/`base.dead`，既有SpriteRenderer只读投影，不新增Collider/Rigidbody或另一个伤害脚本。Stage01作者recipe和当前场景绑定一致，不手写YAML；保留地图PNG/676格/相机/波次原型及全部历史证据。
- 旧“基地仅占位显示”的历史测试可替换为更强逻辑绑定/受击/渲染断言，不能删除地图/原战斗/波次验证；新实际Update测试必须验证真实弹命中与毁坏显示/失败幂等，而非仅注入假hit。

## 启动失败记录

- workflow：`7892270b-5c43-4079-8479-12d96514ca8f`，状态 failed。
- 实现child：`bc1056b9-7512-4c71-9938-90b2583fa0b7`，状态 failed；独立审查未启动。
- 原因：OpenAI API 400 / `model_not_found`：`The 'gpt-5.4' model is not supported when using Codex with a ChatGPT account.` 注册表列出该模型不等于当前账号可调用。
- 子代理在产出前失败，未产生源码/资产变更或验证报告。主代理再次检查：HEAD仍为原起点，仅总计划和本看板为本轮文档改动，既存`.vsconfig`未动，`git diff -- Assets/BattleCity`为空。
- 首次失败未标完成，BC-202未启动。用户随后明确确认所有模型使用`gpt-6.1-sol`；主代理重新核对无活跃子任务、HEAD未变、源码/资产未改，按相同subagent协议重启BC-201，统一显式指定`openai/gpt-6.1-sol`。

## 任务状态

| 任务 | 状态 | 下一个动作 |
| --- | --- | --- |
| BC-200 | 此前已验收 | 保留地图与破砖回归 |
| BC-201 | 已验收 | 227/227 EditMode、37/37 PlayMode，fresh审查OK，主代理源码/结果复核完成；见Acceptance.md |
| BC-202 | 已验收 | 256/256 EditMode、38/38 PlayMode，fresh审查OK，主代理复核通过；见Acceptance.md |
| BC-203 | 已验收 | 299/299 EditMode、55/55 PlayMode；fresh审查OK with notes，主代理354集合/15文件当前SHA256/源码/截图复核；详见Acceptance.md |
| BC-204 | 已验收，用户四项初步人工检查通过 | 320/320 EditMode、58/58 PlayMode；fresh审查OK with notes；主代理378集合/12实现+67保护文件当前SHA256/源码/截图复核，见Acceptance.md；无活跃worker |
| BC-205 | 已验收，用户人工确认通过 | 359 Edit+60 Play=419唯一Passed；独立Update/三截图/清理齐全，fresh审查OK with notes；主代理64源码+259保护文件+5recipe/截图hash与原始集合/运行端点核对通过；用户确认总3次出场及复活护盾有效，见Acceptance.md/Manual-Check.md；无活跃worker |
| BC-206 | 已验收 | P2已修，10Edit+4Play聚焦通过，fresh复审OK；有效证据472唯一用例、原失败保留；截图不再阻塞，详见BC-206/Acceptance.md |
| BC-207 | 已验收 | 像素HUD/提示、真实计数与重开绑定；40唯一用例通过、实际两档窗口缩放证据、fresh Review OK with notes；见BC-207/Acceptance.md及TimeLog.md，无活跃writer |
| BC-208 | 待办 | 完整闭环与边界自动回归 |
| BC-209 / M2 | 待办 | 运行、截图、Console及流程验收；列明人工体验未覆盖项 |
