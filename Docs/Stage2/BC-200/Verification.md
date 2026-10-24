# BC-200 第一关可交互地图专项验证

## 当前状态：BC-200已验收，贴墙破砖Bug已修复；按用户要求暂停

主代理已读取实现交接与最终只读审查，检查BulletWorld最小diff、地图磁盘测试及六组原始结果，接受本轮117/117定向验证。最终审查为**OK with notes，无具体缺陷**，原资产尾i的P1/BLOCK判定撤销。主代理另独立重算当前Stage01.asset的SHA256，确认与记录一致：`741813b2d9ac38e857d8494c71b4959bf8af9dd1dfe081afdcb167a55f43bbbf`，补齐reviewer未独立重算hash的备注。源码/测试定向空白检查通过；无关UserSettings噪声未清理。

结合已核实第一关布局、原全量226/226、用户对Stage01 PlayMode的正面反馈、本轮Bug失败→通过证据与定向审查，**BC-200验收通过，阶段二1/10，M2尚未通过**。贴墙修复版尚未收到新的人工复测反馈，不伪称用户已复测。新入口仍为`Assets/BattleCity/Scenes/Stage01.unity`。

所有子任务已结束，不再自动重试/审查，不启动BC-201；等待用户明确恢复指令。最终审查持久副本见`FinalReview.md`。当前Git HEAD为`b5e2460`（与启动基线不同，用户期间新增的提交不改写）；本轮代理无提交/推送/暂存，所有现有改动保留。下文为各阶段证据与过程记录，其中“待审查/交主代理”等措辞为当时状态，以本节最终裁定为准。

### 资产格式纠正与专项证据

主代理新证据已只读确认：已提交基线`git show HEAD:Assets/BattleCity/Data/CombatArena.asset`的terrain同为5409字符、尾`i`（`ContactFix/Baseline-CombatArena.txt`和`Suffix-Correction.json`）。结合上一阶段全新SO经Unity CreateAsset生成同样尾`i`，此前“5408纯hex才合法”属于错误假设，**撤销据此提出的资产BLOCKED结论**。不得删除Unity自身生成的后缀。旧失败及备份全部保留，格式结论交最终reviewer独立复核。

仅纠正本轮新增Stage01Tests磁盘校验：明确校验5408hex加单个`i`，拒绝其他后缀/长度；仍逐格解码676个合法枚举，与独立26×26矩阵比对，通过Unity API强制同步导入/加载并核对676格。未改asset/meta，未扩大资产调查。

- `ContactFix/StageTest-Results.json`：Stage01Tests **6/6通过**；`StageTest-CompileStatus.json`：completed、failed=false、errors=[]。
- Stage01.asset SHA256未变：`741813b2d9ac38e857d8494c71b4959bf8af9dd1dfe081afdcb167a55f43bbbf`；GUID仍`f6d6dc8b03b323d4887d49ced670c862`。`Editor-Final-Corrected.json`确认非Play、未编译、Stage01 clean且level绑定有效。
- `Console-Final-Corrected.json`：本阶段新增72条，error=1（下节已修正CS1061）、warn=0、log=71；最终groundTruth当前consoleErrors/Warnings=0、compilationFailed=false。
- 下节战斗专项111项加地图6项，共117项定向验证通过，不是新全量226回归。无staged文件，不自行勾选BC-200/M2，停止并等待主代理与定向只读审查，不进入BC-201。

## 贴墙破砖Bug专项：已修复且定向审查通过

症状已用真实WeaponState炮口出生→BulletWorld.Step→MapState.ApplyBulletHits复现：紧贴墙从右向左/从上向下首发误删内层。炮口距中心10px、弹丸半径2px，导致外层重叠与内层接触都返回TOI=0；旧排序按地形row-major TargetId升序，错误优先小坐标内层。仅修改`BulletWorld.CompareCandidates`：同TOI且同为地形时先比较沿各自来弹方向的格深度，优先来弹侧外层，然后保留TargetId/BulletId稳定fallback。跨类型优先级、全局Sweep、炮口、伤害宽度和同tick快照不变。

新增`BrickDestructionTests` 12个参数用例：四方向紧贴/间距8px对照各连续两发（首发外层、次发残墙内层），以及四方向双弹同TOI稳定ID和同tick不钻两层。旧实现33项中29通过/4失败，失败精确为Down/Left贴墙两发用例及双弹用例，期望外层27、实际内层26；修复后33/33通过。未放宽旧断言。

证据目录`ContactFix/`，各过滤均为完整`BattleCity.Tests.<夹具名>`、async_tests=true，test status结果独立保存：

