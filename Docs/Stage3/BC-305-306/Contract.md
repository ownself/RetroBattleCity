# BC-305 / BC-306 本轮契约与lane board

## 授权和基线

用户要求依据总计划利用subagent实现BC305和306；仅此两项，不开展307～309，不提交/推送。HEAD main/bdb20b0e509276be1d4818c09638e5f419e463e4；保留既有BC304未提交成果与.vsconfig。起点hash/diff见Temp/BC305306-Before。Unity6000.6.7f1，CLI1.0.0-beta.14检查为最新，Editor ready。

根AGENTS.md适用于所有child：长时间无进展、重复失败或现有方式无法完成时停止受阻操作并请求人工；明确低风险原因仅一次有界纠正。保留源码、原始结果和现场，不能以反复重启/恢复/换工具代替人工。人工可完成验证，不提前勾选。

## 项目默认规则（未经原版校准）

- Clock：默认600个未来完整战斗tick，重复刷新而非累加。冻结全部活敌坦克（含后续出生）决策/RNG、移动、射击、武器冷却和履带推进；不冻结玩家、在途子弹、特效、波次预留/生成。冻结坦克仍可受击且阻挡。暂停不扣时；玩家死亡不取消全局冻结，终局/退休/重开清零。
- Grenade：新收据只结算一次，消灭拾取时全部活敌（含装甲），不伤玩家/基地/地形、不清除在途弹、不杀预留/未出生敌。走共享幂等死亡/爆炸/分数通路，不伪造子弹命中，不触发道具敌人的“首次有效弹伤”掉落。批量死亡后本tick重新裁决胜负，失败优先；不得出现先胜利后拾取或重复分数。波次以实际活敌计数守恒，本tick手雷之后不额外倒流生成一次。
- Shovel：默认1200未来完整战斗tick，重复刷新不叠加。基地16px足迹外左/上/右各8px厚U形环（底边不加墙），只影响边界内格。首次启动快照当前布局（含残砖mask），到期恢复该快照，不固定重造原关卡/砖布局；有效期间再次拾取不覆盖快照，可重新补强已毁钢格。
- 墙改为既有Steel规则（最高级玩家弹仍可破坏），不另建第二碰撞真相。激活补墙或到期恢复到非空阻挡时若与活坦克/出生预留重叠，逐格延后到安全战斗tick，不挤开/击杀/传送占用者；移除墙或恢复空格无需等待。到期状态可保留“恢复待办”直至安全；不再主动补钢。重开/失败/胜利/退休取消计时及待办并还原快照，不在终局再推进移动。无基地的会话消费铁锹收据但不造隐形墙。
- 所有效果配置按会话快照；仅新TryCollect收据分派，无键盘作弊或生产测试入口。仍不增加BC308拾取奖励/分类计分。

## 拓扑

整体multi-seam。当前工作区串行唯一writer，不使用遗漏BC304未提交成果的旧HEAD worktree。

| Lane | cwd/隔离 | 独占契约/文件 | 权限 | 下一门禁/交接 | 独立依据 |
| --- | --- | --- | --- | --- | --- |
| BC305 | H:/Projects/Unities/BattleCity；原cwd串行 | PickupDefinition/State、ArenaSession共享死亡/冻结/手雷纵切片及专项、Pickups资产必要字段 | 实现/Editor API/聚焦测试；不改306地图组件 | Docs/Stage3/BC-305/Verification.md，实际编译/专项通过 | 时钟与手雷均在既有会话tick/收据/死亡/终局同一接缝，拆开会重叠结算顺序 |
| BC306-map | 同cwd，305交接后 | MapState最小精确地形变更API；新BaseFortificationState及EditMode专项；不改ArenaSession/Controller/Pickup配置 | 实现/组件验证；不可接线整个306 | Docs/Stage3/BC-306/Map-Handoff.md，API/端点/占用专项通过 | 快照/恢复/延期地形模型可不依赖会话独立验证 |
| BC306-integration | 同cwd，组件冻结后 | ArenaSession/PickupDefinition必要配置快照/资产、地图投影兼容及Edit/Play集成 | 仅接线已交接模型，不能重写模型；必要组件缺陷向父代理报告 | Docs/Stage3/BC-306/Verification.md，聚焦集成+相邻回归+真实Update/Console/保护hash | 适配组件至共享tick/终局/退休与显示，不包办地图算法 |
| final-review | 同cwd，全部writer终止后只读 | 本轮相对baseline精确增量及原始证据 | fresh审查，不操作Unity/不改源码 | managed review artifact→主代理裁决/验收 | 独立审查跨效果的顺序与守恒、占用/恢复及证据 |

所有child显式openai/gpt-6.1-sol，不递归委派；源码文件工具编辑，asset/scene/meta通过Editor API；保护场景、PNG、地图/波次/相机/HUD和ProjectSettings。聚焦重跑仅受影响项，不重复用户已通过玩法或全量回归。每个阶段受阻立即交接，不以硬超时作为主动求助点。
