# 道具种类重复：实际游玩不再反复重置固定种子

## 原因

用户报告未见铁锹、时钟、星星，而且首件似乎总是坦克。

实际SinglePlayer绑定的 `Assets/BattleCity/Data/Pickups.asset` 使用seed=303，携带者为波次索引3/10/17，即第4/11/18名敌人。旧Controller每次创建/重开/切关Session都复用该种子。

用真实 `PickupState.TryDrop` 验证：303种子的前三次成功掉落依次为 **Tank、Grenade、Helmet**。每关自然掉落上限正好为3，因此重开或过关都回到同一组。Shovel/Clock/Star并未被类型选择或Sprite映射排除，但在这条重复前缀中没有机会出现。

## 修改与边界

- `ArenaController.cs`：每次CreateSession为道具生成新非零种子；Guid本地熵与已快照配置组合，防止与紧邻上一Session种子相同。初次创建、选关、重开、Campaign切关共用此入口。禁用/启用显示组件不重新抽种子。
- 不使用Unity全局Random，不改变波次或敌脑随机流。`PickupState.InitialSeed`仍可读取本次有效种子用于复现。
- `ArenaSession.cs` / `PickupState.cs`：增加可选的显式种子参数；无覆盖参数的纯逻辑模拟仍用配置seed，给定同一有效种子得到同一类型/位置序列。显式0种子拒绝。
- `PickupDefinition.cs`：仅添加seed用途注释，没有修改序列化数据或现有规则参数。
- 六种均参与原有类型抽取，允许重复；不保证首件绝不为坦克，不强制每关包含六种，也未增加三名携带者/三次自然掉落机会。
- 道具效果、位置可达性、单槽替换、收据幂等、奖励分数、暂停/重开清理均不改。
- 保留此前贴墙、草丛及选关补丁。未改素材、场景、关卡、Campaign/道具资产、ProjectSettings或作者recipe；无提交/推送。

## 验证

Unity 6000.6.7f1，原编辑器ready，编译成功（`Compile.json`）。

| 套件 | 最终结果 |
| --- | --- |
| PickupTests | 25/25 EditMode |
| PickupPlayModeTests | 3/3 PlayMode |
| PlayerPickupPlayModeTests | 2/2 PlayMode |
| ClockGrenadePickupPlayModeTests | 2/2 PlayMode |
| ShovelPickupIntegrationPlayModeTests | 2/2 PlayMode |
| 去重合计 | 34唯一最终Passed：25 Edit＋9 Play |

- 新增5项纯逻辑测试：固定303三件套复现、显式seed与类型/位置/随机状态重放、种子1～120的首件覆盖六种、0种子拒绝、Session覆盖种子不影响波次/AI。
- 增强既有自然携带者/真实键盘拾取用例，不再假定拾取一定不是Star，而是核对实际收据和相应升级等级；保留图标、非致命受击、掉落/替换、一次拾取及计分断言。
- 增强重开用例：每次新seed非0且不同于前次；组件重绑定保持seed；通过真实有效seed重建PickupState并重放相同道具/位置，原资产seed不变。
- 既有实际键盘/Controller.Update验证覆盖Star升级显示、Helmet护盾、Tank生命/HUD、Clock敌AI冻结、Grenade击杀与计分、Shovel临时钢墙/暂停/到期恢复残砖及重开清理。效果测试为了专项验证会有界搜索目标类型，不冒称这些全是自然一局掉落或自然通关。

### 铁锹测试的一次有界纠正

铁锹长时序专项首轮2项失败：一项加速计时后预期remaining=1却为0，另一项预期占位延期恢复却未处于恢复状态，见 `Shovel-FirstFailure.json`。原夹具在1200tick等待期间仍允许自然敌军出生/移动/开火，终局可能提前清理正在验证的铁锹效果。

仅修改该测试文件：在三个自然出生锚点放置无AI、无开火命令的静止测试坦克，隔离无关战斗；保留所有时长、占位延期、残砖mask/Tilemap及重开断言。生产实现未修改。重编译及该2项一次定向复测通过；不重复已通过且未变的其他套件。不冒称首轮全绿，也不以隔离夹具代替自然战斗验证。

原始逐项结果见同名JSON；使用精确套件filter，Edit同步取Results，Play异步取test status。34项去重及7项源码SHA256见 `Audit.json`。

Console cursor2637→2692的55条增量全为log，最终编辑器错误0/警告0、compilationFailed=false；历史缓冲错误保留，见 `Console*.json`。

最终 `SinglePlayer.unity`，非Play/非暂停、场景clean、runInBackground=false，见 `Editor.json`；没有遗留临时输入设备、观察器或设置。

## 用户试玩预期

新开局/重开后，首次掉落应不再被固定为坦克；跨多局有机会见到全部六种。单局只有三次机会，仍可能重复类型，也可能第一件恰好仍是坦克。本轮没有用户人工试玩或自然多局分布统计结论，不宣称每局类型均衡或原版概率已校准。
