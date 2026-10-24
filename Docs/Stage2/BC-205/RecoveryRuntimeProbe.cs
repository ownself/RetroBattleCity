using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BattleCity;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Unimported, bounded observation recipe. Only production Controller.Update advances combat.
public static class BC205RecoveryRuntimeProbe
{
    private const string Root = "Docs/Stage2/BC-205/";
    private static ArenaController controller;
    private static ArenaSession session;
    private static TankState player, enemy;
    private static TankMotor motor;
    private static WeaponState weapon;
    private static SpriteRenderer shield;
    private static Keyboard keyboard;
    private static TankDefinition temporaryDefinition;
    private static EditorApplication.CallbackFunction observer;
    private static readonly List<object> observations = new List<object>();
    private static readonly List<object> captures = new List<object>();
    private static readonly List<object> damageHits = new List<object>();
    private static readonly HashSet<long> seenHitLeases = new HashSet<long>();
    private static bool backgroundBefore, finished, sawEnemyProjectile, protectedHit;
    private static int stage, startFrame, phaseFrame, lastFrame, warmFrames;
    private static float deadline, phaseTime;
    private static long deathTick = -1, respawnTick = -1, frozenTick;
    private static int frozenTimer;
    private static Sprite frozenSprite;
    private static MethodInfo encodeScreen;

    public static string Main()
    {
        if (observer != null || SessionState.GetBool("BC205.RecoveryObserverActive", false))
            throw new InvalidOperationException("Only one recovery observation is permitted.");
        try
        {
            if (!EditorApplication.isPlaying || !EditorApplication.isPaused)
                throw new InvalidOperationException("Enter a fresh paused Editor Play session first.");
            controller = UnityEngine.Object.FindAnyObjectByType<ArenaController>();
            session = controller.Session;
            if (controller.gameObject.scene.path != "Assets/BattleCity/Scenes/Stage01.unity" ||
                session.Clock.Tick != 0 || session.PlayerLife == null || session.PlayerLife.RemainingLives != 3)
                throw new InvalidOperationException("Expected untouched Stage01 lifecycle at tick0.");
            player = session.PlayerTank; motor = session.Player; weapon = session.PlayerWeapon;
            shield = controller.GetComponentsInChildren<SpriteRenderer>(true).Single(r => r.name == "Player shield - display only");
            var captureType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands")).First(t => t != null);
            // Same installed screen encoder as the discovered capture game_view command. Writing the
            // returned PNG bytes directly under Docs avoids that command's Assets-relative import path.
            encodeScreen = captureType.GetMethod("EncodeScreenToPng", BindingFlags.Static | BindingFlags.NonPublic);
            if (encodeScreen == null) throw new InvalidOperationException("Installed screen encoder not found.");
            backgroundBefore = Application.runInBackground;
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            temporaryDefinition = UnityEngine.Object.Instantiate(controller.playerDefinition);
            temporaryDefinition.moveSpeedPixelsPerSecond = .001f;
            temporaryDefinition.bulletSpeedPixelsPerSecond = 180;
            enemy = session.AddTank(temporaryDefinition, CombatTeam.Enemy, new Vector2(72, 40));
            UnityEngine.Object.Destroy(temporaryDefinition); temporaryDefinition = null;
            session.SetFocus(true); session.SetPaused(true);
            startFrame = lastFrame = Time.frameCount;
            deadline = Time.realtimeSinceStartup + 90;
            Snapshot("ArmedPaused");
            observer = Observe;
            SessionState.SetBool("BC205.RecoveryObserverActive", true);
            EditorApplication.update += observer;
            Application.runInBackground = true;
            EditorApplication.isPaused = false;
            return "Single automatic observation armed: warm paused real Updates, shared enemy weapon death, pause, respawn shield, focus-handler freeze, protected hit, expiry, screenshots, finally cleanup. Cancel via BC205RecoveryRuntimeProbe.Cancel().";
        }
        catch
        {
            Cleanup();
            throw;
        }
    }

    public static void Cancel() => Finish(false, "Explicit cancellation");

