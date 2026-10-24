# BC-302 玩家等级与武器升级：实现及验收

## 结论与范围

**BC-302已完成本项验收。** 新23项EditMode＋3项PlayMode最终Passed，加上相邻回归共 **188个唯一最终Passed**。不是全量回归或一次性188项全绿。最终编译通过、Console同session增量无warn/error，真实Update推进及原生GameView两张图已核对。

主代理直接实施/源码自查，无subagent或独立review，不冒称独立审查。HEAD `a8daced9ef7f71708cd642deaf873dd21497eeb5`，保留BC300/301未提交成果及既存 `.vsconfig`，没有提交/推送。用户新授权允许BC302独立推进（直接依赖BC300）；**BC301人工补验不隐式关闭，不开展BC303/304**。

规则全文见 [Contract.md](Contract.md)，原始结果/校验与27项实现/资产/meta SHA256见 [Audit.json](Audit.json)。

## 实现清单

| 文件/资产 | 职责 |
| --- | --- |
| Scripts/Tanks/PlayerUpgradeDefinition.cs | 明确0..3四级，引用共享TankDefinition；验证唯一ID、基础匹配、移动/HP/分值不因升级改变、32帧完整性 |
| Scripts/Tanks/PlayerUpgradeState.cs | 会话拥有的深数值/前缀快照；有界升级、死亡归零，不持有可变资产 |
| Scripts/Tanks/TankDefinition.cs | 新增canDestroySteel，旧定义默认false |
| Scripts/Combat/WeaponState.cs | 只读WeaponParameters值快照；切换配置不重建武器、不取消租约、不清现有冷却 |
| Scripts/Combat/BulletWorld.cs、BulletHit.cs | 子弹及不可变命中携带发射时破钢能力；池复用清标记，旧弹不随升级/死亡改变 |
| Scripts/Map/MapState.cs | 最高级命中后删除接触的一个8px钢格/更新Revision；不穿透、同tick预破坏快照、重复幂等；砖墙路径不改 |
| Scripts/Core/ArenaSession.cs | 可选升级表构造参数；TryUpgradePlayer入口与阶段/生死/暂停门禁；死亡当tick归零 |
| Scripts/Core/ArenaController.cs | 可选显式绑定、四级Sprite缓存、启动配置深快照和重开复位；只读显示当前等级 |
| Editor/PlayerUpgradeAuthoring.cs | 只创建缺失的三个升级TankDefinition及表，已有调参/旧玩家不覆盖；局部绑定菜单 |
| Editor/Stage01Authoring.cs | 完整重建recipe包含升级表绑定，不另建一份规则 |
| Data/PlayerLevel1/2/3.asset、PlayerUpgrades.asset | 四级表复用旧PlayerPrototype作为0级，三个新定义均requiresCalibration=true |
| Scenes/Stage01.unity | Editor API只新增一个playerUpgradeDefinition引用，其他序列化内容完全不变 |

所有Assets路径均相对 `Assets/BattleCity/`；新资产/meta通过Unity Editor API创建，未手写YAML。

默认表：0级180px/s/1弹/不破钢，1级300/1/不破钢，2级300/2/不破钢，3级300/2/破钢。移速60、HP1、冷却15tick/60Hz、分值0不变。死亡立即归0；旧弹保留原属性直到正常回收，复活按M2清玩家旧弹/冷却；重开新session归0并恢复原地图。

**所有规则是待校准项目默认，未经原版核实**。单次破钢范围为接触的一个8×8钢格，不是16px开口保证、不穿透、不附赠AOE。游戏暂无升级调试热键；正常星星生成/拾取和调用入口留BC303/304，本项通过接口及受控测试验证升级，不声称星星道具已可玩。

## 测试原件

| 集合 | 唯一最终Passed | 证据 |
| --- | ---: | --- |
| BC302 Edit专项 | 23 | Edit-Result.json首轮22/23，Edit-Retest.json仅失败项1/1 |
| BC302 Play专项 | 3 | Play-Result.json |
| WeaponTests | 15 | WeaponTests-Result.json |
| BulletCollisionTests | 38 | BulletCollisionTests-Result.json |
| BrickDestructionTests | 33 | BrickDestructionTests-Result.json |
| TankStateTests | 24 | TankStateTests-Result.json |
| PlayerLifeIntegrationTests | 15 | PlayerLifeIntegrationTests-Result.json |
| SessionLifecycleIntegrationTests | 10 | SessionLifecycleIntegrationTests-Result.json |
| Stage01Tests | 6 | Stage01Tests-Result.json |
| EnemyTypesTests | 16 | EnemyTypesTests-Result.json |
| PlayerLifeIntegrationPlayModeTests | 2 | PlayerLifeIntegrationPlayModeTests-Result.json |
| SessionLifecyclePlayModeTests | 3 | SessionLifecyclePlayModeTests-Result.json |
| 总计 | **188** | Audit.json按FullName去重、最后结果合并 |

