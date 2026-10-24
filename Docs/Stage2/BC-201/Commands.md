# BC-201 实际命令索引

所有 `unity command` 附 `--caller plugin --skill unity-cli --project-path H:/Projects/Unities/BattleCity --format json`；表中参数位于`--`后。原始JSON内保留完整eval源码。

| 原始文件 | 命令 | 参数 |
| --- | --- | --- |
| AutoTick.json | `command editor set_auto_tick` | `{"enable": "true", "interval_ms": "16", "persist": "false"}` |
| Background-Enable.json | `command editor.playmode set_run_in_background` | `{"enable": "true"}` |
| Background-Restore.json | `command editor.playmode set_run_in_background` | `{"enable": "false"}` |
| Capture-Cleanup.json | `command script eval` | `{"code": "见该原始JSON完整源码"}` |
| CLI-VersionCheck.json | `self-update` | `{}` |
| Compile-Completed.json | `command script recompile_status` | `{}` |
| Compile-Ready.json | `status` | `{}` |
| Compile-Request.json | `command script recompile` | `{}` |
| Compile-Result.json | `command script recompile_status` | `{}` |
| Console-AfterTests.json | `command console get` | `{"since": "77", "tail": "1000"}` |
| Console-Baseline.json | `command console status` | `{}` |
| Console-Cursor.json | `command console get` | `{"tail": "1"}` |
| Console-Final-Since77.json | `command console get` | `{"since": "77", "tail": "1000"}` |
| Console-Final-Status.json | `command console status` | `{}` |
| EditMode-All-Completed.json | `command test status` | `{}` |
| EditMode-All-Final-Results.json | `command test status` | `{}` |
| EditMode-All-Final-Start.json | `command test run` | `{"mode": "EditMode", "filter": "BattleCity.Tests", "async_tests": "true"}` |
| EditMode-All-Results.json | `command test status` | `{}` |
| EditMode-All-Start.json | `command test run` | `{"mode": "EditMode", "filter": "BattleCity.Tests", "async_tests": "true"}` |
| Editor-Baseline.json | `command script eval` | `{"code": "见该原始JSON完整源码"}` |
| Editor-Connection.json | `status` | `{}` |
| Editor-FinalState.json | `command script eval` | `{"code": "见该原始JSON完整源码"}` |
| Editor-PreTests.json | `command editor status` | `{}` |
| FinalCompile-Ready.json | `status` | `{}` |
| FinalCompile-Request.json | `command script recompile` | `{}` |
| FinalCompile-Result.json | `command script recompile_status` | `{}` |
| FinalTests-Background-Enable.json | `command editor.playmode set_run_in_background` | `{"enable": "true"}` |
| FinalTests-Background-Restore.json | `command editor.playmode set_run_in_background` | `{"enable": "false"}` |
| Live-Before.json | `command script eval` | `{"code": "见该原始JSON完整源码"}` |
| Live-Capture.json | `command capture game_view` | `{"source": "camera", "width": "768", "height": "720", "save_path": "H:/Projects/Unities/BattleCity/Docs/Stage2/BC-201/Stage01-Live.png"}` |
| Live-Frames.json | `command job wait` | `{"condition": "{\"member\":\"UnityEngine.Time.frameCount\",\"op\":\"changed\"}", "timeout": "10", "return_history": "true"}` |
| Live-Pause-After.json | `command script eval` | `{"code": "见该原始JSON完整源码"}` |
| Live-Pause-Before.json | `command script eval` | `{"code": "见该原始JSON完整源码"}` |
| Live-PausedFrames.json | `command job wait` | `{"condition": "{\"member\":\"UnityEngine.Time.frameCount\",\"op\":\"changed\"}", "timeout": "10", "return_history": "true"}` |
| Live-Ready.json | `status` | `{}` |
| Live-Resume.json | `command script eval` | `{"code": "见该原始JSON完整源码"}` |
| Live-Start.json | `command editor.playmode start` | `{}` |
| Live-Stop.json | `command editor.playmode stop` | `{}` |
| Live-StoppedReady.json | `status` | `{}` |
| Live-Ticks.json | `command job wait` | `{"condition": "{\"findType\":\"BattleCity.ArenaController\",\"member\":\"Session.Clock.Tick\",\"op\":\"changed\"}", "timeout": "10", "return_history": "true"}` |
| PlayMode-All-Ready.json | `status` | `{}` |
| PlayMode-All-Results.json | `command test status` | `{}` |
| PlayMode-All-Start.json | `command test run` | `{"mode": "PlayMode", "filter": "BattleCity.Tests", "async_tests": "true"}` |
| RegressionFix-CompileReady.json | `status` | `{}` |
| RegressionFix-CompileRequest.json | `command script recompile` | `{}` |
| RegressionFix-CompileResult.json | `command script recompile_status` | `{}` |
| Release-EditMode-Results.json | `command test status` | `{}` |
| Release-EditMode-Start.json | `command test run` | `{"mode": "EditMode", "filter": "BattleCity.Tests", "async_tests": "true"}` |
| Release-PlayMode-Ready.json | `status` | `{}` |
| Release-PlayMode-Results.json | `command test status` | `{}` |
| Release-PlayMode-Start.json | `command test run` | `{"mode": "PlayMode", "filter": "BattleCity.Tests", "async_tests": "true"}` |
| ReleaseCompile-Ready.json | `status` | `{}` |
| ReleaseCompile-Request.json | `command script recompile` | `{}` |
| ReleaseCompile-Result.json | `command script recompile_status` | `{}` |
| Settings-Restore.json | `command script eval` | `{"code": "见该原始JSON完整源码"}` |
| TankState-Final-Results.json | `command test status` | `{}` |
| TankState-Final-Start.json | `command test run` | `{"mode": "EditMode", "filter": "BattleCity.Tests.TankStateTests", "async_tests": "true"}` |
| TankState-Play-Ready.json | `status` | `{}` |
| TankState-Play-Results.json | `command test status` | `{}` |
| TankState-Play-Start.json | `command test run` | `{"mode": "PlayMode", "filter": "BattleCity.Tests.TankStatePlayModeTests", "async_tests": "true"}` |
| TankState-Results.json | `command test status` | `{}` |
| TankState-Start.json | `command test run` | `{"mode": "EditMode", "filter": "BattleCity.Tests.TankStateTests", "async_tests": "true"}` |
| TestDiscovery.json | `command test list` | `{"mode": "all"}` |
| TestFix-CompileReady.json | `status` | `{}` |
| TestFix-CompileRequest.json | `command script recompile` | `{}` |
| TestFix-CompileResult.json | `command script recompile_status` | `{}` |

其他实际命令：`git status --short`、`git branch --show-current`、`git rev-parse HEAD`、`git diff --check`、`git diff --cached --name-only`；Python逐项比较test list和Release执行集合、汇总各夹具结果及新增源码尾空白；结果见对应证据文件。命令目录发现原始暂存于`Temp/bc201-*-schema.json`，使用的实际schema已摘录保存为`CommandSchemas.json`。
