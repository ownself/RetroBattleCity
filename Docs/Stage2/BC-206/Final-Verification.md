# BC-206 final validation — BLOCKED

Validation only; no implementation, recovery, staging, commit, push, PR, Acceptance or plan edits. Stopped immediately after collecting the failed final regression and read-only cleanup evidence. Parent deadline steering received after that collection; no new validation work started.

## Environment and source identity

- Project `H:/Projects/Unities/BattleCity`; HEAD `e2383bacf3b661f930f87667ed9d6d3062780f79`.
- Same Editor PID 26924 / port 7800, Unity 6000.6.7f1. Existing CLI 1.0.0-beta.14 / Pipeline 0.9.0-exp.1 retained; no upgrade/version rediscovery.
- Read AGENTS.md, Contract.md, Manual-Check.md, Verification.md, SessionLifecyclePlayModeTests.cs and the two supplied CLI skill files. Used the existing Integration catalog/schema, not broad command rediscovery.
- Fresh preflight and final inspection: Stage01 clean/stopped, paused=false, compiling=false, updating=false, runtime and PlayerSettings runInBackground=false. Model and corrected Play fixture types loaded. Console groundTruth compilationFailed=false before and after tests. No forced recompile was needed or issued.
- All **68** BattleCity `.cs`/`.asmdef` hashes match the pre-test snapshot. All **four frozen model source/test/meta** files match `Model-Validated-Hashes.json`.
- Protected hash delta during this slice is empty, including `.vsconfig`, CombatArena, data, Packages and ProjectSettings. Existing BC206 evidence snapshot (60 files, taken during the async test interval) also remains unchanged; earlier tracked evidence matches the integration baseline.
- Original integration baseline differs in the existing intended authoring/Stage01/controller/session/input/compatibility-test files. Existing parent plan/continuation/board deltas also predate this validation and were not edited. Exact list retained in `Final-Hash-Comparison.json`; these are acknowledged pre-existing deltas, not new validation edits.

## Final full regression — each mode executed once

| Mode | Discovery | Completed results | Passed | Failed | Duration |
|---|---:|---:|---:|---:|---:|
| EditMode | 407 | 407 | 407 | 0 | 4.91 s |
| PlayMode | 63 | 63 | 62 | 1 | 21.51 s |

Both starts used async_tests=true. Initial zero-result `running` acknowledgements are **not** counted as passes. CLI exit/outer success were checked; starts also report nested success=true, and actual status/results were read before proceeding. Completed status does not expose a separate nested success or compilationFailed field, so verdict comes from its completed state, summaries and individual statuses, together with Console groundTruth compilationFailed=false.

Exactly 470 unique `(FullName, mode)` cases were discovered and executed. Missing=0, extra=0, duplicate discovery=0, duplicate execution=0. One nonPassed result, below. Used one Edit status check, two Play status checks, and one bounded Play readiness wait. No individual model/Edit/Play fixture rerun or full-suite retry occurred.

### Blocking failure

`BattleCity.Tests.PlayerLifeIntegrationPlayModeTests.Stage01_RealUpdateSharedEnemyWeaponDeathHiddenWaitRespawnShieldExpiry_PauseAndFocusFreeze`

- Status: Failed; duration 5.0274953 s.
- Message: `Real Update did not reach the required life phase before deadline; Expected: True; But was: False`.
- Stack: `PlayerLifeIntegrationPlayModeTests+<WaitUntil>d__6.MoveNext`, `Assets/BattleCity/Tests/PlayMode/PlayerLifeIntegrationPlayModeTests.cs:28`, then Unity TestEnumerator.
- The result does not identify which life-phase wait stalled. No root cause is asserted, no source patch was made, and no rerun was attempted. Parent decides whether a narrowly scoped fix is warranted.

All three corrected `SessionLifecyclePlayModeTests` passed in this full Play run:

1. `OutcomeNotificationRestart_ReplacesOnlyAfterSettledTickAndOldSessionNeverAdvancesAgain`
2. `RealControllerUpdate_ReadyStartHeldInputFocusTerminalAndRestartEdges`
3. `ThreeRestarts_RestoreSnapshotsNaturalFirstSpawnLeasesAndPresentationWithoutHostAccumulation`

