# BC-106：砖墙局部破坏与显示同步（用户专项验收通过）

## 状态与范围

**BC-106本批验收通过。修正窄通道测试布局、将几何测试迁到PlayMode后，用户反馈“两个测试都正确了”，记录为逻辑21/21、显示22/22专项通过；结合此前场景破墙/钢墙挡弹确认，关闭本任务。结果来源为用户报告，未导入XML/JSON测试报告；全套回归及Console计数不据此视为已通过。助手未代跑Test Runner。**

- 16×4px 破坏条带、4px残存单元；普通子弹不破钢。
- 地图逻辑先改变，Tilemap 只投影；不添加 Rigidbody2D / Collider2D / TilemapCollider2D。
- 残墙继续挡坦克与子弹；最后一个4px单元清空才把8px地形格变为 Empty；原始 LevelDefinition 不变。
- 不重建 CombatArena，不修改原始PNG、导入设置、场景YAML、M0基线、相机或 Build Settings。
- 无破墙爆炸/碎屑特效，属于 BC-107；不代表 M1 或第一关完成。

## 具体规则（原型，非已核实原版）

1. `BulletWorld.Step()` 继续只扫掠和收集最早命中，不修改地图；`ArenaSession` 在该 tick 全部命中确定后调用 `MapState.ApplyBulletHits()`。
2. 仅消费地形/砖墙命中。沿射击方向取命中记录的4px格，深度固定一格；垂直方向宽16px。条带下/左边缘为 `floor((impactCenter - 8 + epsilon) / 4) * 4`，覆盖连续4格，范围为半开；贴地图边缘裁剪，不把条带往内平移。
3. 只清除条带内仍存在的砖，不改变钢、水、草、冰或空地。命中记录重放不会多挖一层。
4. 同一 tick 的所有扫掠读取破坏前快照：两颗弹同时命中同一层，均消耗，但不会因先后处理而钻穿第二层；下一 tick 才看见新缺口。同一渲染帧若有多个逻辑 tick，则每 tick 都立即结算破坏。
5. `MapState.Revision` / 每格 revision 只在实际清掉一个4px砖单元时推进；显示层不清除事件、不反写逻辑，也不依赖最后一个 tick 的命中列表才能看见累计变化。
6. `ArenaMapView` 没有逻辑变化时不写 Tilemap；有变化时只更新 revision 改变的8px格。换新 MapState 时全量重画，保证重开恢复。
7. 完整砖/钢仍使用原 Tile；部分砖按源8px象限及4位掩码缓存运行时 Tile / Sprite。Sprite 使用原纹理、原Rect/中心Pivot/PPU16，几何只保留未毁4px象限，UV自动对应原图；不读取纹理像素、不拉伸残砖、不修改源Sprite或生成永久纹理。最多56个部分Tile和对应Sprite，随显示组件销毁释放。
8. `Sprite.OverrideGeometry()` 输入是 Sprite Rect 像素坐标0..8，**不是** `Sprite.vertices` 的枢轴相对世界单位；测试检查几何面积、象限、UV及不可读纹理兼容。保持现有源图Y向下象限与世界Y向上掩码的区别。

API依据：[Sprite.OverrideGeometry](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Sprite.OverrideGeometry.html)、[Tile](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Tilemaps.Tile.html)。

## 专项测试（共43项，按执行环境分组）

在 **Window → General → Test Runner** 切换相应标签，再搜索并运行对应测试组：

| 测试组 | 模式 | 案例数 | 覆盖 |
| --- | --- | ---: | --- |
| `BrickDestructionTests` | EditMode | 21 | 四向16×4条带、非网格中心对齐、地图边缘裁剪、其他地形不变、逐层清空、残墙扫掠阻挡、16px通路与8px窄缺口、同tick两弹快照、幂等/空命中、源数据与新地图恢复、多tick即时破坏、暂停不重复结算 |
| `BrickMapViewTests` | PlayMode | 22 | 四个源象限×四个攻击方向、四种单4px角残片、几何面积/UV/中心Pivot/PPU、不可读纹理、跨tick累计同步、清空Tile、钢墙不变、换地图恢复、缓存复用、组件销毁释放而不破坏源资产 |

建议同时重跑 `BulletCollisionTests` 38项，确认扫掠、命中排序与敌我过滤没有回归。

当前全部源码预期EditMode 170例、PlayMode 27例（22项几何+5项场景集成）。本批不要求立即跑整套，以实际Test Runner为准，不把预期数写成通过数。

## PlayMode 场景集成（另有5项，未运行）

