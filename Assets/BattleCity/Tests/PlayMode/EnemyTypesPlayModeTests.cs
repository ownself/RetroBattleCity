#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BattleCity.Tests
{
    [Category("BC301")]
    public sealed class EnemyTypesPlayModeTests
    {
        private Scene scene;
        private GameObject root;
        private LevelDefinition level;
        private TankDefinition definition;
        private Keyboard keyboard;

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (root != null) Object.Destroy(root);
            if (level != null) Object.Destroy(level);
            if (definition != null) Object.Destroy(definition);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }
        private static void Tick(ArenaSession session, int count)
        {
            session.SetPaused(false);
            session.AdvanceFrame(0, PlayerButtons.None);
            for (int i = 0; i < count; i++) session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None);
            session.SetPaused(true);
        }
        private static void Shoot(ArenaSession session, TankState enemy)
        {
            var value = ScriptableObject.CreateInstance<TankDefinition>();
            try
            {
                var weapon = new WeaponState(value, session.Bullets, CombatTeam.Player, 999);
                weapon.Step(new TankCommand(CardinalDirection.Up, false, true), enemy.Motor.Position - Vector2.up * 18, CardinalDirection.Up);
            }
            finally { Object.Destroy(value); } // WeaponState owns a numeric snapshot, not this asset.
        }

        [UnityTest] public IEnumerator Armor_HitPointsSelectFourPalettes_TracksDirectionsPauseDeathAndRebindStayReadOnly()
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            var roster = AssetDatabase.LoadAssetAtPath<EnemyBalanceDefinition>("Assets/BattleCity/Data/EnemyBalance.asset");
            definition = Object.Instantiate(roster.Get(EnemyKind.Armor));
            var catalog = AssetDatabase.LoadAssetAtPath<SpriteCatalog>("Assets/BattleCity/Data/SpriteCatalog.asset");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/BattleCity/Art/SpriteUnlit.mat");
            var session = new ArenaSession(level, definition);
            var enemy = session.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 104));
            root = new GameObject("BC301 armor view");
            var view = root.AddComponent<EnemyTankView>();
            view.Initialize(enemy, definition, catalog, material);
            var display = root.GetComponentInChildren<SpriteRenderer>();
            var expected = (string[])definition.damageSpritePrefixes.Clone();
            for (int stage = 0; stage < 4; stage++)
            {
                Assert.That(enemy.HitPoints, Is.EqualTo(4 - stage));
                for (int direction = 0; direction < 4; direction++)
                {
                    session.SetPaused(false);
                    session.SetTankCommand(enemy.EntityId, new TankCommand((CardinalDirection)direction));
                    for (int tick = 0; tick < 24; tick++)
                    {
                        session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None);
                        view.Render();
                        // Unity can return different managed wrappers for the same native Sprite entity.
                        Assert.That(display.sprite.GetEntityId(), Is.EqualTo(catalog.Get(expected[stage] + "." +
                            enemy.Motor.Facing.SpriteKey() + "." + enemy.Motor.TrackFrame).GetEntityId()));
                    }
                    Assert.That(enemy.Motor.Facing, Is.EqualTo((CardinalDirection)direction));
                }
                session.SetTankCommand(enemy.EntityId, new TankCommand(CardinalDirection.None));
                session.SetPaused(true);
                var sprite = display.sprite; var position = enemy.Motor.Position; long tickBefore = session.Clock.Tick;
                yield return null;
                session.AdvanceFrame(1, PlayerButtons.None); view.Render();
                Assert.That(display.sprite.GetEntityId(), Is.EqualTo(sprite.GetEntityId()));
                Assert.That(enemy.Motor.Position, Is.EqualTo(position));
                Assert.That(session.Clock.Tick, Is.EqualTo(tickBefore));
                Shoot(session, enemy); Tick(session, 1); view.Render();
                Assert.That(session.Score, Is.EqualTo(stage == 3 ? 400 : 0));
            }
            Assert.That(enemy.IsAlive, Is.False);
            Assert.That(display.enabled, Is.False); Assert.That(display.sprite, Is.Null);
            var replacement = session.AddTank(definition, CombatTeam.Enemy, new Vector2(152, 152));
            view.Clear(); view.Initialize(replacement, definition, catalog, material);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(1));
            Assert.That(display.enabled, Is.True);
            Assert.That(display.sprite.name, Does.StartWith(expected[0]));
            Assert.That(root.GetComponentsInChildren<Collider2D>(), Is.Empty);
        }

        [UnityTest] public IEnumerator Stage01_FourNaturalTypesRender_RealUpdateAdvances_DeathScoresOnce_RestartResetsScoreAndRoster()
        {
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/BattleCity/Scenes/Stage01.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
            var session = controller.Session;
            session.SetFocus(true); Assert.That(session.StartBattle(), Is.True);
            session.AdvanceFrame(0, PlayerButtons.None);
            session.SetPaused(true);
            // Controlled arrival: same real wave/AI/movement, discard projectiles between ticks to avoid random base loss.
            for (int tick = 0; tick < 601; tick++) { Tick(session, 1); session.Bullets.Clear(); }
            yield return null;
            var enemies = session.Tanks.Where(t => t.Team == CombatTeam.Enemy).ToArray();
            Assert.That(enemies.Length, Is.EqualTo(4));
            CollectionAssert.AreEqual(new[] { "enemy.basic", "enemy.fast", "enemy.power", "enemy.armor" }, enemies.Select(e => e.DefinitionId));
            Assert.That(session.EnemyWave.PendingCount, Is.EqualTo(16));
            Assert.That(session.AliveEnemyCount + session.EnemyWave.ReservedCount, Is.EqualTo(4));
            foreach (var enemy in enemies)
            {
                var view = controller.GetComponentsInChildren<EnemyTankView>().Single(v => v.name == "Enemy " + enemy.EntityId + " - display only");
                var display = view.GetComponentInChildren<SpriteRenderer>();
                Assert.That(display.enabled, Is.True);
                var definition = session.GetWaveEnemyDefinition(enemy.EntityId);
                Assert.That(enemy.Weapon.Parameters.BulletSpeedPixelsPerSecond,
                    Is.EqualTo(enemy.DefinitionId == "enemy.power" ? 240 : 120), "Only the Power tank starts with fast bullets.");
                string prefix = enemy.CarriesPickup && session.Clock.Tick / 8 % 2 == 1
                    ? PickupDefinition.CarrierPrefix(definition) : definition.spritePrefix;
                var expected = controller.sprites.Get(prefix + "." + enemy.Motor.Facing.SpriteKey() + "." + enemy.Motor.TrackFrame);
                Assert.That(display.sprite.GetEntityId(), Is.EqualTo(expected.GetEntityId()));
            }
            long before = session.Clock.Tick; int frame = Time.frameCount;
            session.SetPaused(false);
            float deadline = Time.realtimeSinceStartup + 3;
            bool observedFire = false;
            while ((session.Clock.Tick < before + 12 || !observedFire) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                observedFire |= enemies.Any(e => e.Weapon.CooldownRemainingTicks > 0);
                session.Bullets.Clear(); // Keep the controlled arrival's combat isolation during this observation too.
            }
            session.SetPaused(true); yield return null;
            Assert.That(Time.frameCount, Is.GreaterThan(frame));
            Assert.That(session.Clock.Tick, Is.GreaterThanOrEqualTo(before + 12));
            Assert.That(observedFire, Is.True, "Observe actual firing, not an arbitrary endpoint inside a 15-tick cooldown.");
            session.Bullets.Clear();
            Shoot(session, enemies[0]); Tick(session, 1); yield return null; yield return null;
            Assert.That(enemies[0].IsAlive, Is.False);
            Assert.That(session.Score, Is.EqualTo(100));
            Assert.That(session.DefeatedEnemyCount, Is.EqualTo(1));
            Assert.That(controller.GetComponentsInChildren<EnemyTankView>().Length, Is.EqualTo(3));
            Debug.Log($"BC301 Stage01 natural mixed types=4 frame={frame}->{Time.frameCount} tick={before}->{session.Clock.Tick} score={session.Score} pending={session.EnemyWave.PendingCount}");
            controller.RestartSession();
            var fresh = controller.Session;
            Assert.That(fresh, Is.Not.SameAs(session));
            Assert.That(fresh.Score, Is.Zero); Assert.That(fresh.DefeatedEnemyCount, Is.Zero);
            Assert.That(fresh.Phase, Is.EqualTo(SessionPhase.Ready));
            Assert.That(fresh.EnemyWave.TotalCount, Is.EqualTo(20));
            fresh.SetFocus(true); fresh.StartBattle(); fresh.AdvanceFrame(0, PlayerButtons.None);
            Tick(fresh, 61); yield return null;
            Assert.That(fresh.Tanks.Single(t => t.Team == CombatTeam.Enemy).DefinitionId, Is.EqualTo("enemy.basic"));
            Assert.That(fresh.Score, Is.Zero);
        }
    }
}
#endif
