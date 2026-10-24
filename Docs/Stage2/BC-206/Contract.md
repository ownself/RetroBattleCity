# BC-206 冻结契约：会话状态、胜负与完整重开

## 范围与基线

用户现明确要求继续BC206，覆盖此前“先停在BC205”的暂停指令；仅本项，不启动BC207或阶段三。BC200～205已验收，阶段二6/10、M2未通过。当前main/e2383bacf3b661f930f87667ed9d6d3062780f79，工作区起点仅既存未跟踪.vsconfig；不回退、不暂存/提交/推送、不删除既有证据或用户文件。

真实规则来源为`Docs/Stage0/Core-Rules.md`与总计划BC206行，不将原型行为冒称原版事实。

## 状态与控制

- `SessionPhase`：Ready、Playing、Paused、Won、Lost；`SessionFailureReason`为[Flags] None=0、BaseDestroyed=1、PlayerLivesExhausted=2，双失败可组合。
- Stage01显式启用整局生命周期，开始Ready（战斗tick0冻结）；Enter开始，Esc只在Playing/Paused切换。R从任何状态重建整局并回到Ready，之后Enter开始。重开优先于同帧Start/Pause/战斗输入，任何控制转换帧不计入战斗elapsed。
- 新`enableSessionLifecycle`字段及ArenaSession最后可选bool参数默认false：旧构造、CombatArena、未开启整局的测试夹具继续原行为。开启时要求配置非null敌波次，防无波次夹具误判胜利；基地与生命沿原显式启用规则。
- 对外提供明确的开始和重开方法（如`Session.StartBattle()`、`Controller.RestartSession()`），可被测试及未来HUD调用，不能仅支持键盘隐藏逻辑。状态只读；新状态模型是唯一整局状态真相。
- Ready/Paused/终局不推进逻辑、地图、AI、波次、复活、护盾、武器、弹与逻辑特效。Esc、`SetPaused(false)`、focus返回均不能从Ready或终局偷偷恢复战斗。失焦Playing→Paused，返回需手动恢复；未聚焦时不接受开始/恢复/战斗控制。
- 输入边沿在渲染采样处处理，Enter/R长按只触发一次，重建会话不重置管理键物理边沿导致连重开。转换清空方向/射击缓存，仍按住的战斗键需释放再按；多tick帧终局后立即停止余下tick。

## 胜负时点

- 仅Playing的完整tick末尾，在全弹旧快照扫掠→全部地图/伤害/死亡→玩家生命与出生→敌波次出生之后裁决，不在首个死亡回调中提前结束。
- 失败优先：`BaseFailureRequested || PlayerLifeFailureRequested` → Lost；原因准确保留两个失败位。同tick最后敌人+基地毁，或最后敌人+最后一命死，一律Lost；两种失败同tick不能互相覆盖。
- 无失败时，仅`EnemyWave.SequenceExhausted && AliveEnemyCount == 0 && PendingCount == 0 && ReservedCount == 0` → Won。暂时场上无敌人、等待新波次、正在预留/准备、出生占用均不是胜利。
- 终局只锁存一次并对外发布一次结果事件；同tick原始BaseDestroyed/PlayerLivesExhausted通知仍保留且不重复。先完成状态锁存与Clock冻结，再发布终局给订阅者，显示读取结算后的状态。
- 终局保留最后战场和结果，不额外清空最终命中/爆炸/废墟；战斗逻辑冻结，不做结果面板动画或HUD。须验证重入/通知回调不能造成重复终局、重复出生或旧会话继续被Controller推进。

## 完整重开

- 由Controller唯一装配点建立全新ArenaSession，不对现有readonly地图/坦克/Clock做半重置。
- 每个Controller从Awake时验证后的关卡/玩家/敌定义/波次配置保留私有初始快照，后续重开使用相同地图、出生点、种子和原型参数，不读取已破坏MapState或变化的运行状态。快照不修改或保存项目asset，生命周期结束释放临时Unity对象。
- 旧会话先冻结、清空弹/lease及逻辑特效，解除Controller拥有的旧事件连接；移除/清空旧敌view、出生提示、盾/弹/特效显示，重绑定新会话并立即投影。不能每次重开叠加新显示host、订阅或临时配置对象；Destroy延迟帧内也不能残留可见的旧画面。
- 新会话恢复原图砖掩码/基地存活、玩家出生/ID1/HP/方向/履带/武器计数与冷却、总3命/无初生盾、敌总20/待生20/无活敌无预留、初始种子及下一实体ID、tick0/DroppedTicks0/空输入/空命中与爆炸。
- 每次重开完全独立，连续至少3轮验证；相同初始种子和输入的首批自然敌出生位置/AI种子/序列可复现。不重载整个Editor、场景或Packages替代游戏重开。

## 组件API与所有权（先模型，后integration-only）

模型文件仅依赖System，无Unity或ArenaSession依赖：

- `new SessionFlowState()` → Ready、FailureReason.None；只读`Phase`、`FailureReason`、`IsTerminal`。
- `bool TryStart()`：仅Ready→Playing返回true，其他false。
- `bool SetPaused(bool paused)`：仅Playing↔Paused的真实转换返回true；无变化/Ready/终局返回false。
- `bool Settle(bool baseFailure, bool playerLifeFailure, bool sequenceExhausted, int aliveEnemyCount, int pendingEnemyCount, int reservedEnemyCount)`：计数非负且reserved<=pending；仅Playing按上述优先级锁存Won/Lost并返回true，其他false。非法计数抛ArgumentOutOfRangeException/ArgumentException。不发布事件、不包含Clock/按键/重开；integration owner负责恰好一次对外通知。
- 模型不提供原地Reset；新局新模型。集成不得复制或重写其裁决策略；确需API变动先问主代理。

## 验证与停止条件

1. 模型专项：状态边界、失败优先/双失败、无敌但pending/reserved、非法计数、终局幂等；保存轻量Model-Handoff。
2. 集成专项：真实共享弹同tick双目标/多重失败、完整tick结算后结果、8tick渲染帧内终局截断、各状态控制与失焦、连续重开/lease/显示/事件/种子、旧夹具兼容。
3. 源码变化后实际编译、最终完整Edit/Play集合、发现=唯一执行与状态核对；Play HTTP使用已发现的async协议，不再尝试已知不支持的同步模式。正常领域reload readiness只做一次有界等待。
4. 独立短生产Update观察Ready→开始→推进→终局冻结→重开→再次推进，至少有胜利和两种失败的逻辑证据；允许受控布置场景但伤害/结算/终局必须走真实路径。两张关键画面、Console增量、当前源码/保护资源hash与临时设置恢复；不做长时自然通关（BC209）。
5. 每个命令检查exit、外层/嵌套success与实际状态后再发依赖命令；所有unity command显式`--caller plugin --skill unity-cli --project-path H:/Projects/Unities/BattleCity`。Scene/asset/meta只用Editor API。Unity/CLI/Pipeline版本保持原配置，不升级。
6. 根AGENTS.md优先：长时间无可观察进展、反复失败、原因不明或方法不能完成时及时停止受阻自动化，请用户人工介入；明确可控原因最多一次有界纠正。不要等硬期限，不持续换方法或恢复。不要强关、重启、清日志、删除现场。写清Play/暂停/dirty状态、临时设置、已通过测试、缺口及最短人工步骤；允许人工证据补齐，不重跑源码未变的成功测试。

Writer只能写自己组件/集成边界及本项证据，不勾选计划、不写Acceptance、不自行开展BC207。fresh reviewer只读，主代理保留最终处置与验收权。
