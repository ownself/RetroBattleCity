# 星星升级继承到下一关

## 问题与边界

用户报告星星等级似乎不能继承到下一关。根因确认：`ArenaController.AdvanceStage()`此前调用通用`RestartSession()`，新`ArenaSession`的`PlayerUpgradeState.Level`总从0开始。已有Campaign测试也仅在零星情况下切关，未覆盖非零等级。

本轮基于HEAD `7b93ef8`及前一轮尚未提交的弹速调校补丁，保留全部既存修改和`.vsconfig`。只处理**星星等级的正常通关继承**，不扩展分数/生命的跨关规则，也不改变升级、死亡或道具效果。

## 行为

- 正常胜利→结算→下一关：继承通关时的当前星星等级（0～3）、对应坦克Sprite和武器参数。
- 一星快弹、二星两发、三星破钢能力在下一关StageIntro/Ready时即正确初始化；不是开战后补发Star，也不制造道具收据或加分。
- 仅传递整数等级，不复用旧Session、升级状态对象或Weapon；旧子弹/租约、射击冷却和临时效果仍按原新会话路径清理。
- 死亡仍按现有规则归零；如果通关前死亡，携带的是死亡后的当前0级，不恢复曾经获得的最高等级。
- R重开当前关、初始选关/重新选关、Game Over/全通关后新一轮游戏仍为0级。
- 正常下一关仍禁止选关；未改变前一轮120/240px/s、15tick冷却、1/2发上限及仅三星破钢的调校。

## 实现

- `ArenaController.cs`：`AdvanceStage()`先读取当前等级，再使用同一新会话/显示清理路径创建下一关；`CreateSession`/`RestartNow`可传入初始等级，其他调用默认0。不存储可误复用的Campaign最高等级缓存。
- `ArenaSession.cs`：构造器新增末尾可选`initialPlayerUpgradeLevel=0`，拒绝越界或无升级表却指定非零等级；建立新PlayerTank后使用既有升级能力初始化武器/等级。没有添加运行中任意设置等级的公开接口。
- 新增/增强`PlayerUpgradeTests.cs`和`CampaignPlayModeTests.cs`。无本轮资产、场景、Prefab、素材、ProjectSettings或Editor配方修改；无提交/推送。

## 回归证据

先写真实双关流程回归，用一星通关进入下一关：修复前明确失败`Expected 1 / But was 0`，见`Regression-BeforeFix.json`。这是有意记录的缺陷复现，不是受阻自动化反复重试。

修复后编译成功，所有定向用例通过：

| 套件 | 最终结果 |
| --- | --- |
| PlayerUpgradeTests | 35/35 EditMode |
| CampaignPlayModeTests | 5/5 PlayMode |
| PlayerUpgradePlayModeTests | 4/4 PlayMode |
| SinglePlayerFlowPlayModeTests | 3/3 PlayMode |
| PlayerPickupPlayModeTests | 2/2 PlayMode |
| 去重合计 | **49唯一最终Passed：35Edit＋14Play** |

新增8项EditMode：0～3等级在Ready/tick0/暂停状态下正确初始化Sprite/武器参数，仍拒绝Ready时的游戏内升级，无子弹/冷却/分数/结果副作用且不修改资产；-1、4、int.MaxValue及无升级表非零等级被拒绝。既有死亡/复活归零、旧弹快照、钢墙与前一轮弹速/双发时序测试仍通过。

新增4项Campaign PlayMode：

1. 一星、二星、三星分别经真实输入/Controller流程，在受控胜利后进入第二关，核对StageIntro与开战等级/Sprite/弹速/容量/破钢、旧子弹释放、新武器冷却为0、不同升级状态对象、计分/结果账本仍新建；下一关再升级从继承等级继续，三级仍封顶；R与重新选关归零。
2. 先三级，再用实际子弹击杀玩家、确认死亡归零，然后受控胜利切关；只继承0级。

增强原Campaign流程用例：末关三级通关后PLAY AGAIN恢复0；后续带升级失败返回Title也恢复0；保留同host/快照隔离、正常推进禁选关、重开选关开放、终局与输入抑制断言。

Campaign测试使用临时双关/单敌人和直接升级能力设置前置状态，胜利用受控射击完成，不冒称自然通关/自然获得全部Star。相邻PlayerUpgrade PlayMode仍覆盖三颗Star实际收集与升级武器、真实钢墙/Tilemap移除、重开恢复普通弹；SinglePlayerFlow和PlayerPickup仍覆盖实际键盘/Update及重开清理。原始结果各自保存同目录JSON。

## 环境与保护

- Unity 6000.6.7f1 / CLI 1.0.0-beta.14 / Editor7800，最终编译成功，见`Compile.json`。
- Console cursor2794→2853新增59条全log，无新增warn/error；最终groundTruth错误0/警告0、compilationFailed=false，历史缓冲保留，见`Console*.json`。
- 最终`SinglePlayer.unity`、非Play/非暂停、场景clean、runInBackground=false，见`Before.json` / `After.json`；无临时输入设备、观察器或设置遗留。
- `git diff --check`通过，仅LF→CRLF提示。4个本轮源码/测试SHA256、49项去重统计及前一轮20项未变文件的hash保护见`Audit.json`。前一轮唯一同轮继续增补的文件为PlayerUpgradeTests，原弹速相关断言未删改。
- 本轮没有全工程回归、构建、用户自然通关或人工手感已通过声明；无需机械重跑前一轮已通过且未变的其他测试。
