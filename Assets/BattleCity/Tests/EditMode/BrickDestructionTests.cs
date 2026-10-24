using System;
using NUnit.Framework;
using UnityEngine;

namespace BattleCity.Tests
{
    public sealed class BrickDestructionTests
    {
        private LevelDefinition level;
        private TankDefinition tank;
        private BulletWorld bullets;

        [SetUp] public void SetUp()
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            tank = ScriptableObject.CreateInstance<TankDefinition>();
            tank.bulletSpeedPixelsPerSecond = 6000;
            bullets = new BulletWorld();
        }
        [TearDown] public void TearDown()
        { UnityEngine.Object.DestroyImmediate(level); UnityEngine.Object.DestroyImmediate(tank); }

        private void Fill(int x, int y, int width, int height, TerrainKind kind = TerrainKind.Brick)
        {
            for (int row = y / 8; row < (y + height) / 8; row++)
                for (int col = x / 8; col < (x + width) / 8; col++) level.terrain[row * 26 + col] = kind;
        }
        private WeaponState Spawn(Vector2 center, CardinalDirection direction)
        {
            var weapon = new WeaponState(tank, bullets);
            Assert.That(weapon.Step(new TankCommand(CardinalDirection.None, true),
                center - direction.Vector() * WeaponState.MuzzleOffsetPixels, direction), Is.True);
            return weapon;
        }
        private int Fire(MapState map, Vector2 center, CardinalDirection direction)
        {
            Spawn(center, direction); bullets.Step(map);
            return map.ApplyBulletHits(bullets.Hits);
        }

        [TestCase(CardinalDirection.Up, 104, 48, 20)]
        [TestCase(CardinalDirection.Down, 104, 160, 31)]
        [TestCase(CardinalDirection.Right, 48, 104, 20)]
        [TestCase(CardinalDirection.Left, 160, 104, 31)]
        public void FourDirections_RemoveOnlyOne16By4Strip(CardinalDirection direction, float x, float y, int along)
        {
            Fill(80, 80, 48, 48);
            var map = new MapState(level);
            Assert.That(Fire(map, new Vector2(x, y), direction), Is.EqualTo(4));
            bool horizontal = direction == CardinalDirection.Left || direction == CardinalDirection.Right;
            for (int cy = 20; cy < 32; cy++) for (int cx = 20; cx < 32; cx++)
            {
                bool removed = horizontal ? cx == along && cy >= 24 && cy < 28 : cy == along && cx >= 24 && cx < 28;
                Assert.That(map.IsSolid(cx, cy), Is.EqualTo(!removed), $"cell {cx},{cy}");
                Assert.That(map.BlocksBullet(cx, cy), Is.EqualTo(!removed));
            }
            Assert.That(map.Revision, Is.EqualTo(4));
            Assert.That(map.ApplyBulletHits(bullets.Hits), Is.Zero, "Replaying an impact cannot dig a second layer");
            Assert.That(map.Revision, Is.EqualTo(4));
        }

        [TestCase(CardinalDirection.Up, 0)]
        [TestCase(CardinalDirection.Down, 0)]
        [TestCase(CardinalDirection.Right, 0)]
        [TestCase(CardinalDirection.Left, 0)]
        [TestCase(CardinalDirection.Up, 8)]
        [TestCase(CardinalDirection.Down, 8)]
        [TestCase(CardinalDirection.Right, 8)]
        [TestCase(CardinalDirection.Left, 8)]
        public void WeaponMuzzle_TwoShotsRemoveIncomingLayerThenRemainingLayer(CardinalDirection direction, int gap)
        {
            Fill(96, 96, 16, 16);
            tank.fireCooldownTicks = 1;
            var map = new MapState(level);
            var axis = direction.Vector();
            var center = new Vector2(104, 104) - axis * (16 + gap);
            Assert.That(map.CanOccupy(center), Is.True);
            var weapon = new WeaponState(tank, bullets);
            bool horizontal = axis.x != 0;
            int outer = axis.x + axis.y > 0 ? 24 : 27;
            int inner = axis.x + axis.y > 0 ? 25 : 26;
            for (int shot = 0; shot < 2; shot++)
            {
                Assert.That(weapon.Step(new TankCommand(CardinalDirection.None, true), center, direction), Is.True);
                Assert.That(bullets.Active[0].Position, Is.EqualTo(center + axis * WeaponState.MuzzleOffsetPixels));
                bullets.Step(map);
                Assert.That(bullets.Hits.Count, Is.EqualTo(1));
                var hit = bullets.Hits[0];
                Assert.That(horizontal ? hit.DamageCell.x : hit.DamageCell.y, Is.EqualTo(shot == 0 ? outer : inner));
                Assert.That(map.ApplyBulletHits(bullets.Hits), Is.EqualTo(4));
                for (int cross = 24; cross < 28; cross++)
                {
                    Assert.That(horizontal ? map.IsSolid(outer, cross) : map.IsSolid(cross, outer), Is.False);
                    Assert.That(horizontal ? map.IsSolid(inner, cross) : map.IsSolid(cross, inner), Is.EqualTo(shot == 0));
                }
            }
        }

