# BC-208 八边界证据查缺（writer，非验收勾选）

基线 main/eb858f3579626b5e66ba253afb94cdb0240118f2。先查旧成功原始结果，再选择补测；不重跑历史专项/全量。完整逐例 FullName/Status/原始文件见 `Raw/reused-cases.json`（20 个唯一历史用例）。

| 边界 | 已通过用例（前缀均 BattleCity.Tests.） | 原始结果 |
|---|---|---|
| 敌军清零 | `SessionLifecycleIntegrationTests.ActualSharedBullets_LastEnemyAndFailures_SettleAllDamageBeforeOneFrozenOutcome(False,False,Won,None)` | `Docs/Stage2/BC-206/P2-Edit-Current.json` |
| 基地毁坏 | `SessionLifecyclePlayModeTests.RealControllerUpdate_ReadyStartHeldInputFocusTerminalAndRestartEdges` | `Docs/Stage2/BC-207/Raw/lifecycle-play-result.json` |
| 生命耗尽 | `PlayerLifeIntegrationTests.ThreeActualGenerationsSpendExactlyThreeLives_ThenNeverRespawn + SessionLifecycleIntegrationTests.ActualSharedBullets_LastEnemyAndFailures_SettleAllDamageBeforeOneFrozenOutcome(False,True,Lost,PlayerLivesExhausted)` | `Docs/Stage2/BC-206/Final-Full-Edit-Status-01.json + BC-206/P2-Edit-Current.json` |
| 暂停 | `SessionLifecycleIntegrationTests.ReadyStartPauseFocusAndHeldCombat_TransitionsNeverContributeElapsed + SessionLifecyclePlayModeTests.RealControllerUpdate_ReadyStartHeldInputFocusTerminalAndRestartEdges` | `Docs/Stage2/BC-206/P2-Edit-Current.json + BC-207/Raw/lifecycle-play-result.json` |
| 复活保护 | `PlayerLifeIntegrationTests.SharedEnemyWeaponDeath_AbsoluteDelayAndProtectionEndpoints_ConsumesProtectedHits + PlayerLifeIntegrationPlayModeTests.Stage01_RealUpdateSharedEnemyWeaponDeathHiddenWaitRespawnShieldExpiry_PauseAndFocusFreeze` | `Docs/Stage2/BC-206/Final-Full-Edit-Status-01.json + BC-206/Recheck-Test-Status-01.json` |
| 出生受阻 | `SessionLifecycleIntegrationTests.EmptyFieldPendingBlockedAndReservationNeverWins_BirthSettlesBeforeOutcome + PlayerLifeIntegrationTests.ReadyBirthUsesFullSixteenPixelLivingTankFootprint_DeadPositionNotReserved(39,False)/(40,True) + PlayerLifeIntegrationTests.EnemyReservationAndThenLivingEnemyBlockReadyPlayer_UntilDamageReleasesSpace` | `Docs/Stage2/BC-206/P2-Edit-Current.json + BC-206/Final-Full-Edit-Status-01.json` |
| 连续重开 | `SessionLifecyclePlayModeTests.ThreeRestarts_RestoreSnapshotsNaturalFirstSpawnLeasesAndPresentationWithoutHostAccumulation` | `Docs/Stage2/BC-207/Raw/lifecycle-play-result.json` |
| 同tick胜负冲突 | `SessionLifecycleIntegrationTests.ActualSharedBullets_LastEnemyAndFailures_SettleAllDamageBeforeOneFrozenOutcome(True,False,Lost,BaseDestroyed)/(False,True,Lost,PlayerLivesExhausted)/(True,True,Lost,BaseDestroyed, PlayerLivesExhausted)` | `Docs/Stage2/BC-206/P2-Edit-Current.json` |

## 当前源码兼容理由

`Raw/source-compatibility.json`逐文件对照206最终68份源码hash。核心 ArenaSession/SessionFlowState/PlayerLifeState/BulletWorld/EnemyWaveState与上述生命/会话测试逐字节未变；BC207变化限HUD投影、Controller显示绑定、作者recipe、asmdef引用及Stage01显示。BC207已重跑生命周期10 Edit+3 Play，原始`BC-207/Raw/lifecycle-edit-result.json`、`lifecycle-play-result.json`验证当前接缝。本轮不更改任何生产源码或旧测试。

## 唯一追加路径（BC209）

八边界已有功能断言，不为BC208制造重复测试。旧三次出场测试为小fixture且未启用整局；真实Stage01 Update复活专项只消耗一次生命，HUD终局测试用1生命fixture。故当前Stage01实际Update完整3→2→1→0→Lost→物理R Ready仍缺一条整合证据，拟只新增一个受控PlayMode用例；不冒称自然游玩。胜利与基地毁坏复用本轮用户明确人工反馈，Ready/Paused和缩放复用207三图，不再打自然胜利。

## 用例源码文件（相对项目根）

- `Assets/BattleCity/Tests/EditMode/SessionLifecycleIntegrationTests.cs`：清零/真实共享子弹同tick冲突/阶段暂停/敌出生受阻；最终10例与BC207再验证均通过。
- `Assets/BattleCity/Tests/EditMode/PlayerLifeIntegrationTests.cs`：三代耗尽/保护端点/玩家出生占位、预约阻塞。
- `Assets/BattleCity/Tests/PlayMode/SessionLifecyclePlayModeTests.cs`：真实Controller管理键/基地毁坏终局、三次重建快照与显示清理。
- `Assets/BattleCity/Tests/PlayMode/PlayerLifeIntegrationPlayModeTests.cs`：真实Update死亡、隐藏等待、复活盾到期、暂停/失焦冻结。
- `Assets/BattleCity/Tests/PlayMode/ArenaHudPlayModeTests.cs`：当前HUD真实阶段、数字与R重绑定。

表内简写的参数组合以`Raw/reused-cases.json`中精确FullName为准（含flags格式）；每条记录保留原始结果路径、Passed、耗时及消息。新增`Stage01LifeExhaustionPlayModeTests.cs`后一次1/1通过，只为BC209上述真实缝隙补证；八边界无遗留功能覆盖缺口，仍由主代理决定验收。
