# BC205 integration checkpoint

## Intake / 2026-07-09
- Read Contract, Docs/Stage0/Core-Rules, Model-Handoff and upstream model report. No implementation edits yet; sole writer.
- Saved Integration-Baseline-Files.txt (tracked implementation/resources/settings plus model files and .vsconfig); existing editor PID26924 / port7800 / Unity6000.6.7f1 confirmed ready. CLI/discovery/Console/editor raw reports saved.
- Baseline settings eval failed exit6 COMMAND_FAILED: speculative UserSettings/UnityCliSettings.json does not exist. No setting changed, no retry/restart/fallback. Partial diff remains empty for this lane. Await supervisor approval before continuing diagnostics/implementation.

## Baseline recovered / before first compile
- Supervisor approved one bounded read-only diagnostic. Actual package AutoTickCommand uses SessionState; active autoTick=true/16ms, neither persisted key exists (interval=-1 and dual bool defaults differ). RecentlyUsedSceneGuid-0 absent; runInBackground=false; Stage01 clean/stopped. Recovered raw settings report saved. No setting changed.
- First local Python edit attempt failed parsing an unterminated triple quote before any write; subsequently used targeted edit tools. Not a Unity/bridge failure.
- Integration implementation and dedicated Edit/Play tests now written. Model sources untouched. First compile and scene opt-in via Editor API next; no YAML/meta edits.

## Infrastructure stop after compile
- Initial compile failed CS0117 (NUnit Is.AnyOf unavailable). Changed assertion to Does.Contain.
- Second compile command returned success/0, errors0/warnings0 but compilationFailed=true (possibly prior latch); cannot claim final clean compile.
- Next scene-enable eval failed exit12 EDITOR_NOT_RUNNING; the precomposed shell then also attempted the scheduled Edit专项 and got exit12 EDITOR_NOT_RUNNING. Neither Editor command executed. No scene enable, no test results. No retries/restart/kill/upgrade/fallback performed.
- Retained raw compile/scene/test failure reports, git partial diff/status/index. Pause for supervisor decision. Source changes are unverified and Stage01 actual scene has not yet opted in. Settings never mutated.

## Recovery and专项
- Approved recovery: status ready; script recompile_status failed=false/compilationFailed=false; guarded eval current compiling=false/failed=false, loaded new types, Stage01 clean/stopped. Historical CS0117 remains Console entry481; no clear.
- Stage01 scene lifecycle opt-in saved successfully through Editor API. Integration Edit专项 13/13 passed.
- Play专项 synchronous HTTP returned outer exit0/success=true but nested success=false, Total0: package explicitly rejects synchronous PlayMode due reload and instructs async_tests + test status. No Play tests executed; stop pending approval to use required async command. Raw report retained.
- Supervisor approved actual async protocol. Each async run bounded to <=6 minutes / <=12 status checks, readiness barrier only for normal reload, never resubmit an in-flight/completed run. Start/result/status raw persisted.

## 专项 passed / before final source compile
- Async Play专项 completed2/2 passed, real Stage01 Update death/hidden/wait/respawn/shield expiry + pause/focus handler freeze; CombatArena remains disabled. Raw Start/Ready/Status-01 saved. Not manual experience.
- Added stronger Edit tests for new presses sampled during zero-tick dead frames and all three integrated generations. Final source compile, full discovery/run/reconciliation and separate screenshot observer still pending.

## Full initial regression
- Full discovery419 = Edit359 / Play60. Edit359/359 passed. Play59/60 passed; one legacy Stage01 base sprite lookup dereferenced null sprite on the new inactive shield. Added only sprite!=null predicate guard, preserving base existence/position/brick assertions.
- Preparing new final compile and regression after test-only correction. No runtime/model changes after prior compile. Separate real-Update screenshot observation will use temporary unimported Docs recipe and a test-owned enemy clone; no fake HP/death/life writes.

## New infrastructure stop
- Final recompile requested after the one-line legacy regression guard returned exit6 COMMAND_FAILED / Pipeline HTTP400 Bad Request (data=null). No dependent command run and no retry. Saved Integration-Compile-Verified.json and refreshed partial diff.
- Last confirmed actual compile clean was Integration-Compile-Final + settled status before full test execution. The guard-only test edit is not yet validated; 419-test final successful execution set not yet established. Separate screenshot observation not begun. No temporary setting/resource changes to undo yet.

## Final full set / before real-Update observation
- Approved minimal recovery: current compile clean and loaded test/runtime assemblies saved; no repeat compile after HTTP400. Source hash manifest frozen.
- Full Play rerun60/60 passed; Edit359/359 retained (no runtime/model/Edit source changes after that run). Discovery419=419 unique successful executions, no missing/unexpected/duplicates/notPassed. Integration-Test-Reconciliation.json saved. Prior failing Play run retained.
- Real observer next: <=180s observer lifetime, temporary keyboard/definition clone only, actual Stage01 with explicit lifecycle. autoTick alreadytrue/16 remains untouched; backgroundfalse to be temporarily set true in Play and restored; recent key originally absent must remain absent. No authored asset mutation or manual clock/HP/life writes.

## Original-run cleanup / retained recovery intake (2026-10-24 07:51Z)
- Previous hard timeout occurred after successful observer/keyboard cleanup and clean stopped settings restoration; parent independently verified. Do not treat the missing old checkpoint tail as an active observer.
- Historical intake heading date 2026-07-09 was a writing error: actual baseline/report UTC is 2026-10-24.
- Recovery-Decision.md is the only new authorization. Only remaining observation/evidence; no Assets/model/test/scene edits and no rerun of24/419 successful tests.
- Fresh original PID26924/7800 ready, clean/stopped, no compile failure, backgroundfalse/recent absent, autoTicktrue/16ms and persistent keys absent. Recovery-Baseline-* saved;64 validated source hashes unchanged.
- New unimported RecoveryRuntimeProbe.cs has explicit Cancel hook,90s deadline and finally cleanup. Will make exactly one bounded automatic observation attempt with paused-session10-frame real-Update warmup, shared enemy weapon death, pause/focus callback freeze, protected hit, expiry and3 screen PNGs. Same installed screen encoder writes outside Assets to avoid importer side effects. No manual combat clock/HP/life writes. No previous4533 DroppedTicks hidden/reset.

## Recovery complete / closeout only
- One automatic production-Update observation passed: frame1→1665, deathD4/respawnR64, enemy owner2 bullet1 lethal, bullet3 consumed under protection at68, expiry184=R+120.3 fresh screen PNGs read/checked. Pause/focus handler snapshots freeze correctly. Current short runDroppedTicks0; previous4533 preserved, no performance/manual-experience claim.
- Finally removed recovery observer/virtual keyboard, restoredbackgroundfalse; final sameEditor clean/stopped/no compile or reload failure, recent absent, autoTicktrue16ms unchanged, no temporaryAssets/config/scene children. Stop emitted1 Already in edit mode warning556; prior Play exit cause not asserted.
- Final Console all-integration delta480→556 complete76 entries/no dropped/reset: only newerror481 fixedNUnit and warning556. Recovery-only delta555→556 onlywarning556. No Console clear.
- Protected259 recovery files unchanged, validated64 sources unchanged; FullName recomputation419 unique passed from preserved359Edit+60Play, no rerun. Verification.md / raw Recovery-* / command index saved. Parent closeout steering: no further Unity commands/tests/screenshots; ready for fresh independent read-only review, not Acceptance. No staging/commit/push/PR, .vsconfig preserved.
