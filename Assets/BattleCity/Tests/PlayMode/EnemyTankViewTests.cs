#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BattleCity.Tests
{
    public sealed class EnemyTankViewTests
    {
        private LevelDefinition level;
        private TankDefinition definition;
        private SpriteCatalog catalog;
        private Material material;
        private GameObject root;
        private EnemyTankView view;
        private ArenaSession session;
        private TankState enemy;

        [SetUp]
        public void SetUp()
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            definition = ScriptableObject.CreateInstance<TankDefinition>();
            definition.definitionId = "enemy.basic";
            definition.spritePrefix = "tank.white.enemy.basic";
            catalog = AssetDatabase.LoadAssetAtPath<SpriteCatalog>("Assets/BattleCity/Data/SpriteCatalog.asset");
            material = AssetDatabase.LoadAssetAtPath<Material>("Assets/BattleCity/Art/SpriteUnlit.mat");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(material, Is.Not.Null);
            session = new ArenaSession(level, definition);
            enemy = session.AddTank(definition, CombatTeam.Enemy, new Vector2(104, 104));
            root = new GameObject("BC203 enemy view test");
            view = root.AddComponent<EnemyTankView>();
            view.Initialize(enemy, definition, catalog, material);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) Object.Destroy(root);
            if (level != null) Object.Destroy(level);
            if (definition != null) Object.Destroy(definition);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        private SpriteRenderer Display => root.GetComponentsInChildren<SpriteRenderer>(true).Single();

        private void CommandTicks(CardinalDirection direction, int count)
        {
            session.SetTankCommand(enemy.EntityId, new TankCommand(direction));
            for (int i = 0; i < count; i++)
                Assert.That(session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.None), Is.EqualTo(1));
        }

        private void AssertProjection(TankState state)
        {
            var display = Display;
            Assert.That(display.enabled, Is.EqualTo(state.IsAlive));
            Assert.That(display.sprite, Is.SameAs(catalog.Get(definition.spritePrefix + "." +
                state.Motor.Facing.SpriteKey() + "." + state.Motor.TrackFrame)));
            var position = state.Motor.Position;
            Assert.That(display.transform.position, Is.EqualTo(PixelGrid.ToWorld(
                new Vector2(Mathf.Round(position.x), Mathf.Round(position.y)))));
        }

        [TestCase(CardinalDirection.Up)]
        [TestCase(CardinalDirection.Left)]
        [TestCase(CardinalDirection.Down)]
        [TestCase(CardinalDirection.Right)]
        public void SharedCommands_FourDirectionsAndTwoTrackFrames_StopHasNoIndependentAnimation(CardinalDirection direction)
        {
            var start = enemy.Motor.Position;
            for (int tick = 1; tick <= 16; tick++)
            {
                CommandTicks(direction, 1);
                view.Render();
                Assert.That(enemy.Motor.Facing, Is.EqualTo(direction));
                Assert.That(enemy.Motor.TrackFrame, Is.EqualTo(tick / 8 % 2));
                Assert.That(enemy.Motor.Position, Is.EqualTo(start + direction.Vector() * tick));
                AssertProjection(enemy);
            }
            var sprite = Display.sprite;
            var position = enemy.Motor.Position;
            CommandTicks(CardinalDirection.None, 24);
            for (int i = 0; i < 100; i++) view.Render();
            Assert.That(enemy.Motor.IsMoving, Is.False);
            Assert.That(enemy.Motor.Position, Is.EqualTo(position));
            Assert.That(Display.sprite, Is.SameAs(sprite));
            AssertProjection(enemy);
        }

        [Test]
        public void FractionalSharedMotion_RoundsDisplayWithoutWritingLogic_UsesExistingSpritesAndSharedMaterial()
        {
            definition.moveSpeedPixelsPerSecond = 45;
            enemy = session.AddTank(definition, CombatTeam.Enemy, new Vector2(136, 104));
            view.Initialize(enemy, definition, catalog, material);
            CommandTicks(CardinalDirection.Up, 1);
            Assert.That(enemy.Motor.Position.y, Is.EqualTo(104.75f));
            var position = enemy.Motor.Position;
            long tick = session.Clock.Tick;
            for (int i = 0; i < 100; i++) view.Render();
            AssertProjection(enemy);
            Assert.That(PixelGrid.ToMapPixels(Display.transform.position), Is.EqualTo(new Vector2(136, 105)));
            Assert.That(enemy.Motor.Position, Is.EqualTo(position));
            Assert.That(session.Clock.Tick, Is.EqualTo(tick));
            Assert.That(Display.sortingLayerName, Is.EqualTo("Actors"));
            Assert.That(Display.sharedMaterial, Is.SameAs(material));
            Assert.That(Display.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            Assert.That(Display.sprite.pixelsPerUnit, Is.EqualTo(16));
            Assert.That(Display.sprite.rect.size, Is.EqualTo(new Vector2(16, 16)));
            Assert.That(root.GetComponentsInChildren<Collider2D>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
        }

        [UnityTest]
        public IEnumerator Pause_RealFramesAndRepeatedRenderCannotAdvanceSharedPose_ResumeUsesNextTick()
        {
            CommandTicks(CardinalDirection.Down, 9);
            view.Render();
            session.SetPaused(true);
            var position = enemy.Motor.Position;
            var sprite = Display.sprite;
            int track = enemy.Motor.TrackFrame;
            long tick = session.Clock.Tick;
            int frame = Time.frameCount;
            for (int i = 0; i < 5; i++)
            {
                Assert.That(session.AdvanceFrame(1, PlayerButtons.None), Is.Zero);
                view.Render();
                yield return null;
            }
            Assert.That(Time.frameCount, Is.GreaterThan(frame));
            Assert.That(session.Clock.Tick, Is.EqualTo(tick));
            Assert.That(enemy.Motor.Position, Is.EqualTo(position));
            Assert.That(enemy.Motor.TrackFrame, Is.EqualTo(track));
            Assert.That(Display.sprite, Is.SameAs(sprite));
            AssertProjection(enemy);
            session.SetPaused(false);
            CommandTicks(CardinalDirection.Down, 1);
            view.Render();
            Assert.That(enemy.Motor.Position, Is.EqualTo(position + Vector2.down));
            Assert.That(session.Clock.Tick, Is.EqualTo(tick + 1));
            Assert.That(session.Clock.DroppedTicks, Is.Zero);
            AssertProjection(enemy);
        }

        [Test]
        public void RealSharedProjectileDeath_HidesImmediatelyAndRepeatedRenderDoesNotResurrectOrDamage()
        {
            CommandTicks(CardinalDirection.Down, 8);
            view.Render();
            Assert.That(Display.enabled, Is.True);
            // Shared player weapon fires up the clear column at the moving enemy.
            definition.bulletSpeedPixelsPerSecond = 6000;
            var shooter = session.AddTank(definition, CombatTeam.Player, new Vector2(104, 56));
            session.SetTankCommand(shooter.EntityId, new TankCommand(CardinalDirection.Up, false, true));
            CommandTicks(CardinalDirection.None, 1);
            Assert.That(session.Bullets.Hits.Any(h => h.Kind == BulletHitKind.Tank && h.TargetId == enemy.EntityId), Is.True);
            Assert.That(enemy.IsAlive, Is.False);
            Assert.That(enemy.HitPoints, Is.Zero);
            Assert.That(enemy.Motor.IsMoving, Is.False);
            var position = enemy.Motor.Position;
            for (int i = 0; i < 10; i++) view.Render();
            Assert.That(Display.enabled, Is.False);
            Assert.That(Display.sprite, Is.Null);
            CommandTicks(CardinalDirection.Right, 16);
            view.Render();
            Assert.That(enemy.HitPoints, Is.Zero);
            Assert.That(enemy.Motor.Position, Is.EqualTo(position));
            Assert.That(Display.enabled, Is.False);
            Assert.That(Display.sprite, Is.Null);
        }

        [Test]
        public void ClearAndRepeatedBinding_ReuseOneRendererAndDoNotRetainOldTank()
        {
            var display = Display;
            view.Clear(); view.Clear(); view.Render();
            Assert.That(display.enabled, Is.False);
            Assert.That(display.sprite, Is.Null);
            Assert.That(display.sharedMaterial, Is.Null);
            CommandTicks(CardinalDirection.Right, 8);
            Assert.That(enemy.IsAlive, Is.True);
            var replacement = session.AddTank(definition, CombatTeam.Enemy, new Vector2(152, 152));
            for (int i = 0; i < 5; i++) view.Initialize(replacement, definition, catalog, material);
            Assert.That(Display, Is.SameAs(display));
            AssertProjection(replacement);
            CommandTicks(CardinalDirection.Down, 8);
            view.Render();
            AssertProjection(replacement);
            Assert.That(enemy.Motor.Position, Is.Not.EqualTo(replacement.Motor.Position));
        }

        [UnityTest]
        public IEnumerator DisableAndDestroyComponent_ClearDisplayButNeverDestroyOrStopSharedTank()
        {
            var display = Display;
            view.enabled = false;
            Assert.That(display.enabled, Is.False);
            Assert.That(display.sprite, Is.Null);
            view.Render();
            view.enabled = true; view.Render();
            Assert.That(display.enabled, Is.False, "Explicit rebind required after disable.");
            view.Initialize(enemy, definition, catalog, material);
            root.SetActive(false);
            Assert.That(display.enabled, Is.False);
            root.SetActive(true); view.Render();
            Assert.That(display.sprite, Is.Null);
            view.Initialize(enemy, definition, catalog, material);
            Object.Destroy(view);
            yield return null;
            Assert.That(view == null, Is.True);
            // Child destruction is deferred, but may never remain visible after OnDestroy.
            if (display != null) Assert.That(display.enabled, Is.False);
            yield return null;
            Assert.That(display == null, Is.True);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true), Is.Empty);
            CommandTicks(CardinalDirection.Up, 1);
            Assert.That(enemy.IsAlive, Is.True);
            Assert.That(enemy.Motor.Position, Is.EqualTo(new Vector2(104, 105)));
        }

        [UnityTest]
        public IEnumerator DestroyOwner_RemovesOwnedChildAndLeavesSharedInstanceUsable()
        {
            var display = Display;
            Object.Destroy(root);
            yield return null;
            Assert.That(root == null, Is.True);
            Assert.That(display == null, Is.True);
            CommandTicks(CardinalDirection.Left, 1);
            Assert.That(enemy.IsAlive, Is.True);
            Assert.That(enemy.Motor.Position, Is.EqualTo(new Vector2(103, 104)));
        }

        [Test]
        public void Initialize_SnapshotsAllEightFrames_WithoutRetainingCatalogOrDefinition()
        {
            var copy = Object.Instantiate(catalog);
            view.Initialize(enemy, definition, copy, material);
            definition.spritePrefix = "missing.after.binding";
            Object.DestroyImmediate(copy);
            foreach (CardinalDirection direction in new[] { CardinalDirection.Up, CardinalDirection.Left, CardinalDirection.Down, CardinalDirection.Right })
            {
                for (int i = 0; i < 16; i++)
                {
                    CommandTicks(direction, 1);
                    view.Render();
                    Assert.That(Display.sprite, Is.SameAs(catalog.Get("tank.white.enemy.basic." +
                        enemy.Motor.Facing.SpriteKey() + "." + enemy.Motor.TrackFrame)));
                }
            }
        }

        [Test]
        public void InvalidRebind_ClearsPreviousDisplayAndFailsClearly_WithoutExtraRenderer()
        {
            var display = Display;
            Assert.Throws<ArgumentNullException>(() => view.Initialize(null, definition, catalog, material));
            Assert.That(display.enabled, Is.False);
            Assert.That(display.sprite, Is.Null);
            view.Initialize(enemy, definition, catalog, material);
            definition.spritePrefix = "missing.enemy";
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => view.Initialize(enemy, definition, catalog, material));
            view.Render();
            Assert.That(display.enabled, Is.False);
            Assert.That(display.sprite, Is.Null);
            Assert.That(Display, Is.SameAs(display));
            Assert.Throws<ArgumentException>(() => view.Initialize(session.PlayerTank, definition, catalog, material));
            Assert.That(enemy.IsAlive, Is.True);
        }
    }
}
#endif