| 模式/过滤 | 修复后通过 | 结果文件 |
| --- | --- | --- |
| EditMode / BrickDestructionTests | 33/33（含新增12） | Fix-BrickResults.json |
| EditMode / BulletCollisionTests（扫掠、类型优先级及稳定平局） | 38/38 | Fix-CollisionResults.json |
| EditMode / WeaponTests | 15/15 | Fix-WeaponResults.json |
| PlayMode / Stage01PlayModeTests（实际场景/键盘） | 3/3 | Fix-StagePlayResults.json |
| PlayMode / BrickMapViewTests | 22/22 | Fix-MapViewResults.json |

本次专项合计111项通过，不是新一轮全量226回归，资产尾i误判及纠正后6项地图验证见上一节。`Old-TestResults.json`保留旧失败，`Fix-CompileStatus.json`确认completed/failed=false/errors=[]。

过程异常未隐藏：新增测试首次将TickFraction误写为Time，产生1条CS1061，已更正且之后编译/测试通过；未清Console。编译中一次`status --until-ready`报STATUS_BLOCKED_BY_DIALOG（Hold on...），随后同串行命令的编译状态completed、测试启动成功；主代理明确批准按已恢复的同协议瞬时状态继续，未重启/点击对话框/切换协议。初次管道符组合过滤匹配0项（Fix-EditResults.json），未计入通过，改用上述单夹具精确过滤。`Console-Since5.json`保存本阶段67条新增记录：error=1（上述已修正编译错误）、warn=0、log=66；最终groundTruth compilationFailed=false、compiling=false、consoleErrors=0、consoleWarnings=0。`Editor-FinalState.json`确认非Play、Stage01 clean。

本阶段未处理资产或UserSettings，无暂存/提交/推送，不勾选BC-200/M2，不进入BC-201；交主代理和一次定向只读审查后暂停。

## 结论与范围

**原实现阶段范围（历史记录）：** 第一关地图输入、编辑器配方、关卡资源/场景和专项测试。原阶段未修改共享运行脚本、既有测试断言、原始PNG或BuildSettings；随后本次贴墙Bug专项仅最小修改BulletWorld并补充回归，详见上节。最终BC-200已验收、M2未通过，不开始BC-201及后续任务。工程基线为`master@8e91090`加已验收未提交BC-107/108改动，本轮全部保留，没有提交、暂存或推送。

- 新入口：`Assets/BattleCity/Scenes/Stage01.unity`，可用WASD/方向键、Space、Esc；失焦自动暂停，重新聚焦后Esc恢复。
- 地图资产：`Assets/BattleCity/Data/Stage01.asset`，逻辑26×26、8px格、左下起行主序。运行时ArenaSession/MapState复制资产后交互，不读PNG决定碰撞。
- 场景直接复用已验证的CombatArena harness（ArenaController、相机/PixelPerfect、输入、移动、武器、扫掠、破砖、Tilemap与特效）；不复制或扩展战斗代码。
- 基地为数据位置绑定的`base.alive`完好Sprite，仅Transform/SpriteRenderer，无伤害/死亡/碰撞行为。基地16×16空间预留，外围倒U砖墙可破坏。**子弹当前可穿过基地画布，这是未进入BC-204的明确边界，不是已实现保护基地玩法。**
- 敌出生只有配置，不生成假敌人；波次序列为空，AI/波次/坦克共享/生命/会话胜负/HUD均未实施。

## 布局核对与来源

详见[Layout.md](Layout.md)，完整authoring矩阵位于`Assets/BattleCity/Editor/Stage01Layout.txt`。

实际使用image read查看208×208原PNG，随后以独立像素采样核对全部676个8×8块：**220砖、8钢、444纯黑空地、4基地占地**；对应运行terrain为220 Brick、8 Steel、448 Empty，无水/草/冰、无未知块静默回退。重要非对称坐标（左右横台砖在上/钢在下、中央钢块和底部基地）有独立手列断言防止Y翻转错误。

- 基地源矩形(96,192,16,16)，运行矩形(96,0,16,16)，中心(104,8)。保护墙运行顶(88,16,32,8)、左(88,0,8,16)、右(112,0,8,16)。
- 玩家(72,8)及敌出生(8,200)/(104,200)/(200,200)沿用LevelDefinition冻结默认。**PNG没有坦克/出生标记，不宣称这些点是像素核实的原版出生点。** 均经MapState占地、半开边界及彼此/基地不重叠校验；负例检查砖中、越界、玩家重叠及基地重叠。
- `ReferenceAudit.py`用Python标准库独立解码原PNG；`ReferenceCells.csv`保存676格源/运行坐标、分类与每格RGBA SHA256，`ReferenceAudit.json`保存原图hash/计数/采样矩阵。Unity EditMode测试也独立LoadImage并校验全部格，运行时不引用PNG。
- 原始参考图与GeneralSprites调色板不同，本轮复用既有已验收Sprite，不改PNG或重染素材，不声称逐RGB还原。比对图`Reference-vs-Stage01.png`：左原图、右实际场景相机画面裁出地图，均2倍最近邻；右侧额外的黄色坦克为玩家。`Stage01-Live.png`保存实际768×720（3倍参考帧）的场景画面。

