using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BattleCity.Tests
{
    // Synthetic render deltas, not measurements of hardware/render throughput.
    public sealed class RenderDeltaCombatTests
    {
        private LevelDefinition level;
        private TankDefinition tank;

        [SetUp] public void SetUp()
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.playerSpawnPixels = new Vector2Int(24, 24);
            tank = ScriptableObject.CreateInstance<TankDefinition>();
        }
        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(level);
            Object.DestroyImmediate(tank);
        }

        private static void Advance(ArenaSession session, int fps, int frames, PlayerButtons buttons)
        {
            for (int frame = 0; frame < frames; frame++) session.AdvanceFrame(1.0 / fps, buttons);
            Assert.That(session.Clock.DroppedTicks, Is.Zero);
        }

        [TestCase(30)] [TestCase(60)] [TestCase(144)]
        public void HeldFire_MovingCapCooldownAndBoundaryRecycleMatchAtSharedTimes(int fps)
        {
            var session = new ArenaSession(level, tank);
            Advance(session, fps, fps / 2, PlayerButtons.D | PlayerButtons.Fire);
            Assert.That(session.Clock.Tick, Is.EqualTo(30));
            Assert.That(session.Player.Position, Is.EqualTo(new Vector2(54, 24)));
            var first = session.Bullets.Active.Single();
            Assert.That(first.Id, Is.EqualTo(1), "Capacity blocks firing even after cooldown reaches zero");
            Assert.That(first.Position, Is.EqualTo(new Vector2(125, 24)));
            Assert.That(first.AgeTicks, Is.EqualTo(30));
            Assert.That(session.PlayerWeapon.CooldownRemainingTicks, Is.Zero);
            Assert.That(session.PlayerWeapon.ActiveBullets, Is.EqualTo(1));
            Trace(fps, session);

            Advance(session, fps, fps / 2, PlayerButtons.D | PlayerButtons.Fire);
            Assert.That(session.Clock.Tick, Is.EqualTo(60));
            Assert.That(session.Player.Position, Is.EqualTo(new Vector2(84, 24)));
            var second = session.Bullets.Active.Single();
            Assert.That(second, Is.SameAs(first), "Boundary release returns the slot to the pool");
            Assert.That(second.Id, Is.EqualTo(2));
            // Contact at the END of tick 57 recycles immediately; shot 2 starts on tick 58.
            Assert.That(second.Position, Is.EqualTo(new Vector2(101, 24)));
            Assert.That(second.AgeTicks, Is.EqualTo(3));
            Assert.That(session.PlayerWeapon.CooldownRemainingTicks, Is.EqualTo(13));
            Assert.That(session.PlayerWeapon.ActiveBullets, Is.EqualTo(1));
            Assert.That(session.Map.Revision, Is.Zero);
            var effect = session.Effects.Active.Single();
            Assert.That(effect.AgeTicks, Is.EqualTo(3));
            Assert.That(effect.Position, Is.EqualTo(new Vector2(206, 24)));
            Trace(fps, session);
        }

        [TestCase(30)] [TestCase(60)] [TestCase(144)]
        public void HeldFire_BrickSteelDestructionAndEffectAgesMatchAtSharedTimes(int fps)
        {
            for (int x = 2; x <= 3; x++)
            {
                level.terrain[8 * 26 + x] = TerrainKind.Brick;
                level.terrain[16 * 26 + x] = TerrainKind.Steel;
            }
            tank.bulletSpeedPixelsPerSecond = 6000;
            var session = new ArenaSession(level, tank);
            for (int checkpoint = 1; checkpoint <= 6; checkpoint++)
            {
                // 1/6 second is an integer number of frames at all three rates: same input, same time.
                Advance(session, fps, fps / 6, PlayerButtons.W | PlayerButtons.Fire);
                int tick = checkpoint * 10;
                Assert.That(session.Clock.Tick, Is.EqualTo(tick));
                Assert.That(session.Player.Position, Is.EqualTo(new Vector2(24, 24 + tick)));
                Assert.That(session.Bullets.Active, Is.Empty, "Every projectile hits in its spawn tick");
                Assert.That(session.PlayerWeapon.ActiveBullets, Is.Zero);
                Assert.That(session.PlayerWeapon.CooldownRemainingTicks, Is.EqualTo(15 - (tick - 1) % 15));
                Assert.That(session.Map.Revision, Is.EqualTo(checkpoint == 1 ? 4 : 8));
                for (int x = 2; x <= 3; x++)
                {
                    Assert.That(session.Map.BrickMaskAt(x, 8), Is.EqualTo(checkpoint == 1 ? 12 : 0));
                    Assert.That(session.Map.TerrainAt(x, 16), Is.EqualTo(TerrainKind.Steel));
                }
                if (checkpoint % 3 == 0) Assert.That(session.Effects.Active, Is.Empty);
                else
                {
                    var effect = session.Effects.Active.Single();
                    int shot = (tick - 1) / 15 + 1;
                    Assert.That(effect.Id, Is.EqualTo(shot));
                    Assert.That(effect.AgeTicks, Is.EqualTo((tick - 1) % 15));
                    Assert.That(effect.Frame, Is.EqualTo(effect.AgeTicks / 4));
                    Assert.That(effect.Position, Is.EqualTo(new Vector2(24, shot == 1 ? 62 : shot == 2 ? 66 : 126)));
                }
                Trace(fps, session);
            }
            Assert.That(session.Map.CanOccupy(new Vector2(24, 68)), Is.True, "Destroyed wall really opens the path");
            Assert.That(level.terrain[8 * 26 + 2], Is.EqualTo(TerrainKind.Brick));
        }

        private static void Trace(int fps, ArenaSession session)
        {
            // Retained in the cursor-scoped Console evidence, not production logging.
            string bullets = string.Join(";", session.Bullets.Active.Select(b => $"id={b.Id},pos={b.Position},age={b.AgeTicks}"));
            string effects = string.Join(";", session.Effects.Active.Select(e => $"id={e.Id},pos={e.Position},age={e.AgeTicks},frame={e.Frame}"));
            Debug.Log($"BC108 simulatedDelta={fps} tick={session.Clock.Tick} dropped={session.Clock.DroppedTicks} player={session.Player.Position} cooldown={session.PlayerWeapon.CooldownRemainingTicks} ownerBullets={session.PlayerWeapon.ActiveBullets} revision={session.Map.Revision} brick2x8={session.Map.BrickMaskAt(2, 8)} bullets=[{bullets}] effects=[{effects}]");
        }
    }
}
