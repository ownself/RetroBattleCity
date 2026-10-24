# BC-301 实现与验证（已验收，含用户人工闭环）

## 当前结论

**BC-301已验收。** 用户回传RealUpdates测试可正确执行，并确认实际游玩中各类敌人正常出生/工作（含高血量装甲敌人），关闭此前两项人工待办；详见 [Acceptance.md](Acceptance.md)。不要求额外截图或重跑，人工结果不伪装为自动Passed。

以下实现、自动结果、失败及停止经过保留为BC301原轮次历史；其中“待人工/未复测”描述的是当时状态，当前闭环以Acceptance.md为准，原始JSON不改写。主代理直接实施/自查，无subagent或独立审查，不冒称fresh review。

用户授权、项目原型规则及边界见 [Contract.md](Contract.md)。起点HEAD `a8daced9ef7f71708cd642deaf873dd21497eeb5`，保留BC-300未提交变更及 `.vsconfig`；无提交/推送。

## 实现

| 接缝 | 变更 |
| --- | --- |
| TankDefinition | 可选伤害Sprite前缀数组；空数组兼容旧定义，否则长度=HP且首项=默认前缀；校验所有帧 |
| TankState | 出生时快照DefinitionId、KillScore；暴露MaxHitPoints，沿用原有减血/死亡幂等逻辑 |
| EnemyTankView | 预解析伤害阶段×四向×两帧，按已受伤次数选择；仍为只读投影，无独立受击计时器/材质染色/碰撞 |
| ArenaSession | 死亡转换时累计基础Score与DefeatedEnemyCount，非致命命中不计分、同tick过量命中只计一次；重开新session归零 |
| ArenaController/EnemyBalanceDefinition | 在绑定时验证全部伤害阶段帧，避免出生后才因缺帧抛错 |
| EnemyTypesAuthoring | 显式升级菜单及API，不重建场景；设置空的默认装甲色阶并替换Stage01唯一序列/映射，保留地图、坐标、种子、时序、上限 |
| Stage01Authoring | 完整重建recipe使用同一四类型映射和20个混合原型序列 |

实际Stage01敌军：`[enemy.basic, enemy.fast, enemy.power, enemy.armor]`循环5次，每类5个，总数20/上限4/准备60tick/间隔120tick/种子1。所有速度、HP、分值仍沿用BC-300待校准默认，不宣称原版序列。

装甲HP4：白(4)→绿(3)→黄(2)→红(1)→死亡；所有普通敌对弹伤害1。击杀分值100/200/300/400；仅基础会话累计，不实现分类结算/奖励/HUD或最高分（BC-308/后续）。

通过Unity Editor API修改 `EnemyArmor.asset`、`Stage01.asset`、`Stage01EnemyWave.asset`；未手写YAML/meta。原始三资产副本在 `Temp/BC301-BeforeAssets/`。Stage01场景本身、CombatArena、旧基础敌原型、图集和持久项目设置不变；Audit确认Stage01仅序列变化，无地形/出生点/基地/上限变更。

## BC301实施轮自动结果（历史快照，人工闭环见Acceptance.md）

| 检查 | 结果与原始证据 |
| --- | --- |
| 新EditMode专项 | 首轮15/16；玩家死亡夹具从地图外向上射击被边界先消费，改为地图内向下射击后仅该项1/1 Passed；见 `Edit-Result.json`、`Edit-Retest-Result.json` |
| 新PlayMode专项 | 2/2 Passed，见 `Play-Result.json` |
| BC300数据回归 | 34/34 Passed；更新原Stage01旧序列断言为混合原型，不删除旧原型资产保护断言 |
| Stage01地图数据回归 | 6/6 Passed，保留676格参考图/坐标/出生/绑定断言，序列断言升级为四类型各5 |
| TankState共享伤害回归 | 24/24 Passed |
| 波次模型回归 | 30/30 Passed |
| 会话生命周期Edit回归 | 10/10 Passed |
| 玩家生命周期Edit回归 | 15/15 Passed |
| 旧敌人显示Play回归 | 12/12 Passed |
| 旧实际波次Play回归 | 4/5 Passed；1项Sprite托管包装SameAs断言失败；修正原生ID断言后未复测，待人工 |
| 旧会话生命周期Play补充 | 同步入口返回0项，未实际执行，不记通过；已有新Play专项覆盖本项重开归零 |
| 证据并集 | **154唯一用例：153 Passed、1未解决/待人工**；不是全量/单次全绿，见 `Audit.json` |
| 最终编译 | `Compile-Handoff-Result.json`：completed、failed=false、compilationFailed=false |
| 最终Editor | `Cleanup.json`：Stage01/stopped/not paused/clean/background=false |

