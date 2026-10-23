# BC-107 专项实施与验证记录（主代理已验收）

## 边界与实现约定

- 启动基线：`master@8e91090`，Unity `6000.6.7f1`，CLI `1.0.0-beta.14`，Pipeline `0.9.0-exp.1`，唯一工程 `H:/Projects/Unities/BattleCity`。既存 `.vsconfig`、计划文档与执行看板改动保留；没有提交、推送或暂存。没有勾选 BC-107，也不宣告 M1 通过。
- 经主代理批准的**原型默认值，非原版精确规则**：每条有效 `BulletHit` 播放一个命中特效，small.0→small.1→small.2，每帧4逻辑tick，共12tick（0.2秒）。敌对子弹对碰是一条 hit，播放一个特效，不按双方弹数翻倍。不按位置或tick合并独立命中。
- `Session.Effects.PlayExplosion(position)` 是独立纯表现接口：small.0/1/2→large.0/1，每帧4tick，共20tick（约0.333秒）。Tank/Base 命中不等于死亡，不自动播放大爆炸；未来死亡结算自行调用接口。本次没有死亡、血量、敌人或 BC-206 完整重开实现。
- `ArenaSession.Step` 在扫掠、地图破坏结算后，每tick唯一调用 `Effects.Step(Bullets.Hits)`，先推进已有特效再创建本tick命中特效（初始Age=0）。不会依赖只保留最后tick的渲染读取；渲染不会消费/重放事件，也不会回写地图或子弹。无渲染时仍正常过期，不补播已耗尽时长的效果。
- `CombatEffects` 是纯逻辑tick表现状态池，结束归还并清零租约字段；每次新租约ID递增。它不接收 Map/Weapon/Target，不产生任何碰撞/伤害。
- `CombatEffectView` 只投影状态，以既有 SpriteCatalog 引用及玩家 Unlit sharedMaterial、Effects 排序层显示；只四舍五入显示坐标。播放完隐藏并复用renderer。无Coroutine、Invoke、延时回调、事件订阅或第二时钟。
- 暂停及失焦冻结逻辑tick与特效Age。`ArenaController.ClearEffects()` 同步清逻辑活跃项与显示池，显示立即隐藏、PlayMode对象延迟销毁；幂等。controller禁用/销毁自动调用；重新启用保持暂停且不重播旧Hits，须显式恢复。场景重载创建全新空状态。此接口仅是特效清理seam，不是完整会话重开。

## 素材核对

`RuntimeSetup.json` 由编辑器 AssetDatabase/SpriteCatalog API 实读：small.0/1/2均16×16，large.0/1均32×32，PPU=16，语义ID与Sprite名称一致。源规范见 `Docs/Stage0/SpriteInventory.csv` 第290–294行。没有重新切图、修改原PNG、手写资源YAML或Sprite meta；新增C# meta由Unity生成。`PausedExplosion.png` 是实际Game View截图，核看了中央small.0显示；不以静态截图冒充动画/运行证据。

## 专项与紧邻防回归测试

全部通过现有编辑器 `test run`（Test Runner API），异步启动后读取 `test status`；没有另起batch Editor。精确结果在JSON逐项列表中，可独立复核。

| 模式/筛选 | 通过/总数 | 失败/跳过 | 结果证据 |
|---|---:|---:|---|
| EditMode / CombatEffectsTests（新增） | 9/9 | 0/0 | EditModeResults.json |
| PlayMode / CombatEffectViewTests（新增） | 5/5 | 0/0 | PlayModeResults.json |
| EditMode / BulletCollisionTests（原断言保留） | 38/38 | 0/0 | CollisionResults.json |
| EditMode / BrickDestructionTests（原断言保留） | 21/21 | 0/0 | BrickResults.json |
| PlayMode / BrickMapViewTests（原断言保留） | 22/22 | 0/0 | BrickViewResults.json |
| PlayMode / ArenaPlayModeTests（原断言保留） | 5/5 | 0/0 | ArenaResults.json |
| 合计（非全量工程回归） | **100/100：EditMode 68，PlayMode 32** | **0/0** | 各自Start与Results配对 |

新增覆盖：多逻辑步命中保留（最后tick无Hit仍保留已有效果）、同位置同tick三弹不吞事件、砖/钢命中全帧精确12tick过期、逻辑对象与renderer复用、暂停与失焦、纯爆炸20tick过期、边界与敌弹对碰、坦克命中不冒充死亡、暂停清理后不重播、controller禁用/启用、显式销毁与两轮重载、真实Update自然过期。地图revision、残砖掩码、在场弹计数等原专项断言没有删除或降低。

## 编译与真实运行验证