        [TestCase(CardinalDirection.Up)]
        [TestCase(CardinalDirection.Down)]
        [TestCase(CardinalDirection.Right)]
        [TestCase(CardinalDirection.Left)]
        public void TouchingWall_SameTimeWeaponsSelectSameOuterLayerInStableOrder(CardinalDirection direction)
        {
            Fill(96, 96, 16, 16);
            var map = new MapState(level);
            var center = new Vector2(104, 104) - direction.Vector() * 16;
            for (int i = 0; i < 2; i++)
                Assert.That(new WeaponState(tank, bullets).Step(new TankCommand(CardinalDirection.None, true), center, direction), Is.True);
            long firstId = bullets.Active[0].Id, secondId = bullets.Active[1].Id;
            bullets.Step(map);
            Assert.That(bullets.Hits.Count, Is.EqualTo(2));
            Assert.That(bullets.Hits[0].BulletId, Is.EqualTo(firstId));
            Assert.That(bullets.Hits[1].BulletId, Is.EqualTo(secondId));
            Assert.That(bullets.Hits[0].TickFraction, Is.Zero);
            Assert.That(bullets.Hits[1].DamageCell, Is.EqualTo(bullets.Hits[0].DamageCell));
            int along = direction.Vector().x + direction.Vector().y > 0 ? 24 : 27;
            Assert.That(direction.Vector().x != 0 ? bullets.Hits[0].DamageCell.x : bullets.Hits[0].DamageCell.y, Is.EqualTo(along));
            Assert.That(map.ApplyBulletHits(bullets.Hits), Is.EqualTo(4), "Same tick must not drill the next layer");
        }

        [TestCase(104f, 24)] [TestCase(107.5f, 24)] [TestCase(108f, 25)]
        public void AcrossStrip_FloorsLowerEdgeToFourPixelGrid(float cross, int first)
        {
            Fill(80, 80, 48, 16);
            var map = new MapState(level);
            Assert.That(Fire(map, new Vector2(cross, 48), CardinalDirection.Up), Is.EqualTo(4));
            for (int x = 20; x < 32; x++) Assert.That(map.IsSolid(x, 20), Is.EqualTo(x < first || x >= first + 4));
        }

        [TestCase(4f, 0)] [TestCase(204f, 49)]
        public void MapEdge_ClipsWithoutShiftingStripInward(float cross, int first)
        {
            Fill(0, 80, 208, 8);
            var map = new MapState(level);
            Assert.That(Fire(map, new Vector2(cross, 48), CardinalDirection.Up), Is.EqualTo(3));
            for (int x = 0; x < 52; x++) Assert.That(map.IsSolid(x, 20), Is.EqualTo(x < first || x >= first + 3));
        }

        [TestCase(TerrainKind.Steel)] [TestCase(TerrainKind.Water)]
        [TestCase(TerrainKind.Grass)] [TestCase(TerrainKind.Ice)]
        public void Strip_NeverChangesOtherTerrain(TerrainKind neighbor)
        {
            Fill(96, 96, 8, 8); Fill(104, 96, 8, 8, neighbor);
            var map = new MapState(level);
            Assert.That(Fire(map, new Vector2(100, 64), CardinalDirection.Up), Is.EqualTo(2));
            Assert.That(map.TerrainAt(13, 12), Is.EqualTo(neighbor));
            Assert.That(map.BrickMaskAt(12, 12), Is.EqualTo(12));
            Assert.That(map.BrickMaskAt(13, 12), Is.Zero);
        }

