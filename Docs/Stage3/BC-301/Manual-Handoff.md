# BC-301 人工验证交接（已关闭，保留原步骤）

用户已确认RealUpdates测试可正确执行，并实际游玩确认不同种类敌人正常出生/工作（包括高血量装甲敌人）。两项人工待办据此关闭，无需再补截图或重复测试；详见 [Acceptance.md](Acceptance.md)。以下为原交接历史，不表示当前仍受阻。

> 后续版本说明：用户另行授权BC-302，当前工作区已完成玩家升级/武器/钢墙及Stage01单字段绑定；源码版本以 `../BC-302/Audit.json` 的SHA256为准。选择的旧波次用例没有自动重跑，现由用户报告复测正常闭环；BC302新增截图/回归不代替本次用户反馈。下文153Passed/错误经过等属于BC301原轮次记录。

## 原交接时状态（历史）

- 功能实现已落盘：Stage01四类混合波次、4HP装甲白→绿→黄→红、基础击杀分数/去重/重开归零。
- 新专项16个EditMode、2个PlayMode最终均Passed；相邻回归有153个唯一Passed证据，另1个旧波次测试断言待复测。不是全绿验收。
- 该旧测试首轮因 `Is.SameAs` 比较托管Sprite包装身份失败（预期/实际Sprite名称一致）。已将断言改为比较原生 `GetEntityId()`；它仍要求同一原生Sprite，不只是同名。**此断言修正后未复测**，不能先假定只是包装问题或已经通过。
- 修正中先误用Unity6000.6禁止的GetInstanceID，造成2个测试编译错误及1条无法进入Play的Error。此错误已改为GetEntityId，最终编译completed/failed=false。最初自动测试停在running、Editor实际stopped正由此造成；本轮未正确在failed编译状态下阻止发起测试，是主代理执行失误，不归因游戏逻辑或用户环境。
- 已取消受阻自动测试；不自动重跑、不重启Editor或清空日志。

## 原交接现场与版本（非用户试玩后的实时状态）

- `main / a8daced9ef7f71708cd642deaf873dd21497eeb5` ＋ 本工作区保留的BC300/301未提交增量，未提交推送。
- Unity 6000.6.7f1；当前 `Assets/BattleCity/Scenes/Stage01.unity`，**不在Play、不暂停、场景clean**。
- 临时后台运行已恢复false（与HEAD和BC300结束状态一致）；无临时输入设备或观察器。
- 测试临时InitTestScene已在编译/恢复过程中消失；回到Stage01后DeleteAsset返回false，最终git无该文件，不冒称是该删除调用删除成功。此前副本保留在 `Temp/BC301-BeforeAssets/`。
- 本轮API引入的默认Pipeline配置已移至 `Temp/BC301-EditorPipelineConfig.json` 保留，不留项目设置增量。不要删除任何用户文件。
- 原始失败、编译错误、取消记录及验证摘要都在本目录。Console保留历史错误；最终编译失败标记false、当前Editor Console error/warn为0/0。

## 原交接步骤（已关闭，无需再次执行）

### 1. 单项PlayMode复测（无需重跑全部测试）

1. 打开 `Window > General > Test Runner`，选择 **PlayMode** 页。
2. 搜索 `RealUpdatesSpawnMoveFireAndRenderUpToCap`，只选下面这个用例，点击 **Run Selected**：
   `BattleCity.Tests.EnemyWaveIntegrationPlayModeTests.DefaultStage01_RealUpdatesSpawnMoveFireAndRenderUpToCap_PauseFreezesPreparationAndInterval`
3. 等待约10～20秒，预期 **1 Passed**：真实Update出生/移动/射击、暂停冻结、恢复、最多4敌及显示一致。
4. 请返回Test Runner结果截图；若失败，复制失败详情/堆栈（尤其Sprite原生ID对比）和新增Console错误。不要反复重跑或自行删除资源。

本轮已经通过的其他用例不需要重跑，尤其新BC301的2个PlayMode用例已含四类出生、装甲色阶、真实帧tick推进、击杀100分及重开归零。

### 2. 最小视觉确认/一张图

1. 打开 `Assets/BattleCity/Scenes/Stage01.unity`，点Play，按Enter开始。
2. 正常观察约10秒（前4个依次为基础、快速、快速子弹、装甲），可使用WASD/方向键和Space；确认没有缺失Sprite、紫色材质、显示残留或Console新错误。
3. 请返回一张游戏画面，并说明上述显示是否正常；无需再次完整通关或专门打4发装甲（已有自动专项覆盖）。观察完退出Play即可。

## 回传后

已按本次用户回传补齐结论，人工结果单独记录于Acceptance.md；不机械重跑、也不改写失败原件。BC301现已勾选，阶段三BC300/301/302共3/10，M3仍未通过。用户试玩后的Play/暂停现场未读取或改动。