- 更新 `Arena_BrickHitRemovesFourPixelLayerAndDisplaysRemainingWall`：第一弹仍在y=166碰砖，底部4px消失、上部4px残片可见，子弹回收且源关卡数据不变。
- 新增 `Arena_FourBrickLayersOpenA16PixelTankPassage`：四弹穿透试验场16px深砖墙，打通16px宽通道，逻辑坦克可通过，对应Tile清空，旁边墙体仍在。
- 更新重载用例：每次新session从完整砖墙开始，造成破坏后卸载，下一次加载仍完整；原有输入、暂停、子弹显示/复用测试保留。

可以稍后在 PlayMode 搜索 `BattleCity` 一起运行；若本批不跑，明确留BC-108，不预填通过。

## 手动场景复测

打开 `Assets/BattleCity/Scenes/CombatArena.unity`，进入 Play：

1. 出生点朝上按 Space：观察上方砖墙每次只削掉一层4px，不是一发删掉整块8/16px墙。原始墙深16px，打通该方向需4次有效命中。
2. 缺口宽16px后按W穿过去；旁边砖墙仍应保留。只削一层时尝试前进，应被剩余砖挡住。
3. 换侧面和背面打砖，确认上下左右没有翻转、残砖没有被拉伸或整块误删。
4. 打钢墙，仍挡弹而不损坏。
5. 射击中暂停，地图不继续变化；恢复后按既有规则释放并重按战斗键。
6. 退出再进入 Play，砖墙恢复完整；关卡资产不应留下破坏状态。

## 本批实际检查与恢复点

- 编辑器外 Roslyn C#9 静态编译全部28个BattleCity源文件成功，退出码0；同BC-105有1条既有引用兼容性警告CS1701（System.Collections.Immutable 的 netstandard2.0/2.1 匹配）。见 [Static-compile.log](Static-compile.log)。**不是 Editor 编译、Console清零或测试通过的证明。**
- 新增脚本 `.meta` 由用户回到 Unity 刷新时自动生成，不手写。
- Editor 场景效果：用户已反馈砖块受击效果正确、铁块正常阻挡子弹，目前表现良好。
- 用户专项失败：`EightPixelGap_RemainsImpassableTo16PixelTank`预期清2格但返回0；显示组多次`Not allowed to override geometry on sprite`。未收到全部结果数量，不推测其他用例均通过。
- 编辑器内API对照：Readable/不可读纹理 × FullRect/Tight × None/HideAndDontSave共8组，EditMode全部拒绝覆盖几何，排除仅改可读性/meshType/隐藏标记即可修复的猜测。仅创建/销毁临时对象，未进入Play、未改场景，未代跑Test Runner；对照本身产生预期复现日志，不宣称Console清零。原始结果见[Geometry-EditMode-probe.json](Geometry-EditMode-probe.json)。
- 补丁静态编译成功，仍有同一条CS1701引用兼容性警告；见[Static-compile-fix.log](Static-compile-fix.log)。补丁后的两组Unity专项测试已获用户通过确认。
- 工作区本批开始时，Git HEAD=`0592c1e`（Phase 1 partially implementation），仅`.vsconfig`未跟踪；未改`.vsconfig`，未提交或重置Git。
- 修改前源文件与文档另备份到 `Temp/BC106-before/`；正式回退应对照Git基线定向恢复，勿hard reset或丢弃用户后续修改。

## 专项失败修补

- 窄通道：旧弹中心y=104在砖/钢接缝，首层砖消失后钢墙更早挡弹，不能期待每弹都继续挖2格。新布局把上下两侧设置为钢，弹中心改为缺口内部y=100，16px条带被两侧钢裁成实际8px缺口；保留每次清2格、8px盒可进入、16px坦克不可通过、上下钢边不变的断言，最后朝y=108单独验证钢墙挡弹。未放宽生产碰撞规则。
- 显示：`BrickMapViewTests.cs`及原GUID的`.meta`移到`Tests/PlayMode/`。保留全部22项几何/UV/面积/不可读纹理断言；资源释放测试及清理改用Unity协程等待Destroy/OnDestroy结束，检查组件和缓存生命周期，而不是在同帧误判延迟释放。
- 未修改运行时ArenaMapView、MapState、原图或导入可读性设置；没有屏蔽错误、跳过测试或将预期改成错误结果。

## 验收记录

- 21项逻辑专项（EditMode）：用户确认修补后通过，记录21/21。
- 22项显示专项（PlayMode）：用户确认迁移后通过，记录22/22。
- 两组结果均为用户报告，未导入测试报告；未单独提供Console计数，不宣称0错误/0警告。
- 38项扫掠回归：本批待运行，BC-105历史通过不等于本批通过。
- 另5项PlayMode场景集成：未运行。
- 场景破墙/钢墙：用户已确认砖块受击效果与钢墙挡弹表现良好；通路、四向细节及重开未逐项反馈，不扩大为全部通过。
- BC-106已完成，阶段1为7/9已验收；BC-107、BC-108待实施，M1尚未完成。全套回归仍留BC-108。
