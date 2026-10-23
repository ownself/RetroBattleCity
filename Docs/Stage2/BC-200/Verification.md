# BC-200 第一关可交互地图专项验证

## 当前状态：已暂停，未最终验收

用户反馈处理耗时过长后，主代理已中断资产修复子任务，修复工作流随之终止，后续定向审查未启动；不再自动重试，也不开始BC-201。原地图实现、用户PlayMode正面反馈及226/226结果保留，但这些是异常磁盘文件被指出前的验证，不能代表当前落盘修复通过。

最终只读审查发现Stage01.asset地形序列末尾多出i；主代理中断后再核对仍为5409字符（期望5408hex），问题未解决。同值API保存/重序列化没有改写异常字段；最新修复命令报Value cannot be null / definition。修复尝试与异常原始文件备份保留在AssetRepair/，原因未确定，不归责于用户或原实现。BC-200未勾选，等待用户明确指令。下文为原实现阶段记录，以本节当前状态为准。

## 结论与范围

**实现和本轮自动验证已完成，交主代理/独立 reviewer 最终验收；BC-200、M2均未勾选。** 不开始BC-201及后续任务。仅新增第一关地图输入、编辑器配方、关卡资源/场景和专项测试；未修改共享运行脚本、既有测试断言、原始PNG或BuildSettings。工程基线为`master@8e91090`加已验收未提交BC-107/108改动，本轮全部保留，没有提交、暂存或推送。

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
