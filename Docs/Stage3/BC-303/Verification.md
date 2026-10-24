# BC-303 道具生成与拾取框架：实现及验证

## 当前结论

**BC303已完成验收：169个唯一定向用例最终Passed，全部保护资源已核对一致。** 收尾曾因后台运行设置内存false/磁盘1而发出人工请求；停止自动修复后，独立只读审计观察到磁盘已恢复任务起点SHA256，因此该请求已撤销，无需用户再操作或重复测试。不能区分延迟落盘还是用户操作，不冒称人工回传或第二次自动保存成功。经过见 [Manual-Handoff.md](Manual-Handoff.md) 及ProjectSettings-Restore-Check.json。

用户仅授权本项，依赖BC301已验收；主代理直接实施/源码自查，无subagent或独立review。保留BC300～302成果及既存.vsconfig，未提交推送、不开展BC304～309。范围与未核实原版的项目默认见 [Contract.md](Contract.md)。

## 实现

| 文件（相对Assets/BattleCity） | 作用 |
| --- | --- |
| Scripts/Pickups/PickupDefinition.cs | 六类型、显式carrier成功波次索引/独立seed/来源校验，拾取和四敌人红色Sprite家族校验 |
| Scripts/Pickups/PickupState.cs | 会话级深快照、8px锚点16px足迹静态可达flood、动态目的地排除、独立RNG、单槽替换、不可变物品/收据与幂等账本 |
| Scripts/Pickups/PickupView.cs | 一个复用SpriteRenderer，预解析六图标，只读投影，无Collider/Trigger/自主计时或逻辑回写 |
| Scripts/Tanks/TankState.cs | 携带资格只读公开、仅会话设置 |
| Scripts/Tanks/EnemyTankView.cs | 预缓存同型红色八帧，按逻辑tick/8交替；掉落后回归当前HP阶段，暂停不推进 |
| Scripts/Core/ArenaSession.cs | 可选框架绑定、仅自然生成标记资格；比较HP实减后消费资格，全部伤害/出生/终局判定后有界掉落及活玩家重叠收据；终局/退休清槽 |
| Scripts/Core/ArenaController.cs | 显式引用和启动私有深快照、只读显示绑定、禁用隐藏/重新启用重绑、重复重开/销毁清理 |
| Editor/PickupAuthoring.cs | 只创建缺失配置、不覆盖调参；保存前完整校验；局部Stage01绑定，场景切换后重载配置引用 |
| Editor/Stage01Authoring.cs | 完整重建recipe同步包含配置与Ready会话绑定 |
| Data/Pickups.asset | seed303，索引3/10/17（第4/11/18敌），requiresCalibration=true，六种同权重默认 |
| Scenes/Stage01.unity | Editor API仅新增pickupDefinition引用 |

所有新资产/meta由Unity Editor API/正常导入生成，未手写YAML。没有改地图、混合序列、数量/上限/出生时序、相机、HUD、材质、旧玩家/四敌人资产或CombatArena。局部作者脚本再次执行时全部Data和Stage01 hash不变，见Authoring-Idempotence.json及命令断言记录。

### 冻结的框架规则

- 第一次有效敌对伤害即掉落，可为非致命或致命；友军、保护、重复/过量命中不重复消费。
- 道具为Clock/Grenade/Helmet/Shovel/Star/Tank，六图标已存在；不启用gun。独立xorshift流，不影响波次/AI。
- 最多一个，不自动过期；成功掉落才替换旧物品/增加新ID。无活玩家或无可达空位则消费机会但保留旧物品和RNG，不反复找位置/藏进墙或不可达房间。
- 16px坦克可达足迹，不只检查图标中心；砖残片/钢/水/基地阻挡路径，活坦克和出生预留排除目的地。保守8px锚点搜索，不能宣称找到所有连续空间路径。
- 活玩家严格AABB重叠才拾取，边缘接触不算。先清槽再记一次收据；死亡保留旧道具，保护中的复活玩家可拾取；暂停/失焦/Ready不结算，终局清空且不在终局tick掉落/拾取，重开归零。
- **本项只有生成/显示/一次性收据，不实现任何图标对应效果或拾取分数。** 星星/头盔/加命留BC304，时钟/手雷BC305，铁锹BC306、奖励BC308。截图Tank图标不表示本轮已能加命。

