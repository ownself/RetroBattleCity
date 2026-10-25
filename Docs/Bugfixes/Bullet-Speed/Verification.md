# 弹速调校与三颗星星的升级层次

## 请求与基线

用户试玩认为玩家初始/多数敌人的子弹过快，接近一星后的感觉；希望明确一星加速、二星允许第一发未消失时发第二发、三星破钢，并保留敌方天生快弹的坦克。

本轮基线 `7b93ef8`（前几轮补丁已被用户提交），起点只有既存未跟踪 `.vsconfig`，保留不动。Unity 6000.6.7f1；CLI 1.0.0-beta.14版本检查最新，Editor端口7800 ready。开始/结束均为SinglePlayer场景非Play、非暂停、clean，runInBackground=false。

## 核对与变更

原有 `PlayerUpgradeState` / `WeaponState` 已支持用户描述的顺序，不是缺少升级逻辑：

- 初始与一星上限均为1发；二星、三星上限为2发。
- 所有等级保留15tick冷却。二星需等冷却结束后才可发第二发，不必等第一发碰撞/消失，不是同帧齐射或自动双发burst。
- 只有三星新发射的子弹能破钢；升级不会改写已飞出子弹的属性、重置正在进行的冷却或取消子弹租约。

本次改变的是速度层次：

| 使用者/等级 | 旧弹速 px/s | 新弹速 px/s | 每60Hz逻辑tick位移 | 在场上限 | 破钢 |
| --- | --- | --- | --- | --- | --- |
| 玩家初始 | 180 | 120 | 2px | 1 | 否 |
| 玩家一星 | 300 | 240 | 4px | 1 | 否 |
| 玩家二星 | 300 | 240 | 4px | 2 | 否 |
| 玩家三星 | 300 | 240 | 4px | 2 | 是 |
| Basic/Fast/Armor及旧BasicPrototype敌人 | 180 | 120 | 2px | 1 | 否 |
| FastBullet（Power）敌人 | 300 | 240 | 4px | 1 | 否 |

全部冷却仍为15tick（0.25秒）。Fast敌人保持移动快但普通弹，Power保持快弹；没有改坦克移动速度、生命/分数、AI、波次/道具随机流、子弹扫掠、砖/钢破坏、输入或计时机制。

以上为用户反馈驱动的**项目手感调校值**，不是已逐帧校准的FC原版数据；`requiresCalibration=true` 保留，不标记为原版已验证。网页检索仅辅助对照升级顺序，未取得可核对的原版逐帧弹速证据，不以其他复刻源码/搜索摘要充当原版数值证据。

### 修改范围

- `Scripts/Tanks/TankDefinition.cs`：新定义的普通弹默认120。
- `Editor/EnemyBalanceAuthoring.cs` / `PlayerUpgradeAuthoring.cs`：缺失资产生成配方使用120/240；仍不自动覆盖已有调校资产。
- 经一次受保护的Editor API调用更新9个TankDefinition资产。先核对全部旧值、类型及无未保存修改，再Undo.RecordObject/SetDirty/SaveAssetIfDirty；没有手写资产YAML。`Asset-Update.json`保存调用和读回。
- Unity保存旧资产时补齐原已存在的默认false `canDestroySteel`、空 `damageSpritePrefixes` 字段，语义不变。Audit核查资产文本差异仅速度及这两种默认值序列化补齐。
- 未改任何场景、Prefab、地图、素材、Sprite/meta、ProjectSettings或前几轮补丁；未提交/推送。

## 自动验证

### EditMode

默认TankDefinition影响所有逻辑夹具，运行一次全EditMode：757项中744 Passed、13失败。13项均为三组旧弹速绑定断言：

- BaseStateTests四方向×两阵营共8项，12px飞行距离旧值4tick，新值6tick。
- TankStateTests双阵营共2项，8tick弹位置59→51。
- RenderDeltaCombatTests三种模拟帧率共3项，改为新2px/tick的确定位置与边界回收时刻；第二检查点改90tick，保留复用、年龄、冷却、爆炸位置等断言。

对这三组各做一次有界断言同步并仅定向重跑13项，全部通过；原始结果 `Edit-FirstRun.json` 与三个 `*-Retest.json` 均保留，不重复已通过744项。最终并集 **757唯一EditMode Passed**，不是首轮757全绿。

新增4个参数化用例使用真实PlayerUpgrades资产验证0～3级的新弹速、2/4px实际单tick位移、14tick时不再射、15tick后第一发仍在飞时按1/2上限射击、拒绝第三发及钢破坏属性；既有用例继续覆盖飞行子弹快照、冷却不重置、死亡降级、钢墙与池复用。WeaponTests同步真实位移和30/60/144模拟render delta期望；敌人及玩家已发布配置断言同步新值。

### PlayMode

| 定向套件 | 最终唯一结果 |
| --- | --- |
| PlayerUpgradePlayModeTests | 4/4 |
| EnemyTypesPlayModeTests | 2/2 |
| ArenaPlayModeTests | 5/5 |
| PlayerPickupPlayModeTests | 2/2 |
| 合计 | 13唯一PlayMode Passed |

- 新增Stage01三颗Star实际收集专项（有界搜索Star、控制清空两列射击通道、通过Session逻辑tick推进），逐级核对初始2px/tick→一星4px/tick→二星15tick冷却后两发同时在场→三星破钢标志与正确坦克Sprite。不是自然掉落统计或用户人工试玩。
- 既有Stage01实际钢墙/Tilemap移除与重开恢复普通弹、升级Sprite各方向履带、真实Controller.Update推进验证通过。
- 既有自然混合波次确认四种敌人真实运行绑定，新增Only Power=240/其他=120的Weapon快照断言。
- Arena真实键盘/Update发射新增120px/s及单tick2px位移断言，继续覆盖显示复用、暂停、砖层破坏/地形及钢墙阻挡。
- PlayerPickup实际键盘移动拾取Star/Helmet/Tank，含升级、护盾、生命/HUD/分数及重开清理通过。

EnemyTypes首轮2项中Armor通过、自然四类型用例失败在“12tick端点必须恰好处于15tick冷却”的时序断言，不在新增弹速断言。一轮有界修正：在既有3秒deadline内观察实际射击发生且真实推进至少12tick，不把任意端点cooldown当作开火证据；沿用出生阶段的帧间清除子弹以隔离无关终局。只重跑该失败用例通过，Armor不重复。原件 `EnemyTypes-FirstRun.json` 与1项定向 `EnemyTypes-Retest.json` 保留；不冒称首轮Play全绿。

### 汇总与现场

最终结果并集 **770唯一Passed = 757 EditMode + 13定向PlayMode**；不是单轮770全绿或全Play回归。14项初始失败均已各一次有界修正/复测，原结果保留；精确去重与21项源码/资产SHA256见 `Audit.json`。

最终编译成功，见 `Compile.json`。Console cursor2692→2794新增102条全为log，无新增warn/error；groundTruth错误0/警告0、compilationFailed=false，历史缓冲错误未清，见 `Console*.json`。

`Before.json` / `After.json`记录9份资产读回及最终编辑器状态。无遗留临时输入设备、观察器或设置；没有构建、GPU视频对比或用户人工手感通过声明。

## 待用户确认

请直接试玩初始枪与一星后对比：普通弹应明显更慢，一星弹应明显更快；二星可按住Space观察两发间隔在场，三星可打掉铁块。功能自动验证已通过，120/240是否符合用户记忆中的手感仍待用户反馈，不强制重做已有自动回归。
