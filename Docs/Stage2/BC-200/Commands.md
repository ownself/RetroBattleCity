# BC-200 实际CLI命令索引

所有 `unity command` 均附 `--caller plugin --skill unity-cli --project-path H:/Projects/Unities/BattleCity --format json`。以下索引来自原始JSON，其parameters对应`--`后的参数；长eval源码见对应JSON。

| 证据文件 | command | parameters |
|---|---|---|
| After-TestFix-Console.json | `command console status` | `{}` |
| After-TestFix-Ready.json | `status` | `{}` |
| Authoring-First.json | `command script eval` | `{"code": "见原始JSON完整源码"}` |
| Authoring-Repeat.json | `command script eval` | `{"code": "见原始JSON完整源码"}` |
| Authoring-Third.json | `command script eval` | `{"code": "见原始JSON完整源码"}` |
| AutoTick-Enable.json | `command editor set_auto_tick` | `{"enable": "true", "interval_ms": "16", "persist": "false"}` |
| AutoTick-Restore.json | `command editor set_auto_tick` | `{"enable": "true", "interval_ms": "16", "persist": "false"}` |
| Background-Before.json | `command script eval` | `{"code": "见原始JSON完整源码"}` |
| Background-Enable.json | `command editor.playmode set_run_in_background` | `{"enable": "true"}` |
| Background-Restore.json | `command editor.playmode set_run_in_background` | `{"enable": "false"}` |
| Capture-Comparison.json | `command script eval_file` | `{"file": "Docs/Stage2/BC-200/CaptureComparison.cs"}` |
| Capture-Live.json | `command capture game_view` | `{"source": "camera", "width": "768", "height": "720", "save_path": "Docs/Stage2/BC-200/Stage01-Live.png"}` |
| Capture-TemporaryPaths.json | `command script eval` | `{"code": "见原始JSON完整源码"}` |
| Compile.json | `unity command script` | `{}` |
| Console-Final-Since394.json | `command console get` | `{"since": "394", "tail": "1000"}` |
| Console-Final-Status.json | `command console status` | `{}` |
| Console-Since394.json | `command console get` | `{"since": "394", "tail": "500"}` |
| EditMode-All-Results.json | `command test status` | `{}` |
| EditMode-All-Start.json | `command test run` | `{"mode": "EditMode", "filter": "BattleCity.Tests", "async_tests": "true"}` |
| EditMode-Special-Results.json | `command test status` | `{}` |
| EditMode-Special-Start.json | `command test run` | `{"mode": "EditMode", "filter": "BattleCity.Tests.Stage01Tests", "async_tests": "true"}` |
| Editor-BeforeTests.json | `command editor status` | `{}` |
| Editor-Final-State.json | `command script eval` | `{"code": "见原始JSON完整源码"}` |
| Idempotence-After.json | `command script eval` | `{"code": "见原始JSON完整源码"}` |
| Idempotence-Before.json | `command script eval` | `{"code": "见原始JSON完整源码"}` |
| Live-Frames.json | `command job wait` | `{"condition": "{\"member\":\"UnityEngine.Time.frameCount\",\"op\":\"changed\"}", "timeout": "10", "return_history": "true"}` |
| Live-Ready.json | `status` | `{}` |
| Live-Start.json | `command editor.playmode start` | `{}` |
| Live-Stop.json | `command editor.playmode stop` | `{}` |
| Live-Stopped-Ready.json | `status` | `{}` |
| Live-Tick-After.json | `command script eval` | `{"code": "见原始JSON完整源码"}` |
| Live-Tick-Before.json | `command script eval` | `{"code": "见原始JSON完整源码"}` |
| Live-Ticks.json | `command job wait` | `{"condition": "{\"findType\":\"BattleCity.ArenaController\",\"member\":\"Session.Clock.Tick\",\"op\":\"changed\"}", "timeout": "10", "return_history": "true"}` |
| PlayMode-All-Ready.json | `status` | `{}` |
| PlayMode-All-Results.json | `command test status` | `{}` |
| PlayMode-All-Start.json | `command test run` | `{"mode": "PlayMode", "filter": "BattleCity.Tests", "async_tests": "true"}` |
| PlayMode-Special-Final-Ready.json | `status` | `{}` |
| PlayMode-Special-Final-Results.json | `command test status` | `{}` |
| PlayMode-Special-Final-Start.json | `command test run` | `{"mode": "PlayMode", "filter": "BattleCity.Tests.Stage01PlayModeTests", "async_tests": "true"}` |
| PlayMode-Special-Pass-Ready.json | `status` | `{}` |
| PlayMode-Special-Pass-Results.json | `command test status` | `{}` |
| PlayMode-Special-Pass-Start.json | `command test run` | `{"mode": "PlayMode", "filter": "BattleCity.Tests.Stage01PlayModeTests", "async_tests": "true"}` |
| PlayMode-Special-Ready.json | `status` | `{}` |
| PlayMode-Special-Results.json | `command test status` | `{}` |
| PlayMode-Special-Retry-Results.json | `command test status` | `{}` |
| PlayMode-Special-Retry-Start.json | `command test run` | `{"mode": "PlayMode", "filter": "BattleCity.Tests.Stage01PlayModeTests", "async_tests": "true"}` |
| PlayMode-Special-Start.json | `command test run` | `{"mode": "PlayMode", "filter": "BattleCity.Tests.Stage01PlayModeTests", "async_tests": "true"}` |
| Preauthor-Console.json | `command console status` | `{}` |
| Preauthor-State.json | `command script eval` | `{"code": "见原始JSON完整源码"}` |
| Preauthor-TestStatus.json | `command test status` | `{}` |
| Recovery-Status.json | `status` | `{}` |
| ReleaseFix-CompileReady.json | `status` | `{}` |
| ReleaseFix-CompileRequest.json | `command script recompile` | `{}` |
| ReleaseFix-CompileResult.json | `command script recompile_status` | `{}` |
| Settings-TargetedRestore.json | `command script eval` | `{"code": "见原始JSON完整源码"}` |
| TestDiscovery.json | `command test list` | `{"mode": "all"}` |
| TestFix-CompileReady.json | `status` | `{}` |
| TestFix-CompileRequest.json | `command script recompile` | `{}` |
| TestFix-CompileResult.json | `command script recompile_status` | `{}` |
| Wait-Schema.json | `command` | `{}` |

其他实际执行：`unity --version`；`unity self-update --check --format json`（1.0.0-beta.14=latest）；`unity status --until-ready --timeout 60 --project-path H:/Projects/Unities/BattleCity --format json`（多次，原始结果见*-Ready/Recovery-Status.json）；`python Docs/Stage2/BC-200/ReferenceAudit.py`；`git diff --check`、`git diff --cached --name-only`、`git status --short`。

首轮连接错误与三轮非成功专项均保留原始JSON，具体根因/修补/最后通过结果见Verification.md。无batch或Editor重启。
