#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BattleCity.Tests
{
    public sealed class SessionLifecyclePlayModeTests
    {
        private Scene scene;
        private Keyboard keyboard;
        private TankDefinition shotDefinition;
        private ArenaController Controller => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
        private void Keys(params Key[] keys)
        {
            if (keyboard == null) { keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent(); }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update();
        }
        private IEnumerator Load()
        {
            Keys();
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/BattleCity/Scenes/Stage01.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            Controller.SendMessage("OnApplicationFocus", true);
        }
        private static IEnumerator Until(System.Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 3;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "bounded production Update observation");
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (shotDefinition != null) Object.Destroy(shotDefinition);
            shotDefinition = null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator RealControllerUpdate_ReadyStartHeldInputFocusTerminalAndRestartEdges()
        {
            yield return Load(); var c = Controller; var s = c.Session;
            Assert.That(c.enableSessionLifecycle, Is.True); Assert.That(s.Phase, Is.EqualTo(SessionPhase.Ready));
            Keys(Key.Escape, Key.D, Key.Space); yield return new WaitForSecondsRealtime(.1f);
            Assert.That(s.Clock.Tick, Is.Zero); Assert.That(s.EnemyWave.ReservedCount, Is.Zero);
            c.SendMessage("OnApplicationFocus", false); Keys(Key.Enter); yield return null;
            Assert.That(s.Phase, Is.EqualTo(SessionPhase.Ready));
            c.SendMessage("OnApplicationFocus", true); Keys(); yield return null;
            int frame = Time.frameCount;
            Keys(Key.Enter, Key.A, Key.Space); yield return Until(() => s.Clock.Tick > 0);
            Assert.That(Time.frameCount, Is.GreaterThan(frame)); Assert.That(s.Player.Position, Is.EqualTo(new Vector2(72, 8)));
            Assert.That(s.PlayerWeapon.ActiveBullets, Is.Zero);
            Keys(); yield return null; Keys(Key.A); yield return Until(() => s.Player.Position.x < 72);
            c.SendMessage("OnApplicationFocus", false); long paused = s.Clock.Tick;
            c.SendMessage("OnApplicationFocus", true); Keys(); yield return new WaitForSecondsRealtime(.1f);
            Assert.That(s.Clock.Tick, Is.EqualTo(paused)); Assert.That(s.Phase, Is.EqualTo(SessionPhase.Paused));
            Keys(Key.Escape); yield return Until(() => s.Phase == SessionPhase.Playing);
            Keys();
            shotDefinition = Object.Instantiate(c.playerDefinition); shotDefinition.bulletSpeedPixelsPerSecond = 6000;
            var shot = new WeaponState(shotDefinition, s.Bullets, CombatTeam.Enemy, 999);
            // Controlled muzzle placement; sweep, base damage and terminal settlement are all production paths.
            shot.Step(new TankCommand(CardinalDirection.Down, true), s.Base.Position + Vector2.up * 10, CardinalDirection.Down);
            int outcomes = 0; s.SessionEnded += () => outcomes++;
            yield return Until(() => s.Phase == SessionPhase.Lost);
            long terminal = s.Clock.Tick; var ages = s.Effects.Active.Select(e => e.AgeTicks).ToArray();
            Assert.That(s.FailureReason, Is.EqualTo(SessionFailureReason.BaseDestroyed)); Assert.That(shot.ActiveBullets, Is.Zero);
            Keys(Key.Escape, Key.Enter, Key.Space); s.SetPaused(false); c.SendMessage("OnApplicationFocus", false); c.SendMessage("OnApplicationFocus", true);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(s.Clock.Tick, Is.EqualTo(terminal)); Assert.That(outcomes, Is.EqualTo(1));
            Assert.That(s.Effects.Active.Select(e => e.AgeTicks), Is.EqualTo(ages)); Assert.That(c.baseRenderer.sprite.name, Is.EqualTo("base.dead"));
            Keys(Key.R, Key.Enter, Key.A, Key.Space); yield return Until(() => c.Session != s);
            var restarted = c.Session;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(c.Session, Is.SameAs(restarted), "held R survives session replacement as one physical edge");
            Assert.That(restarted.Phase, Is.EqualTo(SessionPhase.Ready)); Assert.That(restarted.Clock.Tick, Is.Zero);
            Assert.That(s.Clock.Paused, Is.True); Assert.That(s.Bullets.Active, Is.Empty);
            Keys(); yield return null; Keys(Key.Enter, Key.A, Key.Space);
            yield return Until(() => restarted.Clock.Tick > 0);
            Assert.That(restarted.Player.Position, Is.EqualTo(new Vector2(72, 8))); Assert.That(restarted.PlayerWeapon.ActiveBullets, Is.Zero);
            Debug.Log($"BC206 realUpdate frame={frame}->{Time.frameCount} oldTerminalTick={terminal} newTick={restarted.Clock.Tick} outcomes={outcomes}");
        }

        [UnityTest] public IEnumerator ThreeRestarts_RestoreSnapshotsNaturalFirstSpawnLeasesAndPresentationWithoutHostAccumulation()
        {
            yield return Load(); var c = Controller;
            var bullets = c.BulletView; var effects = c.EffectView;
            Vector2Int? firstPosition = null; uint firstSeed = 0; int ownedConfigs = -1;
            for (int run = 0; run < 4; run++)
            {
                var s = c.Session;
                Assert.That(s.Phase, Is.EqualTo(SessionPhase.Ready)); Assert.That(s.Clock.Tick, Is.Zero); Assert.That(s.Clock.DroppedTicks, Is.Zero);
                Assert.That(s.PlayerTank.EntityId, Is.EqualTo(1)); Assert.That(s.PlayerTank.HitPoints, Is.EqualTo(c.playerDefinition.hitPoints));
                Assert.That(s.Player.Position, Is.EqualTo(new Vector2(72, 8))); Assert.That(s.Player.Facing, Is.EqualTo(CardinalDirection.Up));
                Assert.That(s.Player.TrackFrame, Is.Zero); Assert.That(s.PlayerWeapon.ActiveBullets, Is.Zero); Assert.That(s.PlayerWeapon.CooldownRemainingTicks, Is.Zero);
                Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(3)); Assert.That(s.PlayerLife.IsProtected, Is.False);
                Assert.That(s.Base.IsAlive, Is.True); Assert.That(s.Map.BrickMaskAt(6, 2), Is.EqualTo(15)); Assert.That(s.Map.Revision, Is.Zero);
                Assert.That(s.EnemyWave.PendingCount, Is.EqualTo(20)); Assert.That(s.EnemyWave.ReservedCount, Is.Zero); Assert.That(s.AliveEnemyCount, Is.Zero);
                Assert.That(s.EnemyWave.RandomState, Is.EqualTo(1)); Assert.That(s.Bullets.Hits, Is.Empty); Assert.That(s.Effects.Active, Is.Empty);
                Assert.That(c.BulletView, Is.SameAs(bullets)); Assert.That(c.EffectView, Is.SameAs(effects));
                Assert.That(c.GetComponentsInChildren<BulletView>(true).Length, Is.EqualTo(1)); Assert.That(c.GetComponentsInChildren<CombatEffectView>(true).Length, Is.EqualTo(1));
                int configCount = Resources.FindObjectsOfTypeAll<TankDefinition>().Count(d => d.hideFlags == HideFlags.HideAndDontSave);
                if (ownedConfigs < 0) ownedConfigs = configCount; else Assert.That(configCount, Is.EqualTo(ownedConfigs));
                Assert.That(c.StartBattle(), Is.True); s.AdvanceFrame(0, PlayerButtons.None);
                s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None);
                var request = s.EnemyWave.Reservation.Value;
                if (firstPosition == null) { firstPosition = request.PositionPixels; firstSeed = request.BrainSeed; }
                Assert.That(request.PositionPixels, Is.EqualTo(firstPosition.Value)); Assert.That(request.BrainSeed, Is.EqualTo(firstSeed));
                for (int i = 0; i < 60; i++) s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None);
                Assert.That(s.EnemyWave.SpawnedCount, Is.EqualTo(1)); Assert.That(s.Tanks.Last().EntityId, Is.EqualTo(2));
                Assert.That(s.Tanks.Last().Motor.Position, Is.EqualTo((Vector2)firstPosition.Value));
                s.SetPaused(true); yield return null;
                var oldViews = c.GetComponentsInChildren<EnemyTankView>(true); Assert.That(oldViews.Length, Is.EqualTo(1));
                s.SetPaused(false); s.AdvanceFrame(0, PlayerButtons.None);
                for (int i = 0; i < 16; i++) s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.A);
                s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.W | PlayerButtons.Fire);
                Assert.That(s.Map.Revision, Is.GreaterThan(0));
                s.Effects.PlayExplosion(s.Player.Position); s.SetPaused(true); yield return null;
                // Mutating the session-exposed working definition must not corrupt the private originals.
                s.GetWaveEnemyDefinition(2).hitPoints = 9;
                c.RestartSession();
                Assert.That(oldViews.All(v => !v.GetComponentInChildren<SpriteRenderer>(true).enabled), Is.True, "hide before delayed Destroy");
                Assert.That(s.Bullets.Active, Is.Empty); Assert.That(s.Tanks.All(t => t.Weapon.ActiveBullets == 0), Is.True);
                Assert.That(s.Effects.Active, Is.Empty); Assert.That(s.AdvanceFrame(1, PlayerButtons.None), Is.Zero);
                Assert.That(c.baseRenderer.sprite.name, Is.EqualTo("base.alive"));
                Assert.That(c.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.name.Contains("shield") || r.name.Contains("preparation")).All(r => !r.enabled), Is.True);
                yield return null;
                Assert.That(oldViews.All(v => v == null), Is.True);
            }
        }

        [UnityTest] public IEnumerator OutcomeNotificationRestart_ReplacesOnlyAfterSettledTickAndOldSessionNeverAdvancesAgain()
        {
            yield return Load(); var c = Controller; var old = c.Session;
            c.StartBattle();
            shotDefinition = Object.Instantiate(c.playerDefinition);
            var shot = new WeaponState(shotDefinition, old.Bullets, CombatTeam.Enemy, 999);
            shot.Step(new TankCommand(CardinalDirection.Down, true), old.Base.Position + Vector2.up * 10, CardinalDirection.Down);
            int outcomes = 0, deaths = 0;
            old.BaseDestroyed += () => deaths++;
            old.SessionEnded += () => { outcomes++; c.RestartSession(); c.RestartSession(); };
            yield return Until(() => c.Session != old);
            Assert.That(old.Phase, Is.EqualTo(SessionPhase.Lost)); Assert.That(outcomes, Is.EqualTo(1)); Assert.That(deaths, Is.EqualTo(1));
            Assert.That(c.Session.Phase, Is.EqualTo(SessionPhase.Ready)); Assert.That(c.Session.Clock.Tick, Is.Zero);
            long tick = old.Clock.Tick; yield return null;
            Assert.That(old.Clock.Tick, Is.EqualTo(tick)); Assert.That(old.AdvanceFrame(1, PlayerButtons.None), Is.Zero);
            Assert.That(c.GetComponentsInChildren<BulletView>(true).Length, Is.EqualTo(1));
        }
    }
}
#endif