## API authoring 与幂等性

`Stage01Authoring.Build()`或菜单`Battle City > Author Stage 01 (BC-200)`：拒绝Play或任何dirty场景；读取显式矩阵并拒绝未知字符，通过现有PixelGrid转换，验证占地，使用AssetDatabase/EditorSceneManager保存asset/unity及自动meta。将原CombatArena保存为副本后在Stage01重绑定关卡、玩家和基地，未写源场景/源地图。

实际连续运行三次均成功（`Authoring-First/Repeat/Third.json`）。第三次前后`Idempotence-Before/After.json`确认关卡JSON、场景GUID、关卡GUID、BuildSettings完全相同，第三次场景rootCount=6、baseCount=1，没有累积对象。每次复制/保存可能重新分配新增基地的场景内部fileID，不以场景YAML字节恒定作为幂等定义。

`Preserved-Hashes-Final.json`确认PixelBaseline.unity、CombatArena.unity、CombatArena.asset、EditorBuildSettings.asset和原Stage01 PNG的SHA256全部未变。所有asset/unity/meta均由Unity API创建，没有手写YAML/Sprite meta。

## 真实编译、测试及失败记录

CLI实际版本`1.0.0-beta.14`，self-update检查latest相同；Unity `6000.6.7f1`，同一个已运行Editor/Pipeline连接。使用当前catalog/schema发现`script eval`、`test run/status/list`、`editor.playmode start/stop`、`job wait`等名称，不使用旧文档猜名。完整参数索引见[Commands.md](Commands.md)，原始JSON均在本目录，每条unity command均带`--caller plugin --skill unity-cli --project-path H:/Projects/Unities/BattleCity --format json`。

| 运行 | 精确过滤/模式 | 实际结果 | 原始证据 |
|---|---|---|---|
| EditMode专项 | mode=EditMode, filter=BattleCity.Tests.Stage01Tests | **5/5通过** | EditMode-Special-Results.json |
| PlayMode专项最终 | mode=PlayMode, filter=BattleCity.Tests.Stage01PlayModeTests | **3/3通过** | PlayMode-Special-Pass-Results.json |
| 全量自有EditMode回归 | mode=EditMode, filter=BattleCity.Tests | **190/190通过** | EditMode-All-Results.json |
| 全量自有PlayMode回归 | mode=PlayMode, filter=BattleCity.Tests | **36/36通过** | PlayMode-All-Results.json |
| 测试集合核对 | test list mode=all，按FullName独立对比最终执行结果 | **226发现=226执行，无缺失/额外，0失败/跳过** | TestDiscovery.json、Test-SetComparison.json |
| PNG独立采样 | python Docs/Stage2/BC-200/ReferenceAudit.py | 676格全部分类，矩阵一致 | ReferenceAudit.json、ReferenceCells.csv |
| 显式编译 | script recompile → status --until-ready → script recompile_status | completed，failed=false，errors=[] | ReleaseFix-CompileRequest/Ready/Result.json |

新增8项（5 EditMode + 3 PlayMode）；既有218项全部保留并通过，不将重复专项/重试累计为新增测试。新增测试文件为`Stage01Tests.cs`、`Stage01PlayModeTests.cs`。

### 保留的非成功过程（不隐藏）

1. 首次`script recompile`返回`EDITOR_NOT_RUNNING`，保存于`Compile.json`；不能据此证明原因。按主代理批准的一次同CLI恢复，有限60秒`unity status --until-ready --timeout 60 --project-path ...`成功；随后只读确认编译已完成、无当前Console错误、非Play、CombatArena干净、无运行测试，再继续API authoring。未重启编辑器、未开batch、未换协议。
2. 首轮PlayMode专项2/3：本轮测试TearDown移除Keyboard后字段未置null，下一测试引用已移除设备。仅修补新增夹具字段清理；紧接的一次retry未显式recompile，仍运行旧代码，仍2/3，结果均保留（PlayMode-Special-Results/Retry-Results.json）。之后明确recompile并确认完成。
3. 设备隔离修补后第三轮2/3：新增钢墙测试在暂停恢复后直接重复S，没有先让Session采样释放；既定输入规则会抑制暂停前按住键，玩家仍在y191而非168。仅在本轮测试准备中加入None释放采样，保留原钢墙位置断言，未改生产输入/碰撞规则。`PlayMode-Special-Final-Results.json`保存该失败，后续3/3及226全量通过。

## 场景交互与真正的帧/tick证据

