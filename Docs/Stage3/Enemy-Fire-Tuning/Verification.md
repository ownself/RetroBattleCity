# 敌AI随机开火等待：难度反馈补调

## 用户反馈与修正

2026-10-25，用户反馈敌人“只要可以发射就发射”，难度过高。原EnemyBrain在移动和全阻挡分支均每tick返回FireHeld=true，武器冷却/在场弹数成为唯一限制。

本轮仅调整AI射击意图，不改变玩家武器、敌人移速/弹速/HP/基础分、地图/波次内容或已实现307～309成果。未进入BC400，不提交/推送。

- `EnemyBrainParameters.MinFireDelayTicks/MaxFireDelayTicks`默认45/105，即60Hz下0.75～1.75秒，正数且min≤max校验。可通过参数实例调整；不是Inspector新增字段或已核实原版数值。
- 每个EnemyBrain出生/注册时就抽取首发等待。每次倒计时结束只在当tick请求一次开火，随后重新抽取等待，不再持续按住扳机。
- WeaponState仍负责冷却和在场弹上限。若请求被拒绝，丢弃本次机会、不排队；释放弹数名额不会立刻补射。因此实际两发间隔可能更长，但不会短于AI请求间隔。
- 独立xorshift32射击流（原seed混合常量，零种子状态有非零回退），不消耗方向RandomState，不调用UnityEngine.Random。同seed与输入可复现；不保证因破砖时机改变后整场路径仍相同。
- 暂停/Clock冻结时不推进等待、尝试数或射击RNG；死亡之后不再请求，退休会话不推进，新实例重开按种子重建。移动/转向的既有逻辑和随机流保持独立。

这是首轮降低密度的默认值，主观手感需要用户实际试玩再调；不声称原版精确或难度已最优。

## 实际验证

Unity6000.6.7f1 / CLI1.0.0-beta.14（已检查最新版），主代理直接实施与验证；未委派、未新增人工验证结论。

**99唯一最终Passed：97 EditMode + 2 PlayMode。** 新专项11 Edit+1 Play。逐项原始证据、去重台账和源码hash见 `Audit.json`。

| 范围 | 结果 |
| --- | --- |
| EnemyBrainTests | 40/40：默认初始/后续随机间隔、4种seed/零状态边界、弹数满错过请求不排队、阻挡仍按间隔射击、独立移动RNG、非法配置、冻结+暂停、同seed/30/60/144delta、既有死亡/转向/武器 |
| EnemyWave相关 | 首42/43，旧“出生下一tick必开炮”断言因预期行为变化失败；一次更新为下一tick开始移动但仍等待射击，定向1/1。原始Wave-Result.json与Wave-Fix-Result.json均保留 |
| Clock/Grenade相邻Edit | 14/14，冻结、伤害/批杀和波次规则不回退 |
| EnemyBrain Play | 2/2：实际Controller.Update注册AI等待后射击/暂停与恢复；Stage01自然波次首发等待 |

几个旧移动/武器接线测试显式传入1/1tick请求策略，继续验证其原来的立即开火碰撞前提，不代表生产默认：实际默认由新专项和真实Stage01自然出生测试独立覆盖。旧Replay/Pause快照已包含FireTicksRemaining/FireRandomState/FireAttempts。

Console证据：
- 实际Update：frame12→357→498→507，tick0→49→50；等到首发后暂停，等待/射击RNG/计数冻结，恢复只推进1tick，DroppedTicks仍0。
- Stage01自然出生敌首发发生于出生后第87个完整战斗tick（1.45s，受控AdvanceFrame推进）；该项不冒称自然等待试玩。
- Console2192→2235新增43条全部log，0新warning/error。首轮NUnit失败单独保留，不因Console安静称一次全绿。

## 保护与现场

起点 `Temp/EnemyFire-Before/` 包含391项Assets/ProjectSettings hash、原有dirty状态与diff。最终仅5个预期C#改变（2生产AI+3测试），其他386项一致；所有已有场景、数据资产、原始Sprites/meta、ProjectSettings及用户Stage01移动、.vsconfig和BC307～309未提交成果保留。没有新资产/手写meta、包升级或临时运行观察器。

最终Editor ready/stopped/not compiling/runInBackground=false，Stage01 clean、not paused；测试键盘/附加场景已TearDown释放。`git diff --check`通过，无staging/commit/push。

本轮为用户反馈下的定向平衡补丁，不重新声称完整M3回归；M3历史验收及原始失败证据不覆盖、不改写。尚无用户对此默认间隔的实际试玩反馈。
