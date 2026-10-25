# BC-307～309 / M3 验收

## 结论

**BC-307、BC-308、BC-309通过本项验收；阶段三必选10/10，M3通过。**

2026-10-25，Unity 6000.6.7f1，CLI 1.0.0-beta.14（已检查为最新版）。起点HEAD `cc948e2b96d04b82bbf341f409870f3165e01611`；主代理直接实施、实际执行Unity测试并自审源码/原始结果，没有本轮独立subagent审查或用户人工反馈。所有数值是待校准项目默认，不是逐帧原版复刻声明。

不进入BC400～407、不提交/推送。新增主界面样式和01～35关参考图不等于完成标题流程或35关可玩数据。

## 交付

### BC-307 特殊地形

- Water挡玩家/敌人、出生占位及道具通达性，不挡弹；Grass/Ice对坦克与弹通行，弹不破坏这三种地形。
- Ice按坦克中心所在8px格采样；主动成功移动刷新最多24tick松键滑行，沿最近成功方向、保持原移速。主动转向立即使用旧网格对齐与扫掠，离冰/碰撞/边界停止；暂停/冻结不耗时间，死亡/复活/终局/退休清动量。玩家与敌人共用TankMotor，纯Preview不写状态。
- 既有16px语义Sprite派生8px四象限Tile。水/冰下层、草Actors/order1，草遮坦克/基地，不盖住Actors/order2道具及更高层子弹、盾、爆炸、出生提示和HUD。Tilemap仍无独立Collider，MapState是唯一碰撞真相；revision驱动替换/清格/重开，无每格GameObject。
- 不改Stage01关卡布局来造演示地形，测试仅修改运行时副本。水显示使用已有water.0静态帧，未另加水波动画验收项。

### BC-308 计分与结算数据

- `Scripts/Core/ScoreState.cs`：按出生时DefinitionId及KillScore快照分类累计基础击杀分，四类型/装甲过量弹伤/同tick弹伤+手雷共用单次死亡结算。旧Score/DefeatedEnemyCount为只读投影。
- `Pickups.asset.collectionScore=500`：六种新拾取收据各奖励一次，配置非负且按会话快照；满级/生命奖励上限/无可选能力仍消费并奖励；无终局拾取奖，不把掉落/渲染/重放LastCollected当奖励。手雷为拾取奖500+实际敌方击杀分。
- `StageResult`在Won/Lost清理后、事件前一次冻结stage、结果/失败原因、结束tick、生命、分类击杀/道具明细、分项/总分。集合与行深只读，不引用活动会话或Unity资产；Ready/Playing/暂停及中途退休不伪造结果。
- 重开/未来新关均为新Session，局内计分、收据、Clock/Helmet/Shovel及冰动量不继承，旧结果保持可读。跨关总分/生命如需保留，将由后续流程显式消费结果；本批无切关UI、通关额外奖或加分换命。
- BC303/304/305旧Edit测试显式使用collectionScore=0隔离旧框架/效果/击杀口径；新专项与实际Stage01 Play测试核查默认500及完整总分，不通过禁用真实场景奖励来躲避回归。

### BC-309 组合回归

新专项覆盖盾+伤害/死亡/复活，钟+暂停/冰动量及玩家继续运动，雷+最后敌/基础分/奖励/一次结果及失败优先，铁锹+到期/残砖mask/占位/暂停，满级奖励+冰+破钢，真实玩家/敌人惯性终局清理，分类结果/参数快照/重开隔离。既有完整Edit及相邻Play测试共同覆盖其他边界。

## 实际测试证据

逐FullName/Mode去重最终 **674唯一Passed：656 EditMode + 18 PlayMode**。新增专项 **42：39 Edit + 3 Play**。全部最终项均有逐项原始JSON，没有借用上轮结果或仅汇总级通过项。完整去重台账：`Test-Audit.json`。

| 执行 | 结果/证据 |
| --- | --- |
| 新BC307 Edit | 首次19/20；单项夹具修正后通过，后续广域Edit也覆盖通过；BC307-Edit-Request.json、BC307-Fixture-Final.json |
| 新BC309组合/计分 Edit | 首17项全过，后加终局惯性2项首次1过1失败，修正锚点夹具后剩余1项通过；BC309-Edit-Request.json、Terminal-Ice-Edit.json、Terminal-Ice-Fixture-Final.json |
| 广域Edit | **单次653/654**，旧第一关参考硬编码路径修正后1/1；All-Edit-Result.json、Moved-Reference-Final.json。另加2项后656最终通过，不声称一次656全绿 |
| 新经典规则Play | 首次2/3，受控终局弹起点夹具修正后1/1；BC309-Play-Status.json、Terminal-Fix-Result.json |
| 既有道具Play | 首8/9，BC303总分断言修正后1/1；Pickup-Regression-Result.json、Pickup-Fix-Result.json |
| 既有升级/敌型/Update Play | 升级3/3；敌型首1/2、Sprite原生实体身份断言修正后1/1；真实Update暂停恢复及键盘射击钢墙1/1。见Upgrade/Enemy/Enemy-Identity-Fix/Render-Regression-Result.json |