### 实际运行证据

新Play专项输出：

`BC301 Stage01 natural mixed types=4 frame=20->107 tick=601->614 score=100 pending=16`

- 四类型经真实Stage01波次/AI/移动路径自然物化，逐一检查正确Sprite。
- **受控到达阶段**：前601tick由测试逐tick驱动并清除子弹，以防无关基地失败，不声称自然游玩通关。
- 随后释放暂停由真实Controller.Update推进，frame/tick均增长；真实武器命中基础敌人→死亡/显示释放/100分；重开归零且序列从Basic重新开始。
- 装甲专项验证四个HP色阶、四向/两帧、暂停稳定、死亡隐藏与renderer复用；显示不反写逻辑。
- 本轮没有保存最终GameView截图；待用户提供最小视觉确认，不宣称截图验收通过或人工体验已确认。

## 失败/停止与Console

1. 玩家死亡夹具从地图下边界外开火导致预期死亡不发生；有界修正夹具，1/1复测通过，未改生产碰撞。
2. 旧波次测试首轮SameAs失败，预期/实际Sprite名字相同，可能是托管包装身份问题；改用原生身份比较，仍需复测才能确认，不将失败当作无效证据删除。
3. 首次改断言误用GetInstanceID，Unity6000.6明确CS0619禁用该API。本轮没有正确检查failed编译结果就发起复测，导致test状态running但Editor stopped。一次focus无进展后取消，停止受阻自动化。主代理负责此执行错误，不归因用户环境。
4. 改为编译器要求的GetEntityId，只检查最终编译通过，没有再自动重跑受阻测试。
5. 同步PlayMode入口3次均返回0项，原始 `*-SyncEmpty.json` 保留；仅一次有界改回此前成功的异步入口，旧显示12/12、旧波次4/5得到真实结果，不能把空结果算通过。旧会话Play未补跑。
6. 解析同步结果时JSON字段实际为大写Summary/Results，脚本KeyError；仅修正解析，没有重跑已通过的BC300 34项。

Console session不变，cursor836→986：累计error5→8，warn1→1；新增3条Error正为2个GetInstanceID编译错误及1个无法进入Play。原件 `Console-Increment.json` 保留。**不能声称本轮Console增量0错误**。修正后最终编译失败标记false，Editor当前Console error/warn=0/0，见 `Console-Handoff.json`；日志未由主代理清空。

## 清理、源码与后续

- test cancel成功，工具状态cancelled；没有活跃测试writer，不自动进入BC-302。
- 返回Stage01，恢复后台false；临时InitTestScene在编译/恢复期间已被测试框架清理，最终无该未跟踪文件，保留此前Temp副本。
- 本轮新生成的默认Pipeline JSON已移动到Temp保留；持久项目设置无diff。未改变AutoTick或GameView设置。
- `git diff --check`通过，仅有Windows行尾提示；Audit保存32项当前实现/资产/meta hash、保护资源及原始结果集合。无独立审查。
- 原人工交接现已关闭：用户确认目标测试可正确执行及试玩各类敌人正常；不再要求截图或重复测试。见Acceptance.md，人工反馈单独记载，不改历史自动统计。阶段三当前BC300/301/302共3/10，M3未通过。
