# BC-304 验证与验收

## 结论

BC-304通过本项验收；阶段三必选5/10（BC300～BC304），M3未通过。不开展BC305/306/308，不提交/推送。

验证者：主代理直接实现、实际运行Unity测试并核对原始结果，无subagent或用户人工反馈。本轮2026-10-25，Unity6000.6.7f1、CLI1.0.0-beta.14（已检查为最新）。起点main/bdb20b0e509276be1d4818c09638e5f419e463e4，仅既存.vsconfig，未修改该文件。规则全部是项目待校准默认，非已核实原版。

## 实现

- `PickupDefinition`/`Pickups.asset`：头盔600tick、奖励生命上限9，正数校验；通过Editor API保存既有资产，仅新增这两个字段，seed/携带序列不变。
- `PickupState`：会话快照与独立头盔倒计时；伤害之后递减、拾取之后不立即扣时；重复刷新，不累计。
- `ArenaSession`：只在新TryCollect成功时分派星星/头盔/坦克，不重放收据；沿用等级0～3/旧弹快照、一次加命、复活保护与头盔保护取并集；死亡/终局/退休清理；Clock/Grenade/Shovel仍仅收据，不增分。
- `PlayerLifeState.TryAddLife`：仅活且未耗尽、未到奖励上限时+1；无复活副作用、不截断较高初始生命、无int溢出。
- `ArenaController`：复用现有shield两帧，支持头盔单独启用，无第二套伤害或时间源；等级Sprite和生命HUD沿用现有投影。
- 新增两类测试及Editor生成的meta，规则详见 `Contract.md` 与 `../../Stage0/Core-Rules.md`。

## 实际自动结果

定向证据去重为 **118唯一Passed：105 EditMode + 13 PlayMode**。不是一次全量/一次全绿声明。`Audit.json`按FullName汇总原始结果与变更/保护文件hash。

| 范围 | 唯一通过 | 原始证据 |
| --- | --- | --- |
| BC304新增EditMode | 26 | `Edit-Result.json`25项；`AuthoredEffectDefaultsAreExplicitAndUncalibrated-Result.json`新增1项；`Helmet_AndRespawnProtectionTickIndependently-Result.json`为增强后单项1/1（不重复计数） |
| BC304新增PlayMode | 2 | `Play-Result.json`，实际键盘/Update拾取三种道具、显示/暂停/连续重开 |
| 既有升级Edit/Play | 23 + 3 | `PlayerUpgradeTests-Result.json`、`PlayerUpgradePlayModeTests-Result.json` |
| 既有道具框架Edit/Play | 20 + 3 | `PickupTests-Result.json`、`PickupPlayModeTests-Stage01_-Result.json`、`PickupPlayModeTests-CombatArena-Result.json` |
| 既有生命模型/集成Edit | 11 + 15 | `PlayerLifeStateTests-Result.json`、`PlayerLifeIntegrationTests-Result.json` |
| 既有生命集成Play | 2 | `PlayerLifeIntegrationPlayModeTests-Result.json` |
| 既有会话生命周期Edit/Play | 10 + 3 | `SessionLifecycleIntegrationTests-Result.json`、`SessionLifecyclePlayModeTests-Result.json` |

过滤器是部分匹配：旧PickupTests筛选连带25新Edit，旧PickupPlayModeTests.Stage01筛选连带2新Play；均有执行证据，但去重不把重复算成新增，也不再重复运行这些成功案例。首次错写SessionLifecycleTests筛选得到0项，保留0项原始结果，不算通过；之后使用正确类名执行10项。新测试增强了“较长复活保护不被较短头盔覆盖”的独立端点，仅补单项及新增配置断言。

新增覆盖：三奖励一次性/重复拾取、满级/满生命仍消费、600配置快照、精确未来伤害阶段/敌弹消费、长短两方向复活盾叠加、暂停/Ready/失焦、死亡不能救回但非终局复活可拾取、星星死亡归0/旧弹不变、终局清盾/清槽且不加命、其他三图标不提前生效、可选能力不隐式创建、参数非法值与生命整数边界。

## 真实运行和显示证据

`PlayerPickupPlayModeTests.Stage01_RealKeyboardUpdateCollectsAllThree_ProjectsUpgradeShieldAndLifeHudOnce`在真实Stage01加载后，用真实掉落算法有界选取所需类型/可达目的地，再通过Input System键盘A和Controller.Update实际移动拾取，不使用反射/伪收据或生产调试快捷键。

`Console-Delta.json`：首次frame13→132、tick0→17，后一次相邻筛选连带执行frame40→148、tick0→17；都确认星星level1 Sprite、头盔shield剩余592、shield位置/启用状态正确、生命与HUD为4、暂停不消耗计时、收据不重放。连续3次重开确认旧会话退休清盾，新会话Ready/等级0/生命3/头盔0、读取启动快照且只有一个shield renderer。

这是真实Update受控测试，不是自然游玩/随机掉落通关，也不是用户人工手感确认。本项未留新截图或重新做像素缩放矩阵；已以实际renderer Sprite/位置/启用和HUD状态断言覆盖本项显示接线，不声称新增截图/逐像素视觉或人工验收。

## 首次失败与有界纠正

- 首次测试代码误调用TankState内部ApplyHit，产生两条CS1061：`Compile-Status.json`、Console seq1504/1505原件保留。删除这个不属于公共契约的强制死亡测试，改用真实弹伤/保护到期死亡、终局及重开路径覆盖；生产代码未因此变更。`Compile-Fix-Status.json`与`Compile-Final.json`均completed/failed=false。编译未成功时首次启动测试得到0项，保留`Edit-Initial-Status.json`，不计为通过。
- 一次本地回归脚本把CLI标记放在`--`之后，请求在发往Editor前被拒绝（工具结果为RuntimeError/空stdout），纠正参数位置后正常执行；没有切换工具/协议或持续重试。
- Console查询首次误用after/limit参数，CLI参数校验拒绝，原件`Console-InvalidArgs.json`保留；读实际schema后改用since/tail，一次成功。没有将这些工具语法错误计为Unity运行缺陷，也不抹掉首次失败。

## Console与现场保护

- 完整Console增量cursor1503→1688，185条：2 error（上述已修正的测试编译诊断）、0 warn、183 log。**不声称整个实施过程0错误**。两条修正之后无新增runtime error/warn。
- 最终`Console-Final.json`：compilationFailed=false/compiling=false，Editor当前consoleErrors=0/consoleWarnings=0；历史buffer error10/warn1保留不清空。
- `Final-State.json`：Stage01 stopped/not paused/clean，runInBackground=false。没有改动/新增后台覆盖、临时观察器或输入设备；测试键盘均在TearDown移除，场景加载均卸载。
- 对比`Temp/BC304-Before.json`，只有6个既有实现/数据文件变化，另4个新增测试/meta；其余资产、PNG、场景、地图、波次、升级数据、相机/HUD、CombatArena和全部ProjectSettings原始hash保持一致。`Audit.json`留存变更前后hash及保护清单。
- `git diff --check`通过（Git提示LF/CRLF工作区策略，不是Unity警告）。无活跃测试/writer、无提交/推送；无需用户补跑已通过项。下一任务BC305等待新授权。