The current-run Console includes `BC206 realUpdate frame=8849->9038 oldTerminalTick=3 newTick=1 outcomes=1`; this is **Play-test evidence**, not an independent runtime observation. The full Edit run also includes the existing exact logical lifecycle/model paths. User feedback remains supplementary manual evidence, not an inferred historical Test Runner count.

## Runtime/screenshots and cleanup

The final test gate failed, so the conditional independent Ready freeze → StartBattle API → advancing Update observation and its two new screenshots were **not attempted**. No old screenshot was substituted. This evidence remains outstanding after the regression blocker is resolved; do not repeat the user's entire manual experience.

No independent Play session, temporary observer, input device, runtime mutation, background override or auto-tick change was created by this slice. Tests returned to stopped mode on their own. Final baseline-equivalent auto-tick=true/16ms; SessionState storedEnabled=false and storedInterval=-1 (absent sentinel), unchanged. No setting restoration writes were necessary. ProjectSettings disk diff is empty. Git diff --check passed; staged-file list is empty. `.vsconfig` is retained unchanged. No commits or PR changes.

Console retained, never cleared: session `316dee6ba1864c7dab134a9af37d8ce3`, original baseline cursor562, own preflight cursor581, final cursor626. Raw increments since562 and since581 saved with dropped=false/reset=false. Own interval contains 45 log entries and no warning/error entries; historical aggregate counts remain error5/warn1. The failing NUnit result still blocks even though no new Console error entry was emitted.

## Evidence and changed files

All project writes in this slice are new `Docs/Stage2/BC-206/Final-*` evidence/helper files; no Assets, scene, meta, production/test code, settings, parent plans or prior evidence was edited. This report is at the authoritative external output path. Key independently reviewable evidence:

- `Final-Commands.jsonl`: argv, exit codes, timings; raw stdout `.json` and `.stderr` files retained as bytes (UTF-8 parsing).
- `Final-Status-Before.json`, `Final-Loaded-State-Corrected.json`, `Final-Loaded-State-After-Tests.json`, `Final-Editor-After-Tests.json`.
- `Final-Discovery.json`, `Final-Full-Edit.json`, `Final-Full-Edit-Status-01.json`, `Final-Full-Play.json`, `Final-Full-Play-Status-01.json`, `Final-Full-Play-Status-02.json`, `Final-Play-Readiness.json`.
- `Final-Test-Reconciliation.json`: complete mode-labelled names, summaries, missing/extra/duplicate lists, exact failure.
- `Final-Baseline-Hashes.json`, `Final-Hash-Comparison.json`, `Final-Existing-Evidence-Hashes.json`.
- `Final-Console-Preflight.json`, `Final-Console-After-Edit.json`, `Final-Console-Since562.json`, `Final-Console-Since581.json`, `Final-Console-Status.json`.
- `Final-Git-Before.txt`, `Final-Git-After.txt`, `Final-HEAD.txt`, empty `Final-Staged-Before.txt` / `Final-Staged-After.txt`, empty `Final-ProjectSettings.diff`, empty `Final-Diff-Check.txt`.
- `Final-Run.py`: local evidence-capture helper, not imported into Assets and not an automatic recovery loop.

Local invocation mistakes are not hidden: obsolete underscore spellings for two status commands returned REMOTE_COMMAND_NOT_FOUND and were corrected to the exact grouped catalog names; an eval CLI argv placement error was corrected by placing global/caller flags before `--`. Their raw failures remain saved. A local heredoc quoting error and invalid Python import expression failed before any Unity operation and were corrected. None caused a test rerun, Editor recovery, asset mutation or setting write.

## Handoff / next action

BC206 remains blocked, not accepted. No build was run; current automatic compilation/type-load evidence is clean and both final test suites completed, with the one genuine Play regression above. Fresh review is still required.

For a shortest manual diagnostic, keep the current stopped/clean Stage01 version, open Test Runner's failed PlayerLifeIntegrationPlayModeTests result and inspect the stack/source to identify which bounded life-phase wait timed out; return that wait's expected phase and observed phase/tick (or a failure-detail screenshot/log). Do not repeat the full user experience or rerun all tests blindly. Parent should review the exact failure and authorize any small fix in a separate slice. After resolving the blocker, fill only the necessary regression/evidence gap and the short independent Update/screenshots check; no BC207 work was started.
