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
    public sealed class ArenaHudPlayModeTests
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
            yield return null; Controller.SendMessage("OnApplicationFocus", true);
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
            shotDefinition = null; LogAssert.NoUnexpectedReceived();
        }

        private void Shoot(ArenaSession s, Vector2 target, CombatTeam team)
        {
            shotDefinition = Object.Instantiate(Controller.playerDefinition); shotDefinition.bulletSpeedPixelsPerSecond = 6000;
            var shot = new WeaponState(shotDefinition, s.Bullets, team, 999);
            Assert.That(shot.Step(new TankCommand(CardinalDirection.Down, true), target + Vector2.up * WeaponState.MuzzleOffsetPixels, CardinalDirection.Down), Is.True);
        }

        [UnityTest] public IEnumerator RealUpdate_PhasesCountsTerminalAndPhysicalRestartRebindImmediately()
        {
            yield return Load(); var c = Controller; var s = c.Session; var hud = c.hud;
            Assert.That(hud.DisplayedSession, Is.SameAs(s)); Assert.That(hud.DisplayedStage, Is.EqualTo(1));
            Assert.That(hud.DisplayedEnemies, Is.EqualTo(20)); Assert.That(hud.DisplayedLives, Is.EqualTo(3));
            Assert.That(hud.phaseCaption.Text, Is.EqualTo("READY  ENTER START"));
            Canvas.ForceUpdateCanvases();
            Assert.That(hud.phaseCaption.canvasRenderer.GetMesh().vertexCount, Is.GreaterThan(0), "caption must submit real pixel geometry");
            Keys(Key.Enter); yield return Until(() => s.Clock.Tick > 0);
            Assert.That(hud.phaseCaption.Text, Is.Empty);
            Assert.That(hud.DisplayedEnemies, Is.EqualTo(20)); Assert.That(s.EnemyWave.ReservedCount, Is.EqualTo(1));
            Keys(); yield return null; Keys(Key.Escape); yield return Until(() => s.Phase == SessionPhase.Paused);
            Assert.That(hud.phaseCaption.Text, Is.EqualTo("PAUSED  ESC RESUME"));
            Keys(); yield return null; Keys(Key.Escape); yield return Until(() => s.Phase == SessionPhase.Playing);
            Keys(); Shoot(s, s.Base.Position, CombatTeam.Enemy); yield return Until(() => s.Phase == SessionPhase.Lost);
            Assert.That(hud.phaseCaption.Text, Is.EqualTo("LOST")); Assert.That(hud.restartCaption.Text, Is.EqualTo("R RESTART"));
            c.level.stageNumber = 9; // HUD, like restart, must use the captured initial stage, not a mutable author asset.
            try
            {
                Keys(Key.R); yield return Until(() => c.Session != s);
                Assert.That(hud.DisplayedSession, Is.SameAs(c.Session)); Assert.That(hud.phaseCaption.Text, Is.EqualTo("READY  ENTER START"));
                Assert.That(hud.DisplayedEnemies, Is.EqualTo(20)); Assert.That(hud.DisplayedLives, Is.EqualTo(3)); Assert.That(hud.DisplayedStage, Is.EqualTo(1));
                Assert.That(hud.lifeDigits.Last().sprite.name, Is.EqualTo("hud.digit.3"));
                s.SetFocus(false); s.SetPaused(false); s.AdvanceFrame(1, PlayerButtons.Start);
                yield return null;
                Assert.That(hud.DisplayedSession, Is.SameAs(c.Session)); Assert.That(hud.phaseCaption.Text, Does.StartWith("READY"));
            }
            finally { c.level.stageNumber = 1; }
            Debug.Log($"BC207 realUpdate frame={Time.frameCount} oldTick={s.Clock.Tick} newTick={c.Session.Clock.Tick} HUD={hud.DisplayedStage}/{hud.DisplayedEnemies}/{hud.DisplayedLives}");
        }

        [UnityTest] public IEnumerator Projection_DeathWaitRespawnAndWonShowSettledNumbersWithoutAdvancingClock()
        {
            yield return Load(); var c = Controller; var hud = c.hud;
            c.enabled = false;
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            var tank = Object.Instantiate(c.playerDefinition);
            var wave = ScriptableObject.CreateInstance<EnemyWaveDefinition>();
            try
            {
                level.playerSpawnPixels = new Vector2Int(24,24); level.enemySpawnPixels = new[] { new Vector2Int(160,160) };
                tank.definitionId = "hud.enemy"; tank.moveSpeedPixelsPerSecond = .001f; tank.bulletSpeedPixelsPerSecond = 6000;
                level.enemySequence = new[] { tank.definitionId }; wave.tankDefinitions = new[] { tank }; wave.preparationTicks = 1;
                var s = new ArenaSession(level, tank, wave, false, new PlayerLifeParameters(3,1,1), true);
                s.StartBattle(); s.AdvanceFrame(0, PlayerButtons.None);
                var shot = new WeaponState(tank, s.Bullets, CombatTeam.Enemy, 999);
                shot.Step(new TankCommand(CardinalDirection.Down, true), s.Player.Position + Vector2.up * 18, CardinalDirection.Down);
                s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None); hud.Render(s, 12);
                Assert.That(hud.DisplayedLives, Is.EqualTo(2)); Assert.That(s.PlayerLife.IsWaitingForRespawn, Is.True);
                s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None); hud.Render(s, 12);
                Assert.That(hud.DisplayedLives, Is.EqualTo(2)); Assert.That(s.PlayerLife.IsAlive, Is.True);
                Assert.That(hud.DisplayedEnemies, Is.EqualTo(1));
                var enemy = s.Tanks.Last(); var kill = new WeaponState(tank, s.Bullets, CombatTeam.Player, 998);
                kill.Step(new TankCommand(CardinalDirection.Down, true), enemy.Motor.Position + Vector2.up * 18, CardinalDirection.Down);
                s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None);
                long tick = s.Clock.Tick; hud.Render(s, 12); hud.Render(s, 12);
                Assert.That(s.Clock.Tick, Is.EqualTo(tick)); Assert.That(s.Phase, Is.EqualTo(SessionPhase.Won));
                Assert.That(hud.phaseCaption.Text, Is.EqualTo("WON")); Assert.That(hud.DisplayedEnemies, Is.Zero);
                Assert.That(hud.stageDigits[2].sprite.name, Is.EqualTo("hud.digit.1")); Assert.That(hud.stageDigits[3].sprite.name, Is.EqualTo("hud.digit.2"));
                var exhausted = new ArenaSession(level, tank, wave, false, new PlayerLifeParameters(1,1,1), true);
                exhausted.StartBattle(); exhausted.AdvanceFrame(0, PlayerButtons.None);
                var finalDeath = new WeaponState(tank, exhausted.Bullets, CombatTeam.Enemy, 997);
                finalDeath.Step(new TankCommand(CardinalDirection.Down, true), exhausted.Player.Position + Vector2.up * 18, CardinalDirection.Down);
                exhausted.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None); hud.Render(exhausted, 12);
                Assert.That(exhausted.Phase, Is.EqualTo(SessionPhase.Lost)); Assert.That(hud.DisplayedLives, Is.Zero);
                Assert.That(hud.lifeDigits.Last().sprite.name, Is.EqualTo("hud.digit.0")); Assert.That(hud.phaseCaption.Text, Is.EqualTo("LOST"));
                yield return null;
            }
            finally { Object.Destroy(wave); Object.Destroy(tank); Object.Destroy(level); }
        }

        [UnityTest] public IEnumerator DisableDestroy_ClearsSessionReferenceAndCanvasWithoutSubscriptions()
        {
            yield return Load(); var c = Controller; var hud = c.hud;
            hud.enabled = false; Assert.That(hud.DisplayedSession, Is.Null); Assert.That(hud.hudCanvas.enabled, Is.False);
            hud.enabled = true; yield return null;
            Assert.That(hud.hudCanvas.enabled, Is.True); Assert.That(hud.DisplayedSession, Is.SameAs(c.Session));
            c.enabled = false; Assert.That(hud.DisplayedSession, Is.Null); Assert.That(hud.hudCanvas.enabled, Is.False);
            c.enabled = true; c.RestartSession(); Assert.That(hud.DisplayedSession, Is.SameAs(c.Session));
            Object.Destroy(c); yield return null;
            Assert.That(hud.DisplayedSession, Is.Null); Assert.That(hud.hudCanvas.enabled, Is.False);
        }
    }
}
#endif