        [Test]
        public void TwoShots_RemoveTwoLayersAndClearTerrainOnlyWhenLastBitIsGone()
        {
            Fill(96, 96, 16, 8);
            var map = new MapState(level);
            Assert.That(Fire(map, new Vector2(104, 64), CardinalDirection.Up), Is.EqualTo(4));
            Assert.That(map.TerrainAt(12, 12), Is.EqualTo(TerrainKind.Brick));
            Assert.That(map.BrickMaskAt(12, 12), Is.EqualTo(12));
            Assert.That(map.CellRevisionAt(12, 12), Is.GreaterThan(0));
            Assert.That(map.CellRevisionAt(0, 0), Is.Zero);
            Assert.That(Fire(map, new Vector2(104, 64), CardinalDirection.Up), Is.EqualTo(4));
            Assert.That(bullets.Hits[0].Position.y, Is.EqualTo(98).Within(.001f));
            Assert.That(map.TerrainAt(12, 12), Is.EqualTo(TerrainKind.Empty));
            Assert.That(map.TerrainAt(13, 12), Is.EqualTo(TerrainKind.Empty));
            Assert.That(map.BrickMaskAt(12, 12), Is.Zero);
            Assert.That(Fire(map, new Vector2(104, 64), CardinalDirection.Up), Is.Zero);
            Assert.That(bullets.Active.Count, Is.EqualTo(1), "Third projectile travels through cleared terrain");
            bullets.Clear();
        }

        [Test]
        public void RemainingLayerBlocksTank_ThenFourShotsOpenA16PixelPassage()
        {
            Fill(96, 80, 16, 48);
            var map = new MapState(level);
            Assert.That(Fire(map, new Vector2(64, 104), CardinalDirection.Right), Is.EqualTo(4));
            Assert.That(map.MoveAxis(new Vector2(88, 104), new Vector2(100, 0)).x, Is.EqualTo(92));
            for (int shot = 0; shot < 3; shot++) Assert.That(Fire(map, new Vector2(64, 104), CardinalDirection.Right), Is.EqualTo(4));
            Assert.That(map.CanOccupy(new Vector2(104, 104)), Is.True);
            Assert.That(map.MoveAxis(new Vector2(88, 104), new Vector2(100, 0)).x, Is.EqualTo(188));
            Assert.That(map.IsSolid(24, 23), Is.True, "Adjacent wall below the corridor survives");
            Assert.That(map.IsSolid(24, 28), Is.True, "Adjacent wall above the corridor survives");
        }

        [Test]
        public void EightPixelGap_RemainsImpassableTo16PixelTank()
        {
            Fill(96, 80, 16, 48);
            Fill(96, 88, 16, 8, TerrainKind.Steel);
            Fill(96, 104, 16, 8, TerrainKind.Steel);
            var map = new MapState(level);
            // Shoot INSIDE the future 8px gap, not at the brick/steel seam.
            // The 16px damage strip spans y=92..108; steel trims the actual gap to y=96..104.
            for (int shot = 0; shot < 4; shot++)
                Assert.That(Fire(map, new Vector2(64, 100), CardinalDirection.Right), Is.EqualTo(2), $"shot {shot + 1}");
            Assert.That(map.CanOccupy(new Vector2(104, 100), 4), Is.True);
            Assert.That(map.CanOccupy(new Vector2(104, 100)), Is.False);
            Assert.That(map.MoveAxis(new Vector2(88, 100), new Vector2(100, 0)).x, Is.EqualTo(88));
            Assert.That(map.IsSolid(24, 23), Is.True, "Lower steel border survives");
            Assert.That(map.IsSolid(24, 26), Is.True, "Upper steel border survives");
            Assert.That(Fire(map, new Vector2(64, 108), CardinalDirection.Right), Is.Zero);
            Assert.That(bullets.Hits[0].Terrain, Is.EqualTo(TerrainKind.Steel));
        }

