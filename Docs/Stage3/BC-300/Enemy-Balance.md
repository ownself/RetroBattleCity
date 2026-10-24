# BC-300：敌人类型与平衡数据

> 本文记录BC-300验收时的基线。用户随后授权BC-301，当前工作区已接入四类型Stage01波次、装甲伤害映射和基础计分（尚待人工补验）；下文“Stage01不变/反馈未启用”属于BC-300当轮边界，最新状态见 [BC-301验证记录](../BC-301/Verification.md)。数值默认与未核实性质不变。

## 范围和状态

- 用户本轮授权：开始实现 BC-300；本轮由主代理直接实施，不沿用阶段二历史 subagent 授权。
- 起点：`main / a8daced9ef7f71708cd642deaf873dd21497eeb5`，仅既存未跟踪 `.vsconfig`，保留不动。
- M2 已通过；本项只交付类型与数据，不实现 BC-301 装甲反馈/类型运行集成、BC-302 升级或 BC-308 计分累计。
- `TankDefinition` 继续是数值的唯一数据模型；新增 `EnemyBalanceDefinition` 只维护类型→定义引用及核实来源，不复制战斗参数。

## 默认参数 v1

**下列全部是项目默认值、待校准，不是已核实原版值。分值也未作原版核实。** 不将 Sprite 的名称或网上常见数值当作验证证据。此项没有已核实的原版数值，不需要联网取证才能交付原型数据。

逻辑 60Hz；速度单位为逻辑 px/s，冷却为战斗 tick。坦克仍使用 16×16px 占地。所有类型使用同一移动/武器能力，不添加刚体或另一套碰撞。

| 类型（EnemyKind） | 定义 ID | 移速 px/s（px/tick） | 弹速 px/s（px/tick） | HP | 击杀分值 | 冷却 tick（秒） | 在场弹上限 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Basic 基础 | `enemy.basic` | 60（1） | 180（3） | 1 | 100 | 15（0.25） | 1 |
| Fast 快速 | `enemy.fast` | 90（1.5） | 180（3） | 1 | 200 | 15（0.25） | 1 |
| FastBullet 快速子弹 | `enemy.power` | 60（1） | 300（5） | 1 | 300 | 15（0.25） | 1 |
| Armor 装甲 | `enemy.armor` | 45（0.75） | 180（3） | 4 | 400 | 15（0.25） | 1 |

设计意图：基础继承 M2 原型的移动/射击节奏；快速型为基础移速的1.5倍；快速子弹型仅提高弹速；装甲型更慢、需要4次普通伤害。100/200/300/400是便于后续分类结算的原型阶梯。不是原版精度声明，也未做人工平衡手感验收。

- 冷却是两次允许开火之间的最短间隔；在场弹数达到1时仍拒绝开火，不承诺实际每0.25秒必发一弹。
- 现有 `EnemyBrain` 请求开火，武器处理限额；本项不新增各类型独立AI方向权重或射击概率。
- `FastBullet` 使用既有美术语义 `enemy.power`，不是玩家升级等级。
- 装甲 HP=4、score=400 是数据；受击颜色/动画、击杀只计一次及计分展示留后续任务。

## 资产与接缝

目录均为 `Assets/BattleCity/Data/`：

- `EnemyBalance.asset`：四类型目录。
- `EnemyBasic.asset`、`EnemyFast.asset`、`EnemyFastBullet.asset`、`EnemyArmor.asset`：共享 `TankDefinition`。
- Sprite 前缀依次为 `tank.white.enemy.basic/fast/power/armor`。每类四方向×两帧通过 `SpriteCatalog` 校验，共32个有效引用；装甲受击色阶并未在本项启用。
- 已有 `EnemyWaveDefinition.tankDefinitions` 可直接引用这些资产，序列仍由 `LevelDefinition.enemySequence` 唯一拥有；本项不添加第二套波次数量或生成计时。
- `EnemyBalanceDefinition.Get(EnemyKind)` 返回显式类型引用，不依赖数组索引或运行时文件名查找；`Validate()` 拒绝缺失类型、未知枚举、重复类型/ID、空引用以及非法共享坦克参数。
- `ValidateSprites(SpriteCatalog)` 用与现有 `EnemyTankView` 相同的语义帧名解析，缺失帧立即报错。

### 与 M2 的兼容边界

`Stage01.unity`、`Stage01.asset`、`Stage01EnemyWave.asset`、`EnemyBasicPrototype.asset` 和现有 Stage01 作者 recipe **不变**。旧定义仍为 `enemy.basic.prototype`、score=0，20个基础敌军原型序列不变。新 Basic 定义是阶段三数据，不替换/重命名旧资产，避免在未实现 BC-301 时改变已验收玩法。

因此此轮在 Stage01 游玩时仍看到原有基础敌军，并非四类型已自然生成；不把数据交付表述为类型行为验收。

## 原版核实与校准

- 所有交付定义 `requiresCalibration=true`；来源字段 `originalValuesSource` 为空，表示没有已核实原版值。
- 未来若将一个目录条目对应的定义标为 `requiresCalibration=false`，该条目必须填写非空 `originalValuesSource`，记录可复查的来源/版本/操作和测量结果；校验器拒绝“标已核实但无来源”。校验器检查元数据完整性，不替代人工确认来源真实性。
- 标记粒度是**整套数值**，不是单字段。只核实部分参数时仍保持 true，在本文件追加参数级证据；不能凭仅分值已核实就把速度/冷却等全部标为已核实。
- 后续调参直接修改显式绑定的资产，不同步另一份运行时常量。修改冻结的碰撞/移动/扫掠规则则须按总计划做相应回归。

## 创建/维护

- 编辑器菜单：`Battle City > Create Missing Enemy Balance Assets (BC-300)`。
- API：`BattleCity.Editor.EnemyBalanceAuthoring.Build()`，必须退出 Play Mode。
- 只创建缺失资产，已有目录只校验不覆盖；不会重建地图、打开/保存场景或改动 Stage01 绑定。新资产与 meta 均由 Unity Editor API 生成。
- 若目录不存在但个别坦克定义已存在，则保留其已调参数并进行校验。已有非法资产不会静默恢复默认值，须明确修正。部分创建失败时保留已生成资产供检查，不删除用户数据。
- 本项无场景/运行行为改动，不要求重复 M2 通关、截图或独立构建；自动证据见 `Verification.md`。