## 测试证据（不是全量或一次全绿）

| 集合 | 唯一最终Passed | 原件 |
| --- | ---: | --- |
| BC303新Edit专项 | 20 | Edit-Result首轮18/19，Edit-Retest仅修正项1/1，Edit-Additional新死亡组合1/1 |
| BC303新Play专项 | 3 | Play-Result首轮2/3，Play-Retest仅修正项1/1 |
| SessionLifecycleIntegrationTests | 10 | 同名-Result.json |
| PlayerLifeIntegrationTests | 15 | 同名-Result.json |
| EnemyTypesTests | 16 | 同名-Result.json |
| EnemyWaveStateTests | 30 | 同名-Result.json |
| TankStateTests | 24 | 同名-Result.json |
| PlayerUpgradeTests | 23 | 同名-Result.json |
| Stage01Tests | 6 | 同名-Result.json |
| EnemyTypesPlayModeTests | 2 | 同名-Result.json |
| EnemyTankViewTests | 12 | EnemyTankViewTests-Progress0首轮4/12；View-Retest-0..4仅8失败项最终Passed，原4通过项未重跑 |
| PlayerUpgradePlayModeTests | 3 | 同名-Result.json |
| PlayerLifeIntegrationPlayModeTests | 2 | 同名-Result.json |
| SessionLifecyclePlayModeTests | 3 | 同名-Result.json |
| 合计 | **169** | 按FullName取最后结果去重，20Edit＋3Play新专项包含在内；Audit.json |

Edit覆盖配置6种错误、源/表快照、六图标/红色家族、三明确索引、正常生成/替换/不可达分区/水钢砖/基地/占位/无候选、分数与AI流不变、严格重叠/一次性收据、自然携带/友军/保护/同tick过量/手工敌人不携带、Ready/暂停/失焦、死亡不拾取/复活保护可拾取、玩家同tick死亡不生成/不重试、最后敌人终局清槽等。

Play覆盖：

1. Stage01成功自然物化第四个装甲敌人，红/白闪烁只读，第一次真实共享武器命中HP4→3/资格消费/生成，立即回归绿色装甲。随后用受控的可达邻位替换道具，由真实键盘A及Controller.Update移动/拾取，重复Render/暂停不重复结算。
2. Stage01禁用隐藏且保留逻辑物品、重新启用显式重绑；修改公开配置引用后连续重开三次仍用启动快照，Ready/种子/计数/资格归零，旧会话清槽且不再推进，只有一个View/Renderer。
3. CombatArena保持未绑定；独立View显示全部六类型原生Sprite ID及正确位置，重复Render不写随机流/收据/活动ID，禁用清理及显式重新绑定。

未重复用户已完成的BC301 RealUpdates单项或试玩。因当前携带者红闪合法变化，将旧EnemyTypesPlayModeTests的白色前缀断言升级为明确的携带/HP初始阶段投影与原生帧ID校验，2/2 Passed；不是删断言。没有构建、性能、完整游玩通关或全分辨率像素矩阵验证。

## 首轮失败与有界修正

