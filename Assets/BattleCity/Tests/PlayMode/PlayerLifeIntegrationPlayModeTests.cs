#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BattleCity.Tests
{
    public sealed class PlayerLifeIntegrationPlayModeTests
    {
        private Scene scene;
        private TankDefinition enemyDefinition;
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (enemyDefinition != null) Object.Destroy(enemyDefinition);
            LogAssert.NoUnexpectedReceived();
        }
        private ArenaController Controller => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
        private static SpriteRenderer Shield(ArenaController c) => c.GetComponentsInChildren<SpriteRenderer>(true).Single(r => r.name == "Player shield - display only");
        private IEnumerator WaitUntil(System.Func<bool> condition, float seconds = 5)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "Real Update did not reach the required life phase before deadline");
        }

        [UnityTest] public IEnumerator Stage01_RealUpdateSharedEnemyWeaponDeathHiddenWaitRespawnShieldExpiry_PauseAndFocusFreeze()
        {
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/BattleCity/Scenes/Stage01.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var c = Controller; var s = c.Session; var player = s.PlayerTank; var motor = s.Player; var weapon = s.PlayerWeapon;
            Assert.That(c.enablePlayerLifecycle, Is.True); Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(3));
            var shield = Shield(c); Assert.That(shield.enabled, Is.False); Assert.That(s.PlayerLife.IsProtected, Is.False);
            enemyDefinition = Object.Instantiate(c.playerDefinition);
            enemyDefinition.moveSpeedPixelsPerSecond = .001f; enemyDefinition.bulletSpeedPixelsPerSecond = 6000;
            var enemy = s.AddTank(enemyDefinition, CombatTeam.Enemy, new Vector2(72, 40));
            s.SetFocus(true); s.SetPaused(false);
            int firstFrame = Time.frameCount; long firstTick = s.Clock.Tick;
            s.SetTankCommand(enemy.EntityId, new TankCommand(CardinalDirection.Down, false, true));
            yield return WaitUntil(() => !player.IsAlive);
            s.SetPaused(true);
            Assert.That(Time.frameCount, Is.GreaterThan(firstFrame)); Assert.That(s.Clock.Tick, Is.GreaterThan(firstTick));
            Assert.That(s.PlayerLife.RemainingLives, Is.EqualTo(2)); Assert.That(c.playerRenderer.enabled, Is.False);
            Assert.That(shield.enabled, Is.False); Assert.That(enemy.Weapon.ActiveBullets, Is.Zero);
            int wait = s.PlayerLife.RespawnRemainingTicks; long pausedTick = s.Clock.Tick;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(s.Clock.Tick, Is.EqualTo(pausedTick)); Assert.That(s.PlayerLife.RespawnRemainingTicks, Is.EqualTo(wait));
            s.SetPaused(false);
            yield return WaitUntil(() => player.IsAlive);
            s.SetPaused(true);
            Assert.That(s.PlayerTank, Is.SameAs(player)); Assert.That(s.Player, Is.SameAs(motor)); Assert.That(s.PlayerWeapon, Is.SameAs(weapon));
            Assert.That(c.playerRenderer.enabled, Is.True); Assert.That(shield.enabled, Is.True);
            Assert.That(new[] { c.sprites.Get("effect.shield.0"), c.sprites.Get("effect.shield.1") }, Does.Contain(shield.sprite));
            Assert.That(shield.transform.position, Is.EqualTo(c.playerRenderer.transform.position));
            Assert.That(shield.GetComponent<Collider2D>(), Is.Null); Assert.That(shield.GetComponent<Rigidbody2D>(), Is.Null);
            int protection = s.PlayerLife.ProtectionRemainingTicks; var sprite = shield.sprite;
            c.SendMessage("OnApplicationFocus", false); c.SendMessage("OnApplicationFocus", true);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(s.Clock.Paused, Is.True); Assert.That(s.PlayerLife.ProtectionRemainingTicks, Is.EqualTo(protection));
            Assert.That(shield.sprite, Is.SameAs(sprite));
            s.SetPaused(false);
            yield return WaitUntil(() => !s.PlayerLife.IsProtected);
            s.SetPaused(true);
            Assert.That(shield.enabled, Is.False); Assert.That(shield.sprite, Is.Null); Assert.That(player.IsAlive, Is.True);
            Debug.Log($"BC205 realUpdate frames={firstFrame}->{Time.frameCount} ticks={firstTick}->{s.Clock.Tick} lives={s.PlayerLife.RemainingLives} droppedTicks={s.Clock.DroppedTicks}");
            c.enabled = false; Assert.That(shield.enabled, Is.False);
            Object.Destroy(c); yield return null;
            Assert.That(shield == null, Is.True, "Destroying only the controller must remove its shield child");
        }

        [UnityTest] public IEnumerator CombatArena_RemainsExplicitlyLifecycleDisabled()
        {
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/BattleCity/Scenes/CombatArena.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            Assert.That(Controller.enablePlayerLifecycle, Is.False); Assert.That(Controller.Session.PlayerLife, Is.Null);
            Assert.That(Controller.GetComponentsInChildren<SpriteRenderer>(true).Any(r => r.name == "Player shield - display only"), Is.False);
        }
    }
}
#endif
