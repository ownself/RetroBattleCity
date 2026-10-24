# BC-207 实现与验证（writer）

## 范围、版本与结论

- 基线：`main / b2b14b9da04ed483c5265986b221172b6207a96d`；Unity 6000.6.7f1。使用已连接Editor与已核实最新CLI，不安装/升级包，不提交/推送。
- 本文仅为实现/验证交付，不是主代理Acceptance，不勾选总计划，不开展BC-208/209。
- 最终专项 **10 EditMode + 3 PlayMode通过**；受影响回归 **16 EditMode + 11 PlayMode通过**，共 **40个唯一用例**（不是一次全量运行）。失败首轮与有界纠正均保留。
- 实际GameView **512×480** 与 **1000×700** 已截图并逐像素比较：2倍像素块完全一致，非整数窗口保持512×480内容与纯黑边。真实Controller Update tick **0→623**，非仅“已进入Play”。

## 变更与显示语义

### 只读接缝

- 新增 `Scripts/UI/ArenaHud.cs`：Controller在已有显示刷新末尾推送当前Session；首次创建与RestartNow（已有RenderSession路径）立即推送。关卡来自Controller私有初始关卡快照，不读会变动的作者资产字段。
- 无HUD事件订阅、输入、战斗状态写入、计时器或独立战斗模型。禁用/销毁清除Session引用并隐藏Canvas；旧Session不能再驱动HUD。
- `ArenaController.cs` 仅增加可选HUD绑定、验证、显示刷新与清理；旧CombatArena未绑定，不改规则/计数/时钟/输入。

### 数字与生命含义

- 剩余敌军 = `EnemyWave.PendingCount + ArenaSession.AliveEnemyCount`。Pending已含预约；不另加Reserved。真实运行例：Pending16 + Alive4 = HUD20；预约/物化不消耗数字，死亡才减少。
- 生命显示 `PlayerLife.RemainingLives`，是**包括当前生命在内的剩余总出场次数**，不是额外备用坦克数。初始3=当前1+备用2；死亡后等待复活显示2（未来两次出场），复活成功后仍为2（当前1+备用1）；最后一次死亡显示0且Lost。底边明确标注 `LIVES CURRENT + RESERVE`，不改变BC-205。
- Ready：`READY  ENTER START`；Playing：阶段提示清空；Paused：`PAUSED  ESC RESUME`；Won/Lost：`WON`/`LOST`；底边持续显示 `R RESTART`。均沿用已有管理键，无第二套UI输入。

### 像素布局与作者生成

- 新增 `PixelHudText.cs`：仅用于缺失操作/结果字形的最小3×5像素mesh；原生数字、旗帜、玩家生命图形与敌军坦克均来自既有SpriteCatalog直接引用，无新字体/纹理/菜单。
- WorldSpace uGUI固定256×240，世界原点(-1,-1)，比例1/16，排序HUD。所有UI图形仅在地图 `[16,224)×[16,224)` 外；右槽32px。无GraphicRaycaster/EventSystem需求。
- 使用既有URP PixelPerfectCamera `UpscaleRenderTexture + Windowbox`，UI与地图进入同一低分辨率目标。不改相机、PPU、参考分辨率、项目渲染设置或Sprite素材。这里不是TMP/SDF文字，故不涉及低分辨率SDF模糊问题。
- 新增 `Editor/Stage01HudAuthoring.cs`；更新 `Stage01Authoring.Build()` 调用并保存Stage01的真实HUD绑定/Ready初始1、20、3。场景与meta由Editor API生成，未手写YAML。
- 作者recipe切换场景后重新读取Wave资产，避免仅被前场景持有的资产被卸载。此为本次实际失败发现并验证的必要recipe修正。
- 四个相关asmdef只增加 `Unity.ugui` 引用。
- `CombatArena.unity`、`PixelBaseline.unity`、Stage01/敌军/波次数据资产最终SHA256与HEAD逐字节相同，见 `Raw/baseline-preservation.json`。Git可能因行尾/stat缓存仍将三个数据资产列M，但无内容差异。父代理已有Execution-Board/Contract变更不属于worker编辑；`.vsconfig`未动。

