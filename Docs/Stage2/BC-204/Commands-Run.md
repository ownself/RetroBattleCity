# BC-204 command evidence index

All `unity command` invocations used `--caller plugin --skill unity-cli --project-path H:/Projects/Unities/BattleCity --format json`; pipeline parameters were passed after `--`. No Console clear or Git add/commit/push/reset executed.

- `unity self-update --check --format json`: current/latest beta.14.
- `unity status --project-path H:/Projects/Unities/BattleCity --format json`: original Editor PID26924/7800 ready. At domain transitions used documented `--until-ready`; raw connections retained.
- Command/schema discovery: `unity command`, groups `script`, `runtime`, `test`, `console`, `editor`, `capture`; Commands.json and *-schema.json.
- `console get --tail 20`: Console-Baseline.json, cursor404/session316dee6ba1864c7dab134a9af37d8ce3.
- `script recompile`, `script recompile_status`: Compile* and Final-Compile.json. Compile2 first status failed during reload; parent approved same-protocol continuation after checkpoint; recovered status completed/errors[].
- `test run --mode editor --filter BaseStateTests --async_tests true`, `test status`: 21/21 passed.
- `script eval`: actual Stage01 renderer binding/name and scene save through Editor API, Scene-Binding/Name.json.
- `test run --mode playmode --filter BaseStatePlayModeTests --async_tests true`, `test status`: 3/3 passed.
- `test list --mode all`: 378 discovered.
- `test run --mode editor --async_tests true`, `test status`: final320/320 passed.
- `test run --mode playmode --async_tests true`, `test status`: final58/58 passed.
- Python UTF-8 JSON FullName set/counter comparison: Test-Set-Comparison.json, 378 unique executed exactly match discovery.
- `script eval`: read baseline/editor/actual autoTick state only; no autoTick write.
- `editor.playmode set_run_in_background --enable true`; `editor.playmode start`; `unity status --until-ready ...`; `script run --file Docs/Stage2/BC-204/RuntimeProbe.cs --entry BC204RuntimeProbe.Main`: real Update/keyboard/shared Weapon observer, Runtime-Observed.json.
- `script eval`: Runtime-Paused1/2.json, frame progresses with tick355 held.
- `capture game_view --source screen --width 1523 --height 752 --save_path Docs/Stage2/BC-204/Base-Destroyed.png`: result saved under Assets/Docs; copied outside Assets for evidence; one final PNG.
- `editor.playmode stop`; status ready; `editor.playmode set_run_in_background --enable false`: original restored.
- `script eval`: cleanup only own screenshot asset/empty directories and new inactive background config through Editor APIs; Cleanup.json. Final-State.json confirms original settings/no observers.
- `console get --since 404 --since_session 316dee6ba1864c7dab134a9af37d8ce3 --tail 1000`: 70 logs/0new warnings/errors. `--since 474 --tail 100`: 0 new after cleanup.
- Python hashlib SHA256 starting/current implementation and protected resources: Start.json, Protected-Start.json, Final-Integrity.json. 267 initial files, 6 authorized changes/261 unchanged/0 missing; 67 protected resources only Stage01 scene changes.
- `git diff --check`: passed, only existing line-ending warnings. `git diff --cached --name-only`: empty. `git status --short`, `git rev-parse HEAD`: unchanged branch/HEAD and prior work retained.

One local Python schema read initially failed on default GBK decoding; corrected to UTF-8. One ephemeral helper compile obsolete-API warning is retained in Runtime-Probe-Request.json. Neither is hidden or counted as a project compile failure. See Verification.md for limitations and exact frame/tick data.