PlayMode专项真实加载Stage01场景，使用Input System虚拟Keyboard→实际ArenaController.Update，而非只对地图函数自证：

- 左移至砖柱下，向上射击，16px坦克被砖挡在y8；4px下层破坏、Map.Revision=4、cell(6,2) mask=12，真实Tilemap显示`.mask.12`且只保留上4px，authoring terrain不变。
- 从实际地图清通道上移并定位中央钢块上方，再用真实S+Space命中钢，命中特效位置(104,162)，子弹回收，revision=0；沿S下移最终卡在(104,168)，钢Tile仍在。
- 两次卸载/重载后砖掩码恢复15、revision恢复0、玩家出生恢复(72,8)，完好基地位置不变。运行变化未污染资产。
- 最终全量输出（Console-Final-Since394.json）：`BC200 realBrick frame=862->1048 tick=0->17 player=(56,8) revision=4 mask=12`；`BC200 realSteel frame=1052->1119 tick=224->314 player=(104,168) revision=0`。钢测试tick314含之后80tick手动边界驱动；真实输入命中部分先证实frame/tick均推进，测试源码保留此区分。

另外独立启动Stage01实际Play会话并截图：`Live-Frames.json` server-side job wait观测**frame8465→8517，met=true，framesObserved=2**；`Live-Ticks.json`观测**tick1217→1223，met=true，framesObserved=2**。随后API capture game_view source=camera保存实际画面，没有把截图当作推进证明。后台临时设置和自动tick先记录后恢复。

## Console、资源清理与编辑器最终状态

- 初始cursor394，最终cursor474；`Console-Final-Since394.json`保存全部80条新增记录，**新增error=0、warn=0**；历史buffer error71保持71（是先前任务历史，不冒称全部历史错误为零）。Test Runner失败由结果JSON保留，其通知在Console为普通log，不漏报为测试通过。
- `Console-Final-Status.json` groundTruth：compilationFailed=false、compiling=false、当前consoleErrors=0、consoleWarnings=0。
- 当前CLI capture实际将相对路径保存到`Assets/Docs/...`（见Capture-Live.json），已按返回路径读取画面。通过Unity Texture2D API生成左右比对、把证据复制到工程Docs，然后AssetDatabase仅删除经确认本轮生成的capture及其空目录；未删用户资产，未留下临时Texture/Sprite/场景对象。`Capture-TemporaryPaths.json`与`Capture-Comparison.json`保留路径及清理结果。
- 临时后台设置恢复为Application.runInBackground=false、PlayerSettings.runInBackground=false，保存以消除ProjectSettings文件残留；本轮CLI生成的Pipeline临时配置在确认override已关闭后移除。autoTick原值即true/16ms，仍true/16ms，persisted选择未改变。详见Settings-TargetedRestore.json、Editor-Final-State.json。
- 最终Editor：**非Play、非Pause、未编译，Stage01.unity已保存且dirty=false**；Assets/Docs临时路径为空、临时Pipeline配置不存在。不关闭/重启用户编辑器。

## 用户本轮反馈

用户已在Stage01 PlayMode测试，认为BC-200表现达到完成状态。这是本轮场景体验的定性确认，不扩展为未实现的基地伤害、AI/波次或完整第一关胜负通过，也不是逐项量化人工测试报告。

## Git状态、边界与交接

- `Git-Final-Status.txt`保存完整未提交状态；`git diff --cached --name-only`为空，**无staged文件**。`git diff --check`及新增源文件尾空白检查通过；Git提示LF未来可能转CRLF是已有工作区行尾配置提示，不是Unity编译警告。
- 本轮新增：Stage01.asset/unity及meta、Stage01Authoring.cs/Stage01Layout.txt及meta、两个Stage01测试及meta、此目录布局/比对/命令/原始结果/验证记录。
- 既存BC-107/108源码、测试、Docs、主计划未由本轮修改；`.vsconfig`及HierarchyWindow既存状态噪声保留。场景操作新增`UserSettings/EditorUserSettings.asset`的RecentlyUsedSceneGuid-1..3记录（0及其余配置保留）；未整文件恢复用户配置。ProjectSettings及Pipeline临时配置已恢复/清理。
- 未覆盖：逐项量化的Stage01人工测试报告、独立构建/长期压力/原版波次/敌军、基地阻挡与伤害、完整第一关胜负；用户已给出上述PlayMode定性体验确认，不扩大其证据范围。默认出生点与图集/PNG调色板差异已显式记录，不作为“原版完全还原”证据。
- 建议下一步：主代理进行既定独立只读审查、核对226项结果及布局比对，再决定BC-200验收；**BC-200 完成后按用户要求暂停，后续需用户恢复指令。** 不启动BC-201或其他后续任务；主计划/执行看板由主代理统一更新，本writer不修改。