## 命令与自动测试证据

所有Editor命令均带 `--caller plugin --skill <ui-ugui|unity-cli|2d-pixel-perfect> --format json`；完整参数（包括eval C#）保存在 `Raw/*.json` 的parameters，发现结果含当前命令schema。此版本实际为分组命令，不使用旧别名。

主要操作：

```text
unity status --format json
unity command --format json
unity command --query eval/test/capture/editor/console/recompile/wait --format json  （实际逐项调用）
unity command script recompile
unity command script recompile_status
unity command script eval -- --code 'return BattleCity.Editor.Stage01Authoring.Build();'
unity command test run -- --mode editor|playmode --filter <fixture> --async_tests true
unity command test status
unity command editor set_auto_tick -- --enable true --interval_ms 16 --persist false
unity command editor.playmode start
unity status --until-ready --format json
unity command job wait -- --condition '{"member":"UnityEngine.Time.frameCount","op":"changed"}' --timeout 10
unity command capture game_view -- --width 512 --height 480 --source screen --save_path BC207-Evidence/Ready-512x480.png
unity command capture game_view -- --width 1000 --height 700 --source screen --save_path BC207-Evidence/Ready-1000x700.png
unity command editor.playmode stop
unity command console get -- --since 681 --tail 50 --level error
```

CLI实际把截图写在Assets下；取图后通过File.Copy搬到本证据目录，使用AssetDatabase删除本轮临时截图资产与目录。没有遗留临时Observer、输入设备或验证脚本到生产目录。

| 用例集合 | 唯一数量 / 结果 | 原始最终证据 |
|---|---:|---|
| 新增ArenaHudTests | 10/10 | `Raw/hud-edit-final-result.json` |
| 新增ArenaHudPlayModeTests | 3/3 | `Raw/hud-play-final-result.json` |
| 既有Stage01Tests | 6/6 | `Raw/stage-edit-result.json` |
| 既有SessionLifecycleIntegrationTests | 10/10 | `Raw/lifecycle-edit-result.json` |
| 既有SessionLifecyclePlayModeTests | 3/3 | `Raw/lifecycle-play-result.json` |
| 既有Stage01PlayModeTests | 3/3 | `Raw/stage-play-result.json` |
| 既有ArenaPlayModeTests | 5/5 | `Raw/arena-play-result.json` |

专项覆盖：预约含在Pending内、预备受阻、成功物化、最后敌军死亡变0；生命死亡/等待/复活不重复扣减、最后死亡0；全部五阶段；原生数字多位与初值；Stage01保存绑定；所有UI在边框内且不覆盖战场、CanvasRenderer齐全；真实Ready/Enter/Playing/Esc/Paused/Lost/R重开、初始关卡快照与旧Session隔离；实际提交非零文字mesh；HUD与Controller禁用/销毁安全。

回归是在最终CanvasRenderer纠正之前执行，但对应Controller战斗接缝/地图/计数源码此后未改；纠正只为新增caption补组件并增加专项断言。没有机械重复既有全量472用例。异步启动输出的0个用例仅表示running，均以最终非零result判断，未冒称通过。

### 失败与一次有界纠正