### 保留的6次失败与有界纠正

1. 冰面离格测试从(100,100)请求横向，实际先执行合法转向对齐；改为对齐的(100,104)冰格，不改生产移动逻辑。
2. Stage01终局测试的受控子弹起点落在生效铁锹屋顶钢墙，正确被钢吞掉，未击中基地；改为在基地足迹内建立受控致命命中，不宣称自然战斗失败流程。
3. 广域Phase0 Importer测试仍硬编码用户已移动的Stage01旧路径；改为共用Stage01Authoring.ReferencePath，不改Importer/原始图片。
4. BC303旧测试适配奖励时总分固定500，漏算确定性手雷实际击杀分（观测1500）；改为精确检查PickupPoints=500与Total=EnemyPoints+500，不删除/忽略敌人分。
5. 新终局惯性测试先把玩家移到非8px锚点x25，再强制唯一道具目的地，合法生成算法拒绝；改为在原锚点先掉落再移动，真实收据仍只结算一次。
6. 旧装甲显示测试`SameAs`比较Unity Sprite的不同托管wrapper失败，原生Sprite相同；改用本工程已使用的`GetEntityId()`精确原生实体比较，不放宽为仅名称一致。

每项仅作明确低风险修正并定向复测，没有机械重复成功项。首次失败JSON均保留；不是“首轮全绿”。初始命令使用旧console_status名称、同步Play请求被工具明确拒绝及编译域重载瞬时断连请求也保留；按命令目录/异步请求/ready连接纠正，不计为测试通过或编译失败。早期recompile_status返回compiling缓存，本轮用最终Editor.compiling=false、实际新类型加载及执行用例确认编译成功，未持续轮询该旧状态。

### 真实推进与显示

`Console-All-Delta.json`记录：

- 新冰面：frame20→226、tick0→29；tick3暂停冻结位置/动量，松键后24px滑行并停止，实际键盘Input System/Controller.Update驱动。
- 新终局：frame13→17、tick4→5；实际Update结算，结果2000、一次通知，重开零分/无结果且旧结果仍2000。
- 四象限纹理rect/PPU/pivot、真实Tilemap分层与Collider.None、替换清格和重开三层不重复，逐项Play断言通过。
- 相邻Play：Clock/Grenade真实Update最终1400（400击杀+1000两收据）；三奖励/HUD；铁锹tick1201恢复mask5（中段公开AdvanceFrame加速）；四种自然敌、最高级Sprite/武器及真实Update暂停恢复/键盘射击钢墙。广域Edit另含30/60/144渲染delta的移动、武器、AI、破坏/特效一致性测试，不把它冒称真实屏幕帧率锁定测试。

无新增截图、用户试玩、自然通关/多关连玩、独立构建或全量PlayMode回归；不能用“674”冒称全模式一次回归。旧已通过的用户操作未要求重复。

## 资源与现场保护

- `Resource-Inventory.json`：35张208×208关卡样式图及一张256×439 Miscellaneous标题/菜单/补充图形；Stage01移动前后PNG SHA256一致，代码与旧Importer测试引用改到Stages目录。PNG/meta不覆盖，不自动修改新增图Importer，不把样式图当碰撞数据。
- `Integrity-Audit.json`：起点383既有文件中**366相同、17预期源码/测试/数据改变**；新4个C#及其Editor生成meta共8项。所有原始Sprites及meta、已有场景、ProjectSettings、Stage01地图/波次/相机/HUD资产均与本轮起点一致；Pickups.asset仅增加collectionScore500。用户移动/.vsconfig保留，无git staging/commit/push。
- `git diff --check`通过。Git LF/CRLF提示不是Unity warning。
- Console游标2048→2192，**144条新增全log，0 warn/error**，未发现新增C#编译或runtime错误；NUnit失败仍保留在上述JSON，不用Console安静掩盖测试失败。
- `Editor-Final.json`、`Scene-Final.json`：Editor ready、stopped、not paused、not compiling、runInBackground=false；Stage01已加载且clean。测试键盘/附加场景经TearDown释放，无留存观察器或运行测试；主代理未手动清Console、强关/重启Editor或改动用户设置。

## 后续边界

下一阶段为BC400主界面/完整单人流程，素材已就绪，但须用户后续授权。本轮不勾选BC400～402，不把35关参考图的存在当作35关交互数据验收。