- `CompileRequest.json` / `CompileResult.json`：编译完成，`failed=false`、`compilationFailed=false`、`errors=[]`。没有额外程序集/依赖或资产改绑。
- 临时开启Pipeline auto tick（16ms）及run-in-background，实际进入原CombatArena，退出时已恢复后台设置false，编辑器保持ready且非Play。auto tick为编辑器会话辅助，不修改游戏时钟；未要求关闭主代理原有的辅助推进。
- `RuntimeSample1.json`：frame=8501、tick=180、active=1；`RuntimeSample2.json`：frame=9330、tick=247、active=0、visible=0。证明渲染帧和逻辑tick均实际推进并自然回收，非只进入Play或静态截图。
- `RuntimePaused1.json`：frame=9726、tick=247、age=0；`RuntimePaused2.json`：frame=11201、tick=247、age=0、sprite=small.0。证明暂停时渲染仍推进，特效冻结。
- `RuntimeClear.json`：active=0、visible=0。PlayMode测试另证明清理后renderer被销毁、旧session无活跃项/回调，重载新session为空。
- `ConsoleAfterCompile.json` 基线cursor=200/session=`6c7f1251baea4fc49745630725f772d1`，历史buffer error=71（不是本轮新增），当前Console groundTruth error/warn=0。`ConsoleNewErrors.json` 按该cursor/session读取至273，error级 entries=[]、returned=0、dropped=false；`ConsoleFinalRuntime.json` 当前Console error=0、warning=0、compiling=false、compilationFailed=false。没有清Console或隐去旧故障。新增PlayMode测试也全部 `LogAssert.NoUnexpectedReceived()`。

## 已发生的工具事件与清理

- 一次只读script eval发生 `COMMAND_FAILED: Network error: socket hang up`，时间在recompile/domain reload期间。向主代理报告并获准一次同协议恢复，`ReconnectStatus.json` ready后检查编译/Console/测试状态；无正在执行测试，未重启编辑器、未重复recompile、未切换工具或执行方式。此命令本身失败，不列作通过；后续全部验证成功。记录见 `ConnectionIncident.txt`。
- 当前Pipeline命令是分组形式（`script eval`、`editor.playmode start`、`editor set_auto_tick`等），与skill示例的旧扁平命名不同；由实际catalog/schema发现后调用。query为子串匹配，初次正则式查询返回0项，不是连接失败。原始group catalog与实际使用schema已保存。
- capture命令把传入相对路径规范到`Assets/Docs/...`，返回savedPath明确。已复制本轮生成截图到本目录，再通过AssetDatabase API删除仅该次创建的`Assets/Docs`临时树，未删除用户文件。证据`CaptureResult.json`、`RuntimeClear.json`。
- 主代理批准清理两处本轮生成噪声：HierarchyWindow Name列宽仅442→443复原；`ProjectSettings/Packages/com.unity.pipeline/EditorPipelineConfig.json`确认为启动不存在、本轮后台开关命令生成且已恢复false，保存其内容/来源到`GeneratedConfigCleanup.txt`后仅删除该文件，不删父目录。其余用户文件未触碰。

## 用户初步人工反馈

用户已在 PlayMode 初步测试爆炸特效，反馈观感不错、显示正常；准确记录为：**用户初步人工确认爆炸表现正常**。此反馈仅覆盖爆炸表现，不扩展为暂停、回收、重开或全套测试均经人工通过。用户愿在需要时配合操作；本轮连接已恢复，无需追加用户操作，也未自行重启编辑器。

## 未运行与剩余限制

- 没有运行全部EditMode/PlayMode、30/60/144fps矩阵、人工贴墙转向/手感、Windows构建或长期性能测试；归BC-108/后续门禁，不以100项紧邻专项替代全量回归。
- 时长/帧序为已批准原型默认值，仍待校准。截图仅确认small.0实际显示，整段动画各帧由自动测试检查；用户另初步确认爆炸表现正常，但不代表完整人工视觉/手感验收。
- 未实现真实敌人/基地死亡，完整重开/地图/计数器会话重置不在范围；本次仅特效接口与重载清理。
- 首次编译域重载曾触发一次连接断开，后续恢复且无复发，已完整披露。
- 验收须主代理独立审查：建议重点看每tick消费唯一性、生命周期幂等清理、复用/暂停断言及证据计数，之后再决定是否进入BC-108。

## 主代理验收结论

实现交接及独立只读审查已完成（workflow `5620a178-d212-470b-b08f-431c8467dfd5`，实现子任务 `983558eb-5deb-41b3-9e0b-5df8c5f6c56a`，审查子任务 `029a994a-b094-4c93-ad85-ef3336e21883`）。审查未发现有证据的 P0/P1/P2 缺陷，结论 OK with notes；主代理另核查特效逻辑/显示源码、专项测试及保存的测试结果，接受 BC-107。用户初步视觉反馈仍仅限爆炸表现。此前“待验收”字样描述实施时状态，以本节为最终裁定。

BC-108 全量回归、帧率矩阵及当前版本人工贴墙转向/手感尚待完成，M1 未通过。不将完整死亡/会话重开列为本次已交付功能。

## 文件与Git

实施C#：`Scripts/Combat/CombatEffects.cs`、`CombatEffectView.cs`、`Scripts/Core/ArenaSession.cs`、`ArenaController.cs`；新增测试：`Tests/EditMode/CombatEffectsTests.cs`、`Tests/PlayMode/CombatEffectViewTests.cs`（均位于Assets/BattleCity/）。新增4份C# meta由Unity生成。其它仅本专项目录证据。`BulletHit.cs`/`BulletWorld.cs`无需修改，保留既有扫掠、伤害及破墙边界。

`GitStatus.txt` 保存最终工作区与空暂存区检查；计划文档及未跟踪执行看板/`.vsconfig`仍为启动既存状态。不提交、不推送；`git diff --check`通过（仅Git配置的LF→CRLF提示）。
