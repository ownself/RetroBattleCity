# BC-107 命令与报告索引

以下均在 `H:/Projects/Unities/BattleCity` 执行。所有 `unity command` 调用均带：

```bash
--caller plugin --skill unity-cli --project-path H:/Projects/Unities/BattleCity --format json
```

表中为命令主干；上述公共参数实际均存在，带 `--` 的参数属于Pipeline命令。

| 主干及专属参数 | 结果 / 保存报告 |
|---|---|
| `unity status --project-path H:/Projects/Unities/BattleCity --format json` | 1个ready编辑器，6000.6.7f1（初始终端输出） |
| `unity command` | CommandCatalog.json（group catalog） |
| `unity command --query ...` | 按实际子串查询script eval、script recompile、test、editor.playmode、console、capture、runtime、editor空格等，使用的schema合并在CommandSchemas.json；初次以竖线拼接查询返回空，并非正则 |
| `unity command script recompile` | CompileRequest.json；本轮只触发一次 |
| `unity command script recompile_status` | 最终CompileResult.json：completed，errors=[] |
| `unity command script eval -- --code 'return new { compiling = UnityEditor.EditorApplication.isCompiling, updating = UnityEditor.EditorApplication.isUpdating, playing = UnityEditor.EditorApplication.isPlaying };'` | **失败**：socket hang up；域重载期间，ConnectionIncident.txt，主代理批准恢复 |
| `unity status --until-ready --timeout 60 --project-path H:/Projects/Unities/BattleCity --format json` | ReconnectStatus.json及各Status/Ready/FinalEditorStatus.json；ready |
| `unity command console status` | ConsoleAfterCompile.json / ConsoleFinalRuntime.json |
| `unity command test status` | TestStatusBefore.json先确认无运行任务；随后每批Results.json完整逐项报告 |
| `unity command test run -- --mode editor --filter CombatEffectsTests --async_tests true` | EditModeStart.json → EditModeResults.json：9/9 |
| `unity command editor set_auto_tick -- --enable true --interval_ms 16 --persist true` | AutoTick.json；后台真实推进辅助 |
| `unity command test run -- --mode playmode --filter CombatEffectViewTests --async_tests true` | PlayModeStart.json → PlayModeResults.json：5/5 |
| `unity command test run -- --mode editor --filter BulletCollisionTests --async_tests true` | CollisionStart.json → CollisionResults.json：38/38 |
| `unity command test run -- --mode editor --filter BrickDestructionTests --async_tests true` | BrickStart.json → BrickResults.json：21/21 |
| `unity command test run -- --mode playmode --filter BrickMapViewTests --async_tests true` | BrickViewStart.json → BrickViewResults.json：22/22 |
| `unity command test run -- --mode playmode --filter ArenaPlayModeTests --async_tests true` | ArenaStart.json → ArenaResults.json：5/5 |
| `unity command script eval -- --code '<素材/场景/后台读取>'` | RuntimeSetup.json，parameters.code保存完整实际源码 |
| `unity command editor.playmode set_run_in_background -- --enable true` | BackgroundOn.json |
| `unity command editor.playmode start` | RuntimeStart.json；随后status等待ready |
| `unity command script eval -- --code '<运行采样/暂停采样>'` | RuntimeSample1/2.json、RuntimePaused1/2.json，各parameters.code为完整源码 |
| `unity command capture game_view -- --source screen --save_path Docs/Stage1/BC-107/PausedExplosion.png` | CaptureResult.json；命令实际规范到Assets/Docs，转存并清理临时资产后最终PNG在本目录 |
| `unity command script eval -- --code '<ClearEffects/AssetDatabase清临时截图>'` | RuntimeClear.json，完整源码与返回值 |
| `unity command console get -- --tail 20 --level error --since 200 --since_session 6c7f1251baea4fc49745630725f772d1` | ConsoleNewErrors.json：新error条目0，cursor273，无丢弃 |
| `unity command editor.playmode stop` | RuntimeStop.json；随后status ready |
| `unity command editor.playmode set_run_in_background -- --enable false` | BackgroundRestored.json：恢复工程原值false |
| `git status --short` / `git rev-parse --short HEAD` | 启动HEAD=8e91090；最终GitStatus.txt |
| `git diff --stat` / `git diff --check` / `git diff --cached --name-only` | 最终GitStatus.txt；check通过，暂存为空 |

没有调用 `unity test`/`unity run` 或任何batch Editor。测试结果精确总计68 EditMode +32 PlayMode=100，均通过；未运行项目详见Verification.md。