- Edit可达性夹具把水格放在(32,32)，与分数坐标玩家中心(24.5,24.25)的16px足迹重叠。算法正确拒绝无效起点；只将水格移远，1/1复测通过，生产可达性未改。
- Play掉落夹具在第602tick击中装甲时，另一敌人同tick击杀玩家；规则正确拒绝掉落。只读模型诊断确认aliveBefore=true/after=false、reachable=true、HP3/attempt1/rejected1。测试增加临时玩家通用保护以隔离无关伤害；仅1项复测通过，未更改生产保护/伤害/拒绝规则。另有Edit真实同tick死亡专项保证拒绝语义。
- 诊断eval首次忘记引入扩展方法命名空间，出现eval代码Compilation Failed（不是项目编译错误）；一次改成显式CardinalDirections.Vector后诊断成功，失败JSON保留。
- 旧EnemyTankViewTests首轮8项托管SameAs Sprite断言失败（预期/实际同名，不据同名直接判通过）。四处Sprite身份断言改为GetEntityId，仍要求同一原生帧及所有位置、暂停、帧、材质、renderer复用/所有权断言；仅8失败项复测Passed，4既有Passed不重复跑。没有用已禁用GetInstanceID，没有修改用户已复测的旧波次测试。
- 收尾磁盘ProjectSettings后台1残留：运行时已false，定向SaveAssetIfDirty尝试一次仍未写回；**停止自动修复并请求人工，不继续换API/循环保存**。后来只读审计实际观察到磁盘hash已恢复，无第二次保存调用；原请求据此关闭，见Manual-Handoff。

## 真实推进、截图与Console

- Play专项日志：`BC303 Stage01 natural carrier hp4->3, drop/replace, keyboard collection=1 frame=15->21 tick=602->603`。真实键盘/Update完成收据；前601tick为受控逐tick到达、清已有子弹＋临时保护，不冒称自然完整游戏流程。
- 另一次真实Stage01运行采样frame12592→13615、tick0→147后暂停（Runtime-ObserveA/B），然后受控到达601，真实共享武器命中自然携带者HP4→3、生成Tank、Score0；Runtime-Carrier/Drop原件保存。
- 原生Screen1523×752截图已查看：[红色携带者](Carrier-Native.png)、[受击绿色装甲＋Tank道具](Pickup-Native.png)。拍摄暂停，无缺失/紫色无效材质/显示残留；不是连续视频。临时保护和子弹清理仅当前会话，不修改资产；新发射子弹仍可在该tick击中地形，因此截图可有受控会话的局部破墙/特效。
- 所有编译结果completed/failed=false，最后Compile-ViewAssertion-Result.json。源码在最终定向通过后未再改动。
- **截至Console-After/Increment记录**：同session316dee6ba1864c7dab134a9af37d8ce3，cursor1226→1503，277log/新增warn0/error0，dropped=false；历史error8/warn1保留。groundTruth编译失败false、当前Console error/warn0/0；未主动清日志。不把后续人工操作或未再次采样的定向保存调用扩展成新的Console检查结论。

## 现场、保护资源与验收

- Editor-Final：Stage01 stopped/not paused/clean，后台运行及PlayerSettings内存false，无临时测试场景；截图临时Assets/BC303-Captures目录及meta已通过Editor API删除。PNG原件按返回savedPath复制至本目录。
- 临时Pipeline默认JSON移至Temp/BC303-EditorPipelineConfig.json保留，没有发出修改AutoTick的指令。测试均已完成，无writer/临时输入设备。
- Audit记录169唯一最终Passed、23项源码/资产/meta hash和保护资源对比；Stage01只新增一个引用，旧Data/CombatArena/其他ProjectSettings不变。后台位经过上述停止/交接后，最终只读观察到磁盘完整SHA256也与任务起点一致，所有保护资源均已恢复，原临时1值副本/diff保留。
- 最终另观察到 `UserSettings/EditorUserSettings.asset` 的Inspector个人偏好/最近场景记录增量（非战斗项目配置，不在保护hash集合），未归因具体用户或Editor操作；保留而不强制覆盖个人设置。ProjectSettings全部hash一致不代表工作区无其他已有/个人增量。
- git diff --check通过（Windows行尾提示），无提交推送。BC303已勾选、阶段三4/10，M3未通过；BC304依赖已满足但等待新授权，不自动开展。没有因收尾重复测试或改源码/用户现场。
