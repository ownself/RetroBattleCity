## Review

**Scope:** Read-only review of the current BC206 working-tree delta against the supplied `main/e2383bacf3b661f930f87667ed9d6d3062780f79` baseline, including the four new source/test files and their metas. Read `AGENTS.md` and all seven requested BC206 documents. Existing BC205 implementation was treated as baseline, not attributed to BC206.

No project files or runtime state were changed. No Unity, compilation, test, Play, or Git-write operations were performed.

### Correct

- **Settlement follows the complete combat tick.** `ArenaSession.Step` resolves bullet sweeps, map damage, effects and all target damage before player-life processing, enemy birth and outcome settlement (`Assets/BattleCity/Scripts/Core/ArenaSession.cs:292–320`). The model gives failure precedence, combines both failure flags, and requires exhausted sequence plus zero alive/pending/reserved enemies for victory (`Assets/BattleCity/Scripts/Core/SessionFlowState.cs:51–75`).

- **Terminal state is frozen before notifications.** Settlement pauses the clock and clears commands before publishing original death notifications and the outcome (`ArenaSession.cs:318–328`). The unchanged clock loop checks `Paused` between ticks (`Assets/BattleCity/Scripts/Core/LogicClock.cs:35–42`), so termination truncates the remaining same-render ticks. Shared-bullet integration cases cover victory, each last-enemy/failure conflict, combined failures, exactly-once notifications and eight-tick-frame truncation (`Assets/BattleCity/Tests/EditMode/SessionLifecycleIntegrationTests.cs:72–104`).

- **Ordinary lifecycle/input paths are coherent.** Ready and terminal phases cannot resume through `SetPaused(false)`; transition frames contribute no combat time; held combat inputs are suppressed until released (`ArenaSession.cs:160–215`). Controller-owned R edges survive session replacement, restart has priority over other sampled input, and the replacement session receives the current held-key suppression state (`Assets/BattleCity/Scripts/Core/ArenaController.cs:122–150`). The new production-Update test exercises these paths, including held R/Enter across replacement (`Assets/BattleCity/Tests/PlayMode/SessionLifecyclePlayModeTests.cs:48–88`).

- **Restart rebuilds rather than partially resets.** Private initial configuration is captured once, including separate private and session-exposed enemy-definition copies (`ArenaController.cs:74–109`). Restart retires/releases the old session, hides old displays before deferred destruction, constructs a new session, rebinds the existing bullet/effect hosts and immediately projects it (`ArenaController.cs:114–138`). There are no Controller-owned event subscriptions to accumulate. The four-iteration restart test covers restored map/base/player/lives/wave/seed/IDs/clock/leases, deterministic initial spawning, hidden old views and stable host/config counts (`SessionLifecyclePlayModeTests.cs:91–136`). Recursive outcome-triggered restart is separately covered at lines 139–156.

- **Compatibility changes preserve the old oracles.** Lifecycle remains opt-in through the default-false constructor argument and Controller field (`ArenaSession.cs:57–62`; `ArenaController.cs:17–18`). Stage01 explicitly opts in. The old base test retains its nonterminal failure-request assertions using a lifecycle-disabled host; the existing wave, player-life and Stage01 tests retain their substantive assertions. The four new meta GUIDs are distinct, with no duplicate matches found under `Assets`.

### Fixed — already present, not changed by this review

The old PlayerLife Play test now waits one real frame after `StartBattle` before injecting its one-shot enemy command (`Assets/BattleCity/Tests/PlayMode/PlayerLifeIntegrationPlayModeTests.cs:41–46`). This matches the intentional transition-frame command clearing in `ArenaSession.cs:173`. The correction does not weaken the death/respawn/shield assertions or change production code.

### Finding: P2 — Reentrant focus loss records unfocused state but discards the required pause

**Location:** `Assets/BattleCity/Scripts/Core/ArenaSession.cs:193–195, 232–242`; subsequent advancement at lines 173–178.

`SetFocus(false)` stores `focused = false` and calls `SetPaused(true)`. During a `StateChanged` notification, however, `ChangePaused` rejects that pause because `notifying` is true. After notification returns, the session remains Playing with an unpaused clock. `AdvanceFrame` suppresses unfocused player input, but does not otherwise stop a Playing session merely because it is unfocused.

**Deterministic source-level reproduction** with a valid lifecycle-enabled session and a nonempty wave:

```csharp
s.StateChanged += () => s.SetFocus(false);
s.StartBattle();
s.AdvanceFrame(0, PlayerButtons.None); // consumes transition
s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None);
```

The final call advances combat while focus remains false. This contradicts the Contract’s Playing→Paused focus-loss rule. The new notification/reentrancy guard makes this interaction reachable; the existing notification test covers reentrant start/pause/advance, but not focus loss (`SessionLifecycleIntegrationTests.cs:121–126`).

**Smallest fix:** preserve the notification reentrancy guard, but retain and apply a focus-loss pause before another combat tick can execute, without recursively publishing state notifications. Add a focused regression for the sequence above when addressing it.

This is a narrow public-event/API edge, not evidence that ordinary focus handling or the user’s observed gameplay is broken. The reproduction was verified by source tracing, not executed during this review.

### Validation evidence and limitations

Evidence paths below are under `Docs/Stage2/BC-206/`.

| Evidence | Result |
|---|---|
| `Final-Discovery.json`, `Final-Test-Reconciliation.json` | Records 470 discovered/unique executed cases, with no missing, extra or duplicate cases |
| `Final-Full-Edit-Status-01.json` | Completed **407/407 passed** |
| `Final-Full-Play-Status-02.json` | Completed **62/63 passed**, including all three new BC206 Play tests |
| Same original Play result | Retains the old PlayerLife failure, message and stack |
| `Recheck-Test-Start.json`, `Recheck-Test-Status-01.json` | Exact corrected old PlayerLife case, **1/1 passed** |
| `Recheck-Test-Reconciliation.json` | Retains the original 469 successes and supersedes only that failed case |

**This is not one 470-pass run.** The original full-run failure and the separate corrected-source result remain distinguishable.

`Final-Hash-Comparison.json` and `Recheck-Hash-Comparison.json` associate the runs with source manifests and report that the sole intervening source change was the PlayerLife test correction; production source hashes remain equal in those manifests. I inspected these associations and the raw results, but available read-only tools do not calculate hashes or programmatically recompute the complete reconciliation. I therefore **do not claim independently recalculated current-file SHA256 correctness or an independently recomputed 470-case set**.

The independent runtime eval failed on nonexistent `Session.Flow` before observation (`Recheck-Ready-01.json`); the public property is `Phase` (`ArenaSession.cs:25`). It produced neither logical Ready/Playing observations nor PNGs. That is an **evidence gap**, not a project compilation failure or gameplay-failure proof. Saved cleanup evidence reports stopped/clean, background=false and restored settings (`Recheck-Loaded-After.json`). I did not re-query or disturb the user’s current screenshot-capture session.

The preceding positive manual feedback remains manual feedback, not exact TestRunner counts. The two requested Game screenshots remain pending. No repeat suite or renewed runtime automation is requested by this review.

### Merge verdict: **OK with notes**

No P0/P1 defect identified. Retain the P2 notification/focus interaction above and the explicitly outstanding screenshot evidence; this review does not make a new source-run or acceptance claim.