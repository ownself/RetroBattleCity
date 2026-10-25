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
    public sealed class SinglePlayerFlowPlayModeTests
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
        private static IEnumerator Until(System.Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 4;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "bounded real Update observation");
        }
        private IEnumerator Press(params Key[] keys)
        {
            Keys(); yield return null; Keys(keys); yield return null;
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

        [UnityTest] public IEnumerator VictoryResultProjectsFrozenCategoriesAndCompletion()
        {
            Keys();
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/BattleCity/Scenes/SinglePlayer.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var c = Controller; c.enabled = false;
            var flow = c.singlePlayerFlow;
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            var wave = ScriptableObject.CreateInstance<EnemyWaveDefinition>();
            shotDefinition = Object.Instantiate(c.playerDefinition);
            shotDefinition.definitionId = "enemy.basic.prototype";
            shotDefinition.moveSpeedPixelsPerSecond = .001f;
            shotDefinition.bulletSpeedPixelsPerSecond = 6000;
            shotDefinition.score = 100;
            level.playerSpawnPixels = new Vector2Int(24, 24);
            level.enemySpawnPixels = new[] { new Vector2Int(160, 160) };
            level.enemySequence = new[] { shotDefinition.definitionId };
            wave.tankDefinitions = new[] { shotDefinition }; wave.preparationTicks = 1;
            var s = new ArenaSession(level, shotDefinition, wave, false, new PlayerLifeParameters(3, 1, 1), true);
            try
            {
                s.StartBattle(); s.AdvanceFrame(0, PlayerButtons.None);
                s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None);
                s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None);
                var enemy = s.Tanks.Last();
                var kill = new WeaponState(shotDefinition, s.Bullets, CombatTeam.Player, 998);
                kill.Step(new TankCommand(CardinalDirection.Down, true), enemy.Motor.Position + Vector2.up * 18, CardinalDirection.Down);
                s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None);
                Assert.That(s.Phase, Is.EqualTo(SessionPhase.Won));
                flow.State.Sample(PlayerButtons.Start, true, SessionPhase.Ready);
                flow.State.Sample(PlayerButtons.None, true, SessionPhase.Ready);
                flow.State.Sample(PlayerButtons.Start, true, SessionPhase.Ready);
                flow.State.Sample(PlayerButtons.None, true, s.Phase);
                flow.Render(s, 1);
                Assert.That(flow.lines[0].Text, Is.EqualTo("VICTORY"));
                Assert.That(flow.lines[1].Text, Is.EqualTo("BASIC 1  100"));
                Assert.That(flow.lines[6].Text, Is.EqualTo("TOTAL 100"));
                flow.State.Sample(PlayerButtons.Start, true, s.Phase);
                flow.Render(s, 1);
                Assert.That(flow.State.Screen, Is.EqualTo(SinglePlayerScreen.Complete));
                Assert.That(flow.heading.Text, Is.EqualTo("STAGE COMPLETE"));
                Assert.That(flow.lines[0].Text, Is.EqualTo("END OF CURRENT SINGLE STAGE"));
            }
            finally { s.Bullets.Clear(); s.Effects.Clear(); Object.Destroy(level); Object.Destroy(wave); }
        }

        [UnityTest] public IEnumerator RealKeyboard_TitleIntroBattlePauseBaseLossResultsReplayAndTitle()
        {
            Keys();
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/BattleCity/Scenes/SinglePlayer.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var c = Controller;
            c.SendMessage("OnApplicationFocus", true);
            var flow = c.singlePlayerFlow;
            Assert.That(flow.State.Screen, Is.EqualTo(SinglePlayerScreen.Title));
            Assert.That(flow.screenCanvas.enabled, Is.True);
            Assert.That(flow.titleArt.sprite.rect.size, Is.EqualTo(new Vector2(192, 72)));
            Assert.That(c.hud.hudCanvas.enabled, Is.False);
            yield return Press(Key.Enter, Key.Space, Key.D);
            yield return Until(() => flow.State.Screen == SinglePlayerScreen.StageIntro);
            var s = c.Session;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(s.Clock.Tick, Is.Zero);
            Assert.That(flow.State.Screen, Is.EqualTo(SinglePlayerScreen.StageIntro));
            int frame = Time.frameCount;
            yield return Press(Key.Enter, Key.Space, Key.D);
            yield return Until(() => s.Clock.Tick > 0);
            Assert.That(Time.frameCount, Is.GreaterThan(frame));
            Assert.That(s.Player.Position, Is.EqualTo(new Vector2(72, 8)));
            Assert.That(s.PlayerWeapon.ActiveBullets, Is.Zero);
            Assert.That(flow.screenCanvas.enabled, Is.False);
            Assert.That(c.hud.hudCanvas.enabled, Is.True);
            yield return Press(Key.Escape);
            yield return Until(() => s.Phase == SessionPhase.Paused);
            long paused = s.Clock.Tick;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(s.Clock.Tick, Is.EqualTo(paused));
            yield return Press(Key.Escape);
            yield return Until(() => s.Phase == SessionPhase.Playing);
            Keys(); yield return null;
            shotDefinition = Object.Instantiate(c.playerDefinition); shotDefinition.bulletSpeedPixelsPerSecond = 6000;
            var shot = new WeaponState(shotDefinition, s.Bullets, CombatTeam.Enemy, 999);
            shot.Step(new TankCommand(CardinalDirection.Down, true), s.Base.Position + Vector2.up * 10, CardinalDirection.Down);
            yield return Until(() => flow.State.Screen == SinglePlayerScreen.Results);
            Assert.That(flow.DisplayedResult, Is.SameAs(s.Result));
            Assert.That(flow.lines[0].Text, Is.EqualTo("DEFEAT"));
            Assert.That(s.Result.FailureReason, Is.EqualTo(SessionFailureReason.BaseDestroyed));
            yield return Press(Key.Enter);
            yield return Until(() => flow.State.Screen == SinglePlayerScreen.GameOver);
            Assert.That(flow.lines[0].Text, Is.EqualTo("BASE DESTROYED"));
            yield return Press(Key.Enter);
            yield return Until(() => flow.State.Screen == SinglePlayerScreen.StageIntro);
            Assert.That(c.Session, Is.Not.SameAs(s));
            Assert.That(c.Session.Score, Is.Zero);
            Assert.That(c.Session.Result, Is.Null);
            Assert.That(c.Session.Base.IsAlive, Is.True);
            Assert.That(c.Session.Clock.Tick, Is.Zero);
            yield return Press(Key.Enter);
            yield return Until(() => c.Session.Clock.Tick > 0);
            // Repeat the production failure path, then choose Title with a held navigation key.
            s = c.Session;
            shot = new WeaponState(shotDefinition, s.Bullets, CombatTeam.Enemy, 999);
            shot.Step(new TankCommand(CardinalDirection.Down, true), s.Base.Position + Vector2.up * 10, CardinalDirection.Down);
            yield return Until(() => flow.State.Screen == SinglePlayerScreen.Results);
            yield return Press(Key.Enter);
            yield return Until(() => flow.State.Screen == SinglePlayerScreen.GameOver);
            yield return Press(Key.DownArrow);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(flow.State.Selection, Is.EqualTo(1));
            yield return Press(Key.Enter);
            yield return Until(() => flow.State.Screen == SinglePlayerScreen.Title);
            Assert.That(flow.titleArt.enabled, Is.True);
            Assert.That(c.Session.Clock.Tick, Is.Zero);
            Debug.Log($"BC400 real Update frames {frame}->{Time.frameCount}; base loss, results, replay, title verified");
        }
    }
}
#endif