1. `Raw/authoring.json`：首次recipe失败 `Value cannot be null: wave`；诊断当前controller.wave为空而AssetDatabase能加载。切场景后重新获取资产，一次纠正成功（`authoring-corrected.json`）。最终同recipe再次保存通过。
2. `Raw/hud-play-result.json`：首次Play专项2/3，Until超时。测试注入的炮口在基地上方砖边界y16，子弹先碰砖而非基地；纠正为现有生命周期专项相同的目标中心炮口。仅重跑失败项1/1（`hud-play-corrected-result.json`），无战斗代码改变。后来生命0边界1/1以及最终全专项3/3均通过。未将原失败抹掉；保留的是原始CLI结果JSON，XML只保留了纠正后的真实版本，文件名明确。
3. 首次视觉发现caption不可见：`Raw/Invalid-default-resized-no-captions.png`（CLI默认将GameView拉伸为1280×720，**不是有效像素证据**）。诊断第一次用了错误GetMesh API，保留 `visual-diagnosis.json`；更正API后明确报缺CanvasRenderer，见 `visual-diagnosis-corrected.json`。对自定义Graphic加RequireComponent、recipe显式添加，并增加组件/mesh专项断言；一次修正后提示真实可见，未反复更换截图方案。
4. `git diff --check`报告Unity自动生成场景的 `m_Name: ` 行尾空格，见 `Raw/diff-check.txt`。不为“清洁检查”手改场景YAML。源码/asmdef/文档定向检查通过（`Raw/source-diff-check.txt`空文件）；此风格结果不当成功隐藏。

## 实际运行、视觉与Console

- 已读运行loop与2d-pixel-perfect对应reference，并按当前schema操作。URP检测见 `Raw/pre-visual-settings.json`。
- GameView通过Editor API临时添加固定512×480与1000×700尺寸；不是仅改变输出图片尺寸。原索引0，临时索引7/8；结束恢复原索引并删除两个临时尺寸。
- 有效截图：
  - `Ready-512x480.png`：2×参考帧，Ready/Enter、R、Stage1、Enemy20、Lives3及总生命说明可读。
  - `Ready-1000x700.png`：同一Ready帧，内容矩形 `(244,110,512,480)`，外围黑边。
  - `Paused-1000x700.png`：真实运行后Paused/Esc Resume提示，非伪造文字状态。
- `Raw/pixel-comparison.json`：整数截图每2×2块像素差异0；非整数截图内容crop与整数截图差异0；黑边非黑像素0。非整数地图矩形 `(276,142,416,416)`，HUD槽 `(692,110,64,480)`；与冻结参考帧对齐。
- `Raw/visual-geometry-1000.json`：真实Screen1000×700，pixelRatio2，Windowbox/UpscaleRenderTexture，caption mesh568顶点，Ready 1/20/3。
- `Raw/visual-frames.json`：frame3982→4030、met=true；`runtime-before.json`→`runtime-after.json`：frame22590→27271、Clock tick0→623；后者Pending16+Alive4、HUD20，生命3，PhasePlaying。随后显式暂停以拍Paused图。没有调整timescale或战斗时钟。
- `Raw/console-increment-final.json`：cursor681→776无新增error/exception；进一步 `Raw/console-observed-interval-final.json` 覆盖cursor664→776，无新增warn/error/exception，dropped=false/reset=false，当前编译失败false、Console error/warn0。流中的5条历史error均在本轮writer之前（最晚18:19），不清除或冒称历史为零。eval/作者失败另在原始命令结果如实保存。

## 未覆盖与最终现场

- 未进行用户人工体验；未验证小于256×240窗口，不在契约保证内。
- Won及生命耗尽Lost由真实共享战斗模型的受控专项验证，未将其包装为完整第一关胜利或两种失败人工体验；没有开展208/209闭环验收。
- 没有独立新增纹理/字体资产；3×5字形为有限HUD字符集，不宣称完整字体。
- 最终 `Raw/final-editor-state.json`：**Play=false、Paused=false、Stage01已保存/dirty=false、HUD1个、GameView索引0、runInBackground=false、临时尺寸key=-1**。原AutoTick已为true/16ms，本轮仅显式保持且persist=false，未改原持久化设置。测试Keyboard均由fixture TearDown移除；没有临时observer。
- 本轮Editor自动写入的最近场景缓存已按writer入口干净基线定向恢复；不清理既有`.vsconfig`或父代理文档。无暂存文件，见 `Raw/staged-files.txt`（空）。
- 待fresh reviewer只读审查精确diff/证据，再由主代理决定验收。当前没有运行步骤阻塞，不需要人工代替自动验证；不得把本文作为主代理验收记录。