Edit专项实际验证：各级移动/弹速/冷却/在场租约限制；升级保留冷却与旧弹、第二租约新参数；上限拒绝；数值/Sprite深快照及敌人不受影响；死亡即时归0/复活/保护；未绑定/暂停/失焦/Ready/终局拒绝；四向钢墙只最高级破坏、相邻格/重复/同tick多弹/池复用；8类坏表、基础定义错配、32帧与缺帧提前报错。

Play专项实际验证：

1. **真实Stage01** 四级左右朝向/履带两帧原生Sprite身份，暂停拒绝升级且不推进，随后由真实Controller.Update推进。日志：
   `BC302 Stage01 level=3 real Update frame=31->101 tick=64->74 sprite=tank.yellow.player.level3.left.0`
2. **真实Stage01** 最高级弹经共享BulletWorld命中钢墙，逻辑格Empty且Tilemap投影消失；重开Ready/0级/原钢墙/普通武器/空弹池；改动运行中公开字段不影响私有启动快照。弹发射位置由测试放到真实钢格外侧，是受控集成验证，不是玩家寻路抵达该处。
3. **真实CombatArena** 未绑定升级表，拒绝升级，原180/单弹/不破钢及0级Sprite保持不变。

原BC301受阻波次用例没有运行；上述会话Play3是因本轮Controller快照改动执行的相邻回归，不冒称关闭BC301旧Sprite断言或其人工画面门禁。没有重复通关、独立构建、性能或像素矩阵验收。

## 失败及有界修正（保留原件）

- **作者脚本首次失败** `Authoring.json`：Missing player upgrade level。新数据已正确创建，但OpenScene会卸载只被本地托管引用持有的等级资产；绑定代码改为打开场景后重新Load表。一次修正后 `Authoring-Final.json`成功，未覆盖既有调参。第一次shell没有因作者调用失败停止，仍启动Edit专项；模型/已创建数据测试完成，不将失败作者调用算成功。随后命令都以set-e＋结果failed=false门禁检查。
- **专项首轮22/23**：3级四向钢墙测试把“相邻保留钢格”放在从右射击的入射路径，子弹正确先打邻格，而夹具断言期待中心格消失。只把邻格移到对角，生产碰撞未改；仅失败的`(3)`参数项复测1/1 Passed。首轮失败原件保留，不冒称首轮23全绿。
- **截图路径/缩放**：Pipeline把请求的Docs路径强制前缀Assets；实际PNG从返回savedPath复制回Docs。首组强制512×480压缩了实际1523×752画面，不用作宽高比/像素证明；只为补清晰画面再次短暂进入Play，按原生Screen尺寸捕获两图，未重跑已通过测试或改相机/GameView。旧图也保留并明确局限。

以上均为明确原因的一次有界纠正，本轮无持续受阻或待人工验证的BC302步骤。

## 实际运行与截图

当前Editor未暴露wait_for命令（查询为空），使用两个独立eval样本记录帧和逻辑tick，不将Play状态或截图本身当作推进证据。

- 首次运行：`Runtime-ObserveA/B.json`，frame1454→2675、tick0→174、phase Playing。随后暂停拍0级，短暂解除暂停调用API升级3次，再暂停拍3级；Runtime-Upgrade显示300/2/破钢true。
- 原生补图：`Native-ObserveA/B.json`，frame1540→2720、tick0→164；尺寸1523×752。捕获前已证明该次游戏真实推进。
- [0级原生GameView](Player-Level0-Native.png) / [3级原生GameView](Player-Level3-Native.png)：主代理查看，玩家Sprite形态正确变化，无紫色/缺帧/显示残留。拍摄时刻为暂停，非持续帧视频或完整通关。
- 没有改渲染设置、PPU、摄像机、HUD或Sprite纹理；不扩展为新的全分辨率像素回归。

## 编译、Console、资源边界与现场

- `Compile-Result.json`和`Compile-Fix-Result.json`均completed/failed=false，无编译错误；修正仅作者/夹具，不改已通过的运行时逻辑。
- Console同session `316dee6ba1864c7dab134a9af37d8ce3`，cursor986→1198共212条log，新增warn/error=0；累计历史error8/warn1前后不变，不声称全历史无错。最终groundTruth编译失败false、Console error/warn=0/0；见Console-Before/After/Increment.json，dropped=false。
- Audit按任务起点hash确认旧PlayerPrototype、Stage01地图/混合波次、CombatArena场景、ProjectSettings全部不变。Stage01场景相比本轮起点只有一个升级引用新增；旧BC301非本轮改动保留。
- 自动测试均完成，无后台writer/临时输入设备。截图临时Assets/Docs及meta通过Editor API删除；Docs原件保留。临时Pipeline默认JSON移至Temp/BC302-EditorPipelineConfig-Removed.json留档。
- `Editor-Final.json`：Stage01 stopped/not paused/clean，Application与PlayerSettings后台false；AutoTick true/16ms与运行前一致，命令使用persist=false没有改SessionState偏好。
- git diff --check通过（仅Windows行尾提示）。无提交/推送、无BC303实现。阶段三BC300＋BC302共2/10，M3未通过，BC301仍待人工。
