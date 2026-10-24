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
    public sealed class EnemyWaveIntegrationPlayModeTests
    {
        private Scene scene;
        private Keyboard keyboard;
        private ArenaController Controller => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
        private SpriteRenderer Hint => Controller.GetComponentsInChildren<SpriteRenderer>().SingleOrDefault(r => r.name == "Enemy preparation - display only");
        private EnemyTankView View(long id) => Controller.GetComponentsInChildren<EnemyTankView>().Single(v => v.name == "Enemy " + id + " - display only");
        private static TankState Enemy(ArenaSession s) => s.Tanks.First(t => t.Team == CombatTeam.Enemy && t.IsAlive);
        private SpriteRenderer Display(TankState enemy) => View(enemy.EntityId).GetComponentInChildren<SpriteRenderer>();
        private IEnumerator Load()
        {
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/BattleCity/Scenes/Stage01.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            Controller.Session.SetFocus(true); Controller.Session.StartBattle();
            Controller.Session.AdvanceFrame(0, PlayerButtons.None); Controller.Session.SetPaused(true);
        }
        private static void Tick(ArenaSession s, int count)
        {
            s.SetPaused(false);
            if (s.EnableSessionLifecycle) s.AdvanceFrame(0, PlayerButtons.None);
            for (int i = 0; i < count; i++) s.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None);
            s.SetPaused(true);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator DefaultStage01_RealUpdatesSpawnMoveFireAndRenderUpToCap_PauseFreezesPreparationAndInterval()
        {
            yield return Load();
            var c = Controller; var s = c.Session; var w = s.EnemyWave;
            Assert.That(c.enemyWaveDefinition, Is.Not.Null); Assert.That(w.TotalCount, Is.EqualTo(20));
            Assert.That(w.MaxActiveEnemies, Is.EqualTo(4));
            int firstFrame = Time.frameCount; long firstTick = s.Clock.Tick;
            s.SetPaused(false);
            float deadline = Time.realtimeSinceStartup + 3;
            while (w.ReservedCount == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            s.SetPaused(true); yield return null;
            Assert.That(w.ReservedCount, Is.EqualTo(1)); Assert.That(w.SpawnedCount, Is.Zero);
            Assert.That(Hint, Is.Not.Null); Assert.That(Hint.enabled, Is.True);
            Assert.That(Hint.sprite.name, Does.StartWith("effect.spawn."));
            Assert.That(PixelGrid.ToMapPixels(Hint.transform.position), Is.EqualTo((Vector2)w.Reservation.Value.PositionPixels));
            int prep = w.PreparationRemainingTicks; uint random = w.RandomState; long tick = s.Clock.Tick;
            var hintSprite = Hint.sprite; int pausedFrame = Time.frameCount;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(Time.frameCount, Is.GreaterThan(pausedFrame)); Assert.That(s.Clock.Tick, Is.EqualTo(tick));
            Assert.That(w.PreparationRemainingTicks, Is.EqualTo(prep)); Assert.That(w.RandomState, Is.EqualTo(random));
            Assert.That(Hint.sprite, Is.SameAs(hintSprite));
            s.SetPaused(false); deadline = Time.realtimeSinceStartup + 4;
            while (w.SpawnedCount == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(w.SpawnedCount, Is.EqualTo(1));
            var enemy = Enemy(s); var birth = enemy.Motor.Position;
            deadline = Time.realtimeSinceStartup + 3;
            while ((enemy.Motor.Position == birth || enemy.Weapon.CooldownRemainingTicks == 0) && Time.realtimeSinceStartup < deadline) yield return null;
            s.SetPaused(true); yield return null;
            Assert.That(enemy.Motor.Position, Is.Not.EqualTo(birth)); Assert.That(enemy.Weapon.CooldownRemainingTicks, Is.GreaterThan(0));
            Assert.That(s.Bullets.Active.Any(b => b.OwnerId == enemy.EntityId && b.Team == CombatTeam.Enemy), Is.True);
            var renderer = Display(enemy); Assert.That(renderer.enabled, Is.True);
            Assert.That(renderer.sprite, Is.SameAs(c.sprites.Get("tank.white.enemy.basic." + enemy.Motor.Facing.SpriteKey() + "." + enemy.Motor.TrackFrame)));
            Assert.That(PixelGrid.ToMapPixels(renderer.transform.position), Is.EqualTo(new Vector2(Mathf.Round(enemy.Motor.Position.x), Mathf.Round(enemy.Motor.Position.y))));
            Assert.That(renderer.sharedMaterial, Is.SameAs(c.playerRenderer.sharedMaterial));
            Assert.That(Hint == null || !Hint.enabled, Is.True);
            int interval = w.IntervalRemainingTicks, cooldown = enemy.Weapon.CooldownRemainingTicks;
            var position = enemy.Motor.Position; var sprite = renderer.sprite;
            tick = s.Clock.Tick; long dropped = s.Clock.DroppedTicks; pausedFrame = Time.frameCount;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(Time.frameCount, Is.GreaterThan(pausedFrame)); Assert.That(s.Clock.Tick, Is.EqualTo(tick));
            Assert.That(w.IntervalRemainingTicks, Is.EqualTo(interval)); Assert.That(enemy.Weapon.CooldownRemainingTicks, Is.EqualTo(cooldown));
            Assert.That(enemy.Motor.Position, Is.EqualTo(position)); Assert.That(renderer.sprite, Is.SameAs(sprite));
            s.SetPaused(false); deadline = Time.realtimeSinceStartup + 2;
            while (s.Clock.Tick == tick && Time.realtimeSinceStartup < deadline) yield return null;
            s.SetPaused(true);
            long resumed = s.Clock.Tick - tick;
            Assert.That(resumed, Is.InRange(1, LogicClock.MaxTicksPerFrame)); Assert.That(s.Clock.DroppedTicks, Is.EqualTo(dropped));
            Assert.That(w.IntervalRemainingTicks, Is.EqualTo(interval - resumed));
            s.SetPaused(false); deadline = Time.realtimeSinceStartup + 20;
            while (s.AliveEnemyCount < 4 && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(s.AliveEnemyCount + w.ReservedCount, Is.LessThanOrEqualTo(4));
                yield return null;
            }
            s.SetPaused(true); yield return null;
            Assert.That(s.AliveEnemyCount, Is.EqualTo(4)); Assert.That(w.SpawnedCount, Is.EqualTo(4));
            Assert.That(c.GetComponentsInChildren<EnemyTankView>().Length, Is.EqualTo(4));
            foreach (var t in s.Tanks.Where(t => t.Team == CombatTeam.Enemy && t.IsAlive))
            {
                Assert.That(Display(t).enabled, Is.True);
                Assert.That(s.Map.CanOccupy(t.Motor.Position), Is.True);
                foreach (var other in s.Tanks.Where(other => other != t && other.IsAlive)) Assert.That(t.Bounds.Overlaps(other.Bounds), Is.False);
            }
            Debug.Log($"BC203 realStage01 frame={firstFrame}->{Time.frameCount} tick={firstTick}->{s.Clock.Tick} spawned={w.SpawnedCount} pending={w.PendingCount} alive={s.AliveEnemyCount} reserved={w.ReservedCount} resumed={resumed} dropped={s.Clock.DroppedTicks} views={c.GetComponentsInChildren<EnemyTankView>().Length}");
        }

        [UnityTest] public IEnumerator DefaultStage01_ActualSharedProjectileDeathClearsOnlyItsViewAndAllowsReplacement()
        {
            yield return Load(); var c = Controller; var s = c.Session;
            Tick(s, 61); yield return null;
            var victim = Enemy(s); var oldView = View(victim.EntityId); var oldDisplay = Display(victim);
            Assert.That(oldDisplay.enabled, Is.True);
            var definition = s.GetWaveEnemyDefinition(victim.EntityId);
            var weapon = new WeaponState(definition, s.Bullets, CombatTeam.Player, 999);
            weapon.Step(new TankCommand(CardinalDirection.Up, true), victim.Motor.Position - new Vector2(0, 18), CardinalDirection.Up);
            Tick(s, 1);
            Assert.That(victim.IsAlive, Is.False); Assert.That(weapon.ActiveBullets, Is.Zero);
            yield return null; yield return null;
            Assert.That(oldView == null, Is.True); Assert.That(oldDisplay == null, Is.True);
            Assert.That(c.GetComponentsInChildren<EnemyTankView>(), Is.Empty);
            Assert.That(s.EnemyWave.SpawnedCount, Is.EqualTo(1)); Assert.That(s.EnemyWave.PendingCount, Is.EqualTo(19));
            Tick(s, 179); yield return null;
            Assert.That(s.EnemyWave.SpawnedCount, Is.EqualTo(2)); Assert.That(s.EnemyWave.PendingCount, Is.EqualTo(18));
            Assert.That(Enemy(s).EntityId, Is.Not.EqualTo(victim.EntityId)); Assert.That(Display(Enemy(s)).enabled, Is.True);
            Assert.That(c.GetComponentsInChildren<EnemyTankView>().Length, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator DisableReenableAndDestroy_ClearViewsHintsAndBulletLeases_OnlyRebindCurrentLivingTanks()
        {
            yield return Load(); var c = Controller; var s = c.Session;
            Tick(s, 70); yield return null;
            var enemy = Enemy(s); var oldView = View(enemy.EntityId);
            Assert.That(enemy.Weapon.ActiveBullets, Is.GreaterThan(0));
            c.enabled = false;
            Assert.That(s.Bullets.Active, Is.Empty); Assert.That(enemy.Weapon.ActiveBullets, Is.Zero);
            Assert.That(s.PlayerWeapon.ActiveBullets, Is.Zero);
            yield return null;
            Assert.That(oldView == null, Is.True); Assert.That(c.GetComponentsInChildren<EnemyTankView>(), Is.Empty);
            c.enabled = true; yield return null;
            Assert.That(s.Clock.Paused, Is.True); Assert.That(Display(enemy).enabled, Is.True);
            Tick(s, 111); yield return null; // Tick181 starts the next preparation.
            Assert.That(Hint.enabled, Is.True);
            c.enabled = false; yield return null;
            Assert.That(Hint == null, Is.True); Assert.That(c.GetComponentsInChildren<EnemyTankView>(), Is.Empty);
            c.enabled = true; yield return null;
            Assert.That(Hint.enabled, Is.True); Assert.That(s.Clock.Paused, Is.True);
            var root = c.gameObject;
            Object.Destroy(root); yield return null;
            Assert.That(root == null, Is.True); Assert.That(s.Bullets.Active, Is.Empty);
            Assert.That(s.Tanks.All(t => t.Weapon.ActiveBullets == 0), Is.True);
        }

        [UnityTest] public IEnumerator DestroyHostComponent_DoesNotStrandSessionBoundDisplayChildrenOrLeases()
        {
            yield return Load(); var c = Controller; var s = c.Session; var root = c.gameObject;
            Tick(s, 190); yield return null;
            Assert.That(c.GetComponentsInChildren<EnemyTankView>().Length, Is.EqualTo(1));
            Assert.That(Hint.enabled, Is.True);
            Object.Destroy(c); yield return null; yield return null;
            Assert.That(root != null, Is.True);
            Assert.That(root.GetComponentsInChildren<EnemyTankView>(), Is.Empty);
            Assert.That(root.GetComponentsInChildren<BulletView>(), Is.Empty);
            Assert.That(root.GetComponentsInChildren<CombatEffectView>(), Is.Empty);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Any(r => r.name == "Enemy preparation - display only"), Is.False);
            Assert.That(s.Bullets.Active, Is.Empty); Assert.That(s.Tanks.All(t => t.Weapon.ActiveBullets == 0), Is.True);
            Assert.That(s.Clock.Paused, Is.True);
        }

        [UnityTest] public IEnumerator CombatArena_RemainsWaveFreeWithNoEnemyOrPreparationDisplay()
        {
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/BattleCity/Scenes/CombatArena.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var c = Controller;
            Assert.That(c.enemyWaveDefinition, Is.Null); Assert.That(c.Session.EnemyWave, Is.Null);
            Tick(c.Session, 700); yield return null;
            Assert.That(c.Session.Tanks.Count, Is.EqualTo(1)); Assert.That(c.GetComponentsInChildren<EnemyTankView>(), Is.Empty);
            Assert.That(Hint, Is.Null);
        }
    }
}
#endif
