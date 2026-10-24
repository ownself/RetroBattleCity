using System.Linq;
using BattleCity.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleCity.Tests
{
    public sealed class ArenaHudTests
    {
        private LevelDefinition level;
        private TankDefinition tank;
        private EnemyWaveDefinition wave;
        [SetUp] public void SetUp()
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            tank = ScriptableObject.CreateInstance<TankDefinition>();
            tank.definitionId = "hud.enemy"; tank.moveSpeedPixelsPerSecond = .001f; tank.bulletSpeedPixelsPerSecond = 6000;
            level.playerSpawnPixels = new Vector2Int(24, 24); level.baseCenterPixels = new Vector2Int(104, 104);
            level.enemySpawnPixels = new[] { new Vector2Int(160, 160) };
            level.enemySequence = new[] { tank.definitionId };
            wave = ScriptableObject.CreateInstance<EnemyWaveDefinition>(); wave.tankDefinitions = new[] { tank };
            wave.preparationTicks = 1; wave.spawnIntervalTicks = 1;
        }
        [TearDown] public void TearDown()
        { Object.DestroyImmediate(wave); Object.DestroyImmediate(tank); Object.DestroyImmediate(level); }
        private ArenaSession Session(int lives = 3) => new ArenaSession(level, tank, wave, true, new PlayerLifeParameters(lives, 1, 1), true);
        private static void Tick(ArenaSession s) => s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None);
        private static void Start(ArenaSession s) { s.StartBattle(); s.AdvanceFrame(0, PlayerButtons.None); }
        private void Shoot(ArenaSession s, Vector2 position, CombatTeam team)
        {
            var weapon = new WeaponState(tank, s.Bullets, team, 999);
            Assert.That(weapon.Step(new TankCommand(CardinalDirection.Down, true), position + Vector2.up * 18, CardinalDirection.Down), Is.True);
        }

        [Test] public void RemainingEnemies_ReservationBlockedMaterializationAndDeathOnlySpendOne()
        {
            var s = Session(); Assert.That(ArenaHud.RemainingEnemies(s), Is.EqualTo(1));
            Start(s); Tick(s);
            Assert.That(s.EnemyWave.ReservedCount, Is.EqualTo(1));
            Assert.That(ArenaHud.RemainingEnemies(s), Is.EqualTo(1), "Pending already includes the reservation");
            // Block the reservation through the existing wave's occupancy seam, without changing its counters.
            Assert.That(s.EnemyWave.Step(0, _ => true, _ => { Assert.Fail("must not create"); return false; }), Is.False);
            Assert.That(ArenaHud.RemainingEnemies(s), Is.EqualTo(1));
            Tick(s);
            Assert.That(s.EnemyWave.PendingCount, Is.Zero); Assert.That(s.AliveEnemyCount, Is.EqualTo(1));
            Assert.That(ArenaHud.RemainingEnemies(s), Is.EqualTo(1), "materialization is not a kill");
            Shoot(s, s.Tanks.Last().Motor.Position, CombatTeam.Player); Tick(s);
            Assert.That(s.Phase, Is.EqualTo(SessionPhase.Won)); Assert.That(ArenaHud.RemainingEnemies(s), Is.Zero);
        }

        [Test] public void Lives_TotalIncludesCurrent_DeathSpendsAndRespawnDoesNotSpendAgain()
        {
            var s = Session(); Start(s);
            Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(3));
            Shoot(s, s.Player.Position, CombatTeam.Enemy); Tick(s);
            Assert.That(s.PlayerLife.IsWaitingForRespawn, Is.True); Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(2));
            Tick(s);
            Assert.That(s.PlayerLife.IsAlive, Is.True); Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(2));
        }

        [TestCase(SessionPhase.Ready, "READY  ENTER START")]
        [TestCase(SessionPhase.Playing, "")]
        [TestCase(SessionPhase.Paused, "PAUSED  ESC RESUME")]
        [TestCase(SessionPhase.Won, "WON")]
        [TestCase(SessionPhase.Lost, "LOST")]
        public void PhaseCaption_AllSessionPhases(SessionPhase phase, string caption)
            => Assert.That(ArenaHud.PhaseCaption(phase), Is.EqualTo(caption));

        [Test] public void Stage01_SavedHudUsesNativeNumbersAndOnlyMarginGeometry()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(Stage01Authoring.ScenePath, OpenSceneMode.Additive);
            try
            {
                var c = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
                var hud = c.hud; Assert.That(hud, Is.Not.Null); Assert.DoesNotThrow(hud.ValidateBindings);
                Assert.That(hud.sprites, Is.SameAs(c.sprites));
                Assert.That(hud.hudCanvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
                Assert.That(hud.hudCanvas.sortingLayerName, Is.EqualTo("HUD"));
                Assert.That(hud.transform.position, Is.EqualTo(new Vector3(-1, -1, 0)));
                Assert.That(hud.transform.localScale, Is.EqualTo(Vector3.one / 16));
                Assert.That(hud.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(256, 240)));
                Assert.That(hud.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Null);
                var map = new Rect(16, 16, 208, 208);
                foreach (var graphic in hud.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                {
                    Assert.That(graphic.GetComponent<CanvasRenderer>(), Is.Not.Null, graphic.name);
                    var rect = graphic.rectTransform;
                    var bounds = new Rect(rect.anchoredPosition, rect.sizeDelta);
                    Assert.That(bounds.Overlaps(map), Is.False, graphic.name);
                    Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(0)); Assert.That(bounds.xMax, Is.LessThanOrEqualTo(256));
                    Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(0)); Assert.That(bounds.yMax, Is.LessThanOrEqualTo(240));
                    Assert.That(graphic.raycastTarget, Is.False, graphic.name);
                }
                Assert.That(hud.stageDigits.Last().sprite, Is.SameAs(c.sprites.Get("hud.digit.1")));
                Assert.That(hud.enemyDigits[2].sprite, Is.SameAs(c.sprites.Get("hud.digit.2")));
                Assert.That(hud.enemyDigits[3].sprite, Is.SameAs(c.sprites.Get("hud.digit.0")));
                Assert.That(hud.lifeDigits.Last().sprite, Is.SameAs(c.sprites.Get("hud.digit.3")));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        [TestCase("Assets/BattleCity/Scenes/CombatArena.unity")]
        [TestCase("Assets/BattleCity/Scenes/PixelBaseline.unity")]
        public void LegacyScenes_DoNotAcquireHud(string path)
        {
            var previous = SceneManager.GetActiveScene(); var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try { Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaHud>(true)), Is.Empty); }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