    private static void Observe()
    {
        bool end = false; bool passed = false; string message = null;
        try
        {
            if (!EditorApplication.isPlaying || controller == null) throw new InvalidOperationException("Play/controller ended before observation completed.");
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Recovery observer exceeded90s.");
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            foreach (var hit in session.Bullets.Hits)
            {
                if (!seenHitLeases.Add(hit.BulletId)) continue;
                damageHits.Add(new { observedTick = session.Clock.Tick, frame = Time.frameCount, hit.BulletId, hit.OwnerId, hit.TargetId, kind = hit.Kind.ToString() });
                if (respawnTick >= 0 && hit.OwnerId == enemy.EntityId && hit.TargetId == player.EntityId && player.IsAlive)
                    protectedHit = true;
            }
            if (session.Bullets.Active.Any(b => b.OwnerId == enemy.EntityId)) sawEnemyProjectile = true;
            if (stage == 0)
            {
                Require(session.Clock.Tick == 0 && session.Clock.Paused, "Warmup must not advance combat.");
                if (++warmFrames < 10) return;
                Snapshot("WarmedPaused");
                session.SetTankCommand(enemy.EntityId, new TankCommand(CardinalDirection.None));
                session.SetPaused(false);
                session.SetTankCommand(enemy.EntityId, new TankCommand(CardinalDirection.Down, false, true));
                stage = 1; Snapshot("CombatResumed");
            }
            else if (stage == 1)
            {
                if (player.IsAlive) { Snapshot("LethalProjectileFlight"); return; }
                deathTick = session.Clock.Tick - (60 - session.PlayerLife.RespawnRemainingTicks);
                Require(session.PlayerLife.RemainingLives == 2 && !controller.playerRenderer.enabled && !shield.enabled, "Death projection/lives mismatch.");
                Require(sawEnemyProjectile, "Did not observe the real enemy projectile in flight.");
                Freeze(session.PlayerLife.RespawnRemainingTicks); stage = 2; Snapshot("DeathHidden");
            }
            else if (stage == 2)
            {
                CheckFrozen(session.PlayerLife.RespawnRemainingTicks);
                if (Time.frameCount == phaseFrame + 2) Capture("DeathHidden");
                if (Time.realtimeSinceStartup - phaseTime < .25f || Time.frameCount < phaseFrame + 5) return;
                Snapshot("DeathPauseFrozen"); session.SetPaused(false); stage = 3; Snapshot("WaitingResumed");
            }
            else if (stage == 3)
            {
                if (!player.IsAlive) { Snapshot("RespawnWaiting"); return; }
                respawnTick = session.Clock.Tick - (120 - session.PlayerLife.ProtectionRemainingTicks);
                Require(respawnTick == deathTick + 60, "Production respawn did not occur at D+60.");
                Require(controller.playerRenderer.enabled && shield.enabled && session.PlayerLife.IsProtected, "Respawn shield not visible/protected.");
                Require(ReferenceEquals(player, session.PlayerTank) && ReferenceEquals(motor, session.Player) && ReferenceEquals(weapon, session.PlayerWeapon), "Player references changed.");
                Freeze(session.PlayerLife.ProtectionRemainingTicks); stage = 4; Snapshot("RespawnShield");
                controller.SendMessage("OnApplicationFocus", false);
                controller.SendMessage("OnApplicationFocus", true);
            }
            else if (stage == 4)
            {
                CheckFrozen(session.PlayerLife.ProtectionRemainingTicks);
                Require(shield.sprite == frozenSprite, "Shield animation advanced while paused/focus-returned.");
                if (Time.frameCount == phaseFrame + 2) Capture("RespawnShield");
                if (Time.realtimeSinceStartup - phaseTime < .25f || Time.frameCount < phaseFrame + 5) return;
                Snapshot("FocusReturnStillPaused"); session.SetPaused(false);
                session.SetTankCommand(enemy.EntityId, new TankCommand(CardinalDirection.Down, false, true));
                stage = 5; Snapshot("ProtectionResumed");
            }
            else if (stage == 5)
            {
                Require(player.IsAlive && session.PlayerLife.RemainingLives == 2, "Player harmed during protection.");
                if (session.PlayerLife.IsProtected) { Snapshot("ProtectedUpdate"); return; }
                Require(session.Clock.Tick >= respawnTick + 120, "Protection ended early.");
                Require(protectedHit && enemy.Weapon.ActiveBullets == 0, "Protected enemy hit was not observed/consumed.");
                Require(!shield.enabled && shield.sprite == null, "Shield lingered after protection.");
                Freeze(0); stage = 6; Snapshot("ProtectionExpired");
            }
            else if (stage == 6)
            {
                CheckFrozen(0);
                if (Time.frameCount < phaseFrame + 2) return;
                Capture("ProtectionExpired"); Require(captures.Count == 3, "Expected three distinct phase captures.");
                Snapshot("Completed"); end = passed = true; message = "All automatic production-path observation phases completed.";
            }
        }
        catch (Exception error) { end = true; message = error.ToString(); }
        finally { if (end) Finish(passed, message); }
    }