        [Test]
        public void SameTickTwoBullets_HitOneSnapshotAndDoNotDrillTwoLayers()
        {
            Fill(96, 96, 16, 8);
            var map = new MapState(level);
            var a = Spawn(new Vector2(104, 64), CardinalDirection.Up);
            var b = Spawn(new Vector2(104, 64), CardinalDirection.Up);
            bullets.Step(map);
            Assert.That(bullets.Hits.Count, Is.EqualTo(2));
            Assert.That(map.ApplyBulletHits(bullets.Hits), Is.EqualTo(4));
            Assert.That(map.BrickMaskAt(12, 12), Is.EqualTo(12));
            Assert.That(a.ActiveBullets + b.ActiveBullets, Is.Zero);
            Assert.That(Fire(map, new Vector2(104, 64), CardinalDirection.Up), Is.EqualTo(4));
        }

        [Test]
        public void SteelBoundaryAndEmptyEvents_DoNotAdvanceMapRevision()
        {
            Fill(96, 96, 16, 8, TerrainKind.Steel);
            var map = new MapState(level);
            Assert.That(Fire(map, new Vector2(104, 64), CardinalDirection.Up), Is.Zero);
            Assert.That(Fire(map, new Vector2(40, 100), CardinalDirection.Left), Is.Zero);
            Assert.That(map.Revision, Is.Zero);
            Assert.Throws<ArgumentNullException>(() => map.ApplyBulletHits(null));
        }

        [Test]
        public void AuthoringAndNewSession_RetainOriginalFullBricks()
        {
            Fill(96, 96, 16, 8);
            var map = new MapState(level);
            Fire(map, new Vector2(104, 64), CardinalDirection.Up);
            Fire(map, new Vector2(104, 64), CardinalDirection.Up);
            var fresh = new MapState(level);
            Assert.That(level.terrain[12 * 26 + 12], Is.EqualTo(TerrainKind.Brick));
            Assert.That(fresh.BrickMaskAt(12, 12), Is.EqualTo(15));
            Assert.That(fresh.Revision, Is.Zero);
            Assert.That(fresh.CellRevisionAt(12, 12), Is.Zero);
        }

        [Test]
        public void Session_MultipleTicksApplyDamageBeforeTheNextSweep()
        {
            level.playerSpawnPixels = new Vector2Int(24, 24);
            Fill(32, 16, 8, 16);
            tank.fireCooldownTicks = 1;
            var session = new ArenaSession(level, tank);
            Assert.That(session.AdvanceFrame(2 * LogicClock.StepSeconds, PlayerButtons.D | PlayerButtons.Fire), Is.EqualTo(2));
            Assert.That(session.Map.Revision, Is.EqualTo(8));
            Assert.That(session.Map.TerrainAt(4, 2), Is.EqualTo(TerrainKind.Empty));
            Assert.That(session.Map.TerrainAt(4, 3), Is.EqualTo(TerrainKind.Empty));
            Assert.That(session.Bullets.Active, Is.Empty);
            Assert.That(session.PlayerWeapon.ActiveBullets, Is.Zero);
            Assert.That(session.Map.CanOccupy(new Vector2(40, 24)), Is.True);
        }

        [Test]
        public void PausedSession_DoesNotReplayLastTickDamage()
        {
            level.playerSpawnPixels = new Vector2Int(24, 24);
            Fill(32, 16, 8, 16);
            var session = new ArenaSession(level, tank);
            session.AdvanceFrame(LogicClock.StepSeconds, PlayerButtons.D | PlayerButtons.Fire);
            int revision = session.Map.Revision;
            Assert.That(revision, Is.EqualTo(4));
            session.SetPaused(true);
            for (int i = 0; i < 10; i++) session.AdvanceFrame(.2, PlayerButtons.Fire);
            Assert.That(session.Map.Revision, Is.EqualTo(revision));
            Assert.That(session.Map.BrickMaskAt(4, 2), Is.EqualTo(10));
        }
    }
}
