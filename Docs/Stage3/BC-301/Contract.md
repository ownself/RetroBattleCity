# BC-301 实施契约

## 授权与范围

用户在BC-300完成后明确要求继续BC-301。本轮主代理直接实施，不委派，不提交/推送、不开展BC-302～309。基线HEAD仍为 `a8daced9ef7f71708cd642deaf873dd21497eeb5`，保留上一轮BC-300未提交增量及既存 `.vsconfig`。

交付门槛：四类型的实际移动、射击和血量符合配置；装甲受击有可读视觉反馈；死亡和基础积分结算一次；实际Stage01接入及PlayMode推进/重开验证。测试/编辑器受阻时遵守根AGENTS.md及时人工交接，不反复重试。

## 冻结的项目原型规则（均非原版核实）

1. Stage01固定20敌、场上上限4、出生60tick、间隔120tick、种子1不变；将唯一 `LevelDefinition.enemySequence` 改为 `[basic,fast,power,armor]` 循环5次，每类5个。新映射引用BC-300四类数据，不复制参数或另建生成系统。旧 `EnemyBasicPrototype.asset` 保留不动，CombatArena保持无波次。
2. Basic/Fast/FastBullet/Armor数值沿用BC-300，不趁本项调参。现有TankMotor/WeaponState/EnemyBrain共享控制足够实现类型差异，不复制整套逻辑。
3. 装甲HP4。普通敌对弹每发伤害1；4→3→2→1时白→绿→黄→红，下一发死亡并隐藏。反馈使用已有Sprite原色，不用材质染色/光照/墙时计时，不宣称原版调色顺序。
4. 可选 `TankDefinition.damageSpritePrefixes` 按“从满血开始已受伤次数”索引；空数组使用原 `spritePrefix`，否则长度必须等于HP、首项必须等于spritePrefix。显示只读取已结算HP/朝向/履带，暂停和多次Render不推进状态；预先解析全部帧，不在受击时临时加载资源。
5. 击杀分值在TankState出生时快照。ArenaSession仅在敌人存活→死亡时累计Score及DefeatedEnemyCount；受伤、护盾、友军弹、玩家死亡和同tick过量命中不重复记分。即使同tick判负，也先完成本tick击杀结算。重开创建新session归零，暂停不改变分数。
6. 这只是BC-301要求的基础击杀积分；分类结算、奖励、HUD分数、跨关累计/最高分留BC-308/后续。本轮不改UI。
7. 实际资产通过Unity Editor API修改。增加局部升级菜单，只保存Stage01的序列/波次映射和装甲反馈资产，不重建或保存场景、地图、相机和HUD；更新完整Stage01作者recipe以保持可重建。

## 验证计划

- EditMode：四类型移动/弹速/冷却/限额、按配置血量死亡/分值、多弹同tick、出生快照、保护/暂停/友军、玩家不计分、伤害Sprite映射及全部色阶引用。
- PlayMode：装甲四阶段、朝向/履带/暂停/死亡/复用；实际Stage01波次四类型自然出生/显示、真实Update持续推进、击杀100分及重开归零/序列复位。
- 为固定四类型到达的测试可在tick之间清除子弹避免无关基地毁坏，必须披露为受控生成验证；不将其称为自然完整通关。
- 相邻回归：BC300数据/Stage01布局、波次、坦克共享伤害/生命周期及旧敌人显示。仅对源码变化影响的部分验证，不机械重复通关或像素矩阵；结果/失败原件保留。
- 最后核对Console增量、资产变更范围、Editor现场；满足门槛才勾选BC-301。