    private static void Freeze(int timer)
    {
        session.SetPaused(true); frozenTick = session.Clock.Tick; frozenTimer = timer;
        frozenSprite = shield.sprite; phaseFrame = Time.frameCount; phaseTime = Time.realtimeSinceStartup;
    }
    private static void CheckFrozen(int timer)
    {
        Require(session.Clock.Paused && session.Clock.Tick == frozenTick && timer == frozenTimer, "Pause/focus failed to freeze ticks/timer.");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Capture(string phase)
    {
        var bytes = (byte[])encodeScreen.Invoke(null, new object[] { 1280, 720 });
        string path = Root + "Recovery-" + phase + ".png";
        File.WriteAllBytes(path, bytes);
        captures.Add(new { phase, path, frame = Time.frameCount, tick = session.Clock.Tick, bytes = bytes.Length, source = "screen", implementation = "Installed CaptureCommands.EncodeScreenToPng" });
    }
    private static void Snapshot(string phase)
    {
        observations.Add(new {
            phase, stage, frame = Time.frameCount, tick = session.Clock.Tick, delta = Time.unscaledDeltaTime,
            realtime = Time.realtimeSinceStartup, deathTick, respawnTick,
            alive = player.IsAlive, lives = session.PlayerLife.RemainingLives, wait = session.PlayerLife.RespawnRemainingTicks,
            protection = session.PlayerLife.ProtectionRemainingTicks, paused = session.Clock.Paused,
            playerVisible = controller.playerRenderer.enabled, shieldVisible = shield.enabled,
            shieldSprite = shield.sprite == null ? null : shield.sprite.name,
            playerPosition = motor.Position.ToString(), shieldPosition = shield.transform.position.ToString(),
            playerId = player.EntityId, enemyId = enemy.EntityId, playerHp = player.HitPoints,
            playerLeases = weapon.ActiveBullets, enemyLeases = enemy.Weapon.ActiveBullets,
            droppedTicks = session.Clock.DroppedTicks
        });
    }
    private static void Finish(bool passed, string message)
    {
        if (finished) return;
        finished = true;
        try
        {
            File.WriteAllText(Root + "Recovery-Runtime-Observed.json", JsonConvert.SerializeObject(new {
                passed, message, startFrame, finalFrame = Time.frameCount, deathTick, respawnTick,
                sawEnemyProjectile, protectedHit, observations, damageHits, captures,
                droppedTicks = session == null ? -1 : session.Clock.DroppedTicks,
                priorRunDroppedTicks = 4533, note = "Controlled automated observation, not manual user experience. Focus tested through production callback, not OS focus switching."
            }, Formatting.Indented));
        }
        finally { Cleanup(); }
    }
    private static void Cleanup()
    {
        if (session != null) session.SetPaused(true);
        if (observer != null) EditorApplication.update -= observer;
        observer = null;
        if (keyboard != null && InputSystem.devices.Contains(keyboard)) InputSystem.RemoveDevice(keyboard);
        if (temporaryDefinition != null) UnityEngine.Object.Destroy(temporaryDefinition);
        SessionState.EraseBool("BC205.RecoveryObserverActive");
        Application.runInBackground = backgroundBefore;
        File.WriteAllText(Root + "Recovery-Runtime-Cleanup.json", JsonConvert.SerializeObject(new {
            observerRemoved = true, keyboardRemoved = keyboard == null || !InputSystem.devices.Contains(keyboard),
            backgroundRestored = Application.runInBackground == backgroundBefore, background = Application.runInBackground,
            sessionPaused = session == null || session.Clock.Paused, finished
        }, Formatting.Indented));
    }
}
