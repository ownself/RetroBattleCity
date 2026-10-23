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
    public sealed class CombatEffectViewTests
    {
        private Scene scene;
        private ArenaController controller;
        private const string ArenaPath = "Assets/BattleCity/Scenes/CombatArena.unity";

        private IEnumerator Load()
        {
            scene = EditorSceneManager.LoadSceneInPlayMode(ArenaPath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaController>()).Single();
            controller.Session.SetFocus(true); controller.Session.SetPaused(true);
        }
        private void Tick(int count)
        {
            controller.Session.SetPaused(false);
            for (int i = 0; i < count; i++) controller.Session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None);
            controller.Session.SetPaused(true);
        }
        private void HitBrick()
        {
            controller.Session.SetPaused(false);
            for (int i = 0; i < 100; i++)
            {
                controller.Session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.Fire);
                if (controller.Session.Bullets.Hits.Count > 0) break;
            }
            controller.Session.SetPaused(true);
            Assert.That(controller.Session.Bullets.Hits.Count, Is.EqualTo(1));
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Hit_RealUpdateProjectsFramesRecyclesAndReusesRendererWithoutDamage()
        {
            yield return Load(); HitBrick(); yield return null;
            var view = controller.EffectView;
            var renderer = view.GetComponentsInChildren<SpriteRenderer>().Single();
            Assert.That(renderer.sprite.name, Is.EqualTo("effect.explosion.small.0"));
            Assert.That(renderer.sortingLayerName, Is.EqualTo("Effects"));
            Assert.That(renderer.sharedMaterial, Is.SameAs(controller.playerRenderer.sharedMaterial));
            Assert.That(PixelGrid.ToMapPixels(renderer.transform.position), Is.EqualTo(new Vector2(24, 166)));
            int revision = controller.Session.Map.Revision;
            for (int i = 0; i < 5; i++) view.Render();
            Assert.That(controller.Session.Effects.Active.Count, Is.EqualTo(1));
            Tick(4); yield return null;
            Assert.That(renderer.sprite.name, Is.EqualTo("effect.explosion.small.1"));
            Tick(4); yield return null;
            Assert.That(renderer.sprite.name, Is.EqualTo("effect.explosion.small.2"));
            Tick(4); yield return null;
            Assert.That(renderer.gameObject.activeSelf, Is.False);
            Assert.That(renderer.sprite, Is.Null);
            Assert.That(controller.Session.Map.Revision, Is.EqualTo(revision));
            controller.Session.Effects.PlayExplosion(new Vector2(80.4f, 90.6f)); yield return null;
            Assert.That(view.GetComponentsInChildren<SpriteRenderer>().Single(), Is.SameAs(renderer));
            Assert.That(PixelGrid.ToMapPixels(renderer.transform.position), Is.EqualTo(new Vector2(80, 91)));
            Tick(12); yield return null;
            Assert.That(renderer.sprite.name, Is.EqualTo("effect.explosion.large.0"));
            Tick(4); yield return null;
            Assert.That(renderer.sprite.name, Is.EqualTo("effect.explosion.large.1"));
            Tick(4); yield return null;
            Assert.That(renderer.gameObject.activeSelf, Is.False);
            Assert.That(controller.Session.Map.Revision, Is.EqualTo(revision));
            Assert.That(controller.Session.PlayerWeapon.ActiveBullets, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Pause_FreezesEffectWhileFramesAdvanceAndResumeDoesNotCatchUp()
        {
            yield return Load(); HitBrick(); yield return null;
            var effect = controller.Session.Effects.Active.Single();
            var renderer = controller.EffectView.GetComponentsInChildren<SpriteRenderer>().Single();
            int frame = Time.frameCount; long tick = controller.Session.Clock.Tick;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(Time.frameCount, Is.GreaterThan(frame));
            Assert.That(controller.Session.Clock.Tick, Is.EqualTo(tick));
            Assert.That(effect.AgeTicks, Is.Zero);
            Assert.That(renderer.sprite.name, Is.EqualTo("effect.explosion.small.0"));
            Tick(1); yield return null;
            Assert.That(effect.AgeTicks, Is.EqualTo(1));
            Assert.That(controller.Session.Effects.Active.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ClearAndDisable_RemovePoolAndReenableCannotReplayOldHits()
        {
            yield return Load(); HitBrick(); yield return null;
            var old = controller.EffectView.GetComponentsInChildren<SpriteRenderer>().Single();
            controller.ClearEffects(); controller.ClearEffects();
            Assert.That(old.gameObject.activeSelf, Is.False);
            yield return null;
            Assert.That(old == null, Is.True);
            Assert.That(controller.EffectView.GetComponentsInChildren<SpriteRenderer>(true), Is.Empty);
            Assert.That(controller.Session.Effects.Active, Is.Empty);
            yield return null;
            Assert.That(controller.Session.Effects.Active, Is.Empty, "Paused stale Hits are not replayed");
            controller.Session.Effects.PlayExplosion(new Vector2(90, 90)); yield return null;
            old = controller.EffectView.GetComponentsInChildren<SpriteRenderer>().Single();
            controller.enabled = false;
            Assert.That(old.gameObject.activeSelf, Is.False);
            yield return null;
            Assert.That(old == null, Is.True);
            controller.enabled = true; yield return null;
            Assert.That(controller.Session.Clock.Paused, Is.True);
            Assert.That(controller.Session.Effects.Active, Is.Empty);
            Assert.That(controller.EffectView.GetComponentsInChildren<SpriteRenderer>(true), Is.Empty);
            Tick(1); yield return null;
            Assert.That(controller.Session.Effects.Active, Is.Empty);
        }

        [UnityTest]
        public IEnumerator ReloadAndDestroy_ClearOldSessionAndDisplaysWithoutCallbacks()
        {
            for (int run = 0; run < 2; run++)
            {
                yield return Load();
                Assert.That(controller.Session.Effects.Active, Is.Empty);
                controller.Session.Effects.PlayExplosion(new Vector2(100, 100)); yield return null;
                var oldSession = controller.Session;
                var oldView = controller.EffectView;
                var oldRenderer = oldView.GetComponentsInChildren<SpriteRenderer>().Single();
                if (run == 0) yield return SceneManager.UnloadSceneAsync(scene);
                else
                {
                    Object.Destroy(controller.gameObject);
                    yield return null;
                    yield return null;
                }
                Assert.That(oldView == null, Is.True);
                Assert.That(oldRenderer == null, Is.True);
                Assert.That(oldSession.Effects.Active, Is.Empty);
                yield return new WaitForSecondsRealtime(.1f);
                Assert.That(oldSession.Effects.Active, Is.Empty);
            }
        }

        [UnityTest]
        public IEnumerator RuntimeClock_ActuallyExpiresExplosionWithoutManualStepping()
        {
            yield return Load();
            controller.Session.Effects.PlayExplosion(new Vector2(104, 104));
            controller.Session.SetPaused(false);
            long start = controller.Session.Clock.Tick; int frame = Time.frameCount;
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(Time.frameCount, Is.GreaterThan(frame));
            Assert.That(controller.Session.Clock.Tick - start, Is.GreaterThanOrEqualTo(20));
            Assert.That(controller.Session.Effects.Active, Is.Empty);
            Assert.That(controller.EffectView.GetComponentsInChildren<SpriteRenderer>(), Is.Empty);
        }
    }
}
#endif
