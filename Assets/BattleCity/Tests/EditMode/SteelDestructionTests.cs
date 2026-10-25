using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BattleCity.Tests
{
    public sealed class SteelDestructionTests
    {
        private LevelDefinition level;
        private TankDefinition tank;
        private BulletWorld bullets;

        [SetUp] public void SetUp()
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            tank = ScriptableObject.CreateInstance<TankDefinition>();
            tank.bulletSpeedPixelsPerSecond = 6000;
            tank.canDestroySteel = true;
            bullets = new BulletWorld();
        }
        [TearDown] public void TearDown()
        { Object.DestroyImmediate(level); Object.DestroyImmediate(tank); }

        private void Fill(int x, int y, int width, int height)
        {
            for (int cy = y; cy < y + height; cy++) for (int cx = x; cx < x + width; cx++)
                level.terrain[cy * 26 + cx] = TerrainKind.Steel;
        }
        private WeaponState Spawn(Vector2 muzzle, CardinalDirection direction)
        {
            var weapon = new WeaponState(tank, bullets);
            Assert.That(weapon.Step(new TankCommand(direction, true),
                muzzle - direction.Vector() * WeaponState.MuzzleOffsetPixels, direction), Is.True);
            return weapon;
        }
        private int Fire(MapState map, Vector2 muzzle, CardinalDirection direction)
        {
            var weapon = Spawn(muzzle, direction);
            bullets.Step(map);
            Assert.That(bullets.Hits.Single().Kind, Is.EqualTo(BulletHitKind.Terrain));
            Assert.That(bullets.Hits.Single().Terrain, Is.EqualTo(TerrainKind.Steel));
            Assert.That(weapon.ActiveBullets, Is.Zero);
            return map.ApplyBulletHits(bullets.Hits);
        }
        private static bool Front(CardinalDirection direction, int x, int y)
            => direction == CardinalDirection.Up ? y == 12 : direction == CardinalDirection.Down ? y == 13 :
                direction == CardinalDirection.Right ? x == 12 : x == 13;

        [TestCase(CardinalDirection.Up, 0)] [TestCase(CardinalDirection.Up, 8)]
        [TestCase(CardinalDirection.Down, 0)] [TestCase(CardinalDirection.Down, 8)]
        [TestCase(CardinalDirection.Left, 0)] [TestCase(CardinalDirection.Left, 8)]
        [TestCase(CardinalDirection.Right, 0)] [TestCase(CardinalDirection.Right, 8)]
        public void TwoByTwoBlock_FirstShotClearsFrontPair_SecondClearsBackPair(CardinalDirection direction, int gap)
        {
            Fill(12, 12, 2, 2);
            var map = new MapState(level);
            var center = new Vector2(104, 104);
            var tankCenter = center - direction.Vector() * (16 + gap);
            var muzzle = tankCenter + direction.Vector() * WeaponState.MuzzleOffsetPixels;
            Assert.That(map.CanOccupy(tankCenter), Is.True);
            for (int shot = 0; shot < 2; shot++)
            {
                Assert.That(Fire(map, muzzle, direction), Is.EqualTo(8), "One 16x8 strip = eight 4px damage cells.");
                Assert.That(map.Revision, Is.EqualTo((shot + 1) * 2));
                Assert.That(map.ApplyBulletHits(bullets.Hits), Is.Zero, "Replaying the same contact cannot dig deeper.");
                for (int y = 12; y < 14; y++) for (int x = 12; x < 14; x++)
                {
                    bool remains = shot == 0 && !Front(direction, x, y);
                    Assert.That(map.TerrainAt(x, y), Is.EqualTo(remains ? TerrainKind.Steel : TerrainKind.Empty));
                    Assert.That(map.IsSolid(x * 2, y * 2), Is.EqualTo(remains));
                    Assert.That(map.BlocksBullet(x * 2, y * 2), Is.EqualTo(remains));
                }
                Assert.That(map.CanOccupy(center), Is.EqualTo(shot == 1));
                if (shot == 0)
                    Assert.That(map.MoveAxis(tankCenter, direction.Vector() * 64),
                        Is.EqualTo(tankCenter + direction.Vector() * (8 + gap)));
            }
            Assert.That(level.terrain[12 * 26 + 12], Is.EqualTo(TerrainKind.Steel), "Runtime damage never rewrites the level asset.");
        }

        [TestCase(CardinalDirection.Up)] [TestCase(CardinalDirection.Down)]
        [TestCase(CardinalDirection.Left)] [TestCase(CardinalDirection.Right)]
        public void SameTickTwoShotsRemoveOnlyOneFrontPair_NoPiercingOrDoubleCount(CardinalDirection direction)
        {
            Fill(12, 12, 2, 2);
            var map = new MapState(level);
            var muzzle = new Vector2(104, 104) - direction.Vector() * 6;
            Spawn(muzzle, direction); Spawn(muzzle, direction);
            bullets.Step(map);
            Assert.That(bullets.Hits.Count, Is.EqualTo(2));
            Assert.That(map.ApplyBulletHits(bullets.Hits), Is.EqualTo(8));
            Assert.That(map.Revision, Is.EqualTo(2));
            Assert.That(map.ApplyBulletHits(bullets.Hits), Is.Zero);
            for (int y = 12; y < 14; y++) for (int x = 12; x < 14; x++)
                Assert.That(map.TerrainAt(x, y), Is.EqualTo(Front(direction, x, y) ? TerrainKind.Empty : TerrainKind.Steel));
            Assert.That(bullets.Active, Is.Empty);
        }

        [TestCase(104f, 12)] [TestCase(111.5f, 12)] [TestCase(112f, 13)]
        public void AcrossStripFloorsLowerEdgeToEightPixelGrid(float across, int first)
        {
            Fill(10, 12, 8, 2);
            var map = new MapState(level);
            Assert.That(Fire(map, new Vector2(across, 64), CardinalDirection.Up), Is.EqualTo(8));
            for (int x = 10; x < 18; x++)
            {
                Assert.That(map.TerrainAt(x, 12), Is.EqualTo(x >= first && x < first + 2 ? TerrainKind.Empty : TerrainKind.Steel));
                Assert.That(map.TerrainAt(x, 13), Is.EqualTo(TerrainKind.Steel));
            }
        }

        [TestCase(CardinalDirection.Up)] [TestCase(CardinalDirection.Down)]
        [TestCase(CardinalDirection.Left)] [TestCase(CardinalDirection.Right)]
        public void EdgeClipsStripWithoutShiftingItInward(CardinalDirection direction)
        {
            bool horizontal = direction == CardinalDirection.Left || direction == CardinalDirection.Right;
            if (horizontal) Fill(12, 0, 1, 26); else Fill(0, 12, 26, 1);
            var map = new MapState(level);
            float along = direction == CardinalDirection.Up || direction == CardinalDirection.Right ? 64 : 144;
            Assert.That(Fire(map, horizontal ? new Vector2(along, 4) : new Vector2(4, along), direction), Is.EqualTo(4));
            for (int cross = 0; cross < 26; cross++)
                Assert.That(horizontal ? map.TerrainAt(12, cross) : map.TerrainAt(cross, 12),
                    Is.EqualTo(cross == 0 ? TerrainKind.Empty : TerrainKind.Steel));
        }

        [TestCase(TerrainKind.Brick)] [TestCase(TerrainKind.Water)]
        [TestCase(TerrainKind.Grass)] [TestCase(TerrainKind.Ice)]
        public void SteelStripNeverDamagesOtherTerrain(TerrainKind neighbor)
        {
            Fill(12, 12, 1, 1); level.terrain[12 * 26 + 13] = neighbor;
            var map = new MapState(level);
            Assert.That(Fire(map, new Vector2(104, 64), CardinalDirection.Up), Is.EqualTo(4));
            Assert.That(map.TerrainAt(13, 12), Is.EqualTo(neighbor));
            Assert.That(map.BrickMaskAt(13, 12), Is.EqualTo(neighbor == TerrainKind.Brick ? 15 : 0));
        }

        [Test] public void SteelCapableBrickHitStillUsesBrickStripAndLeavesNeighborSteel()
        {
            Fill(13, 12, 1, 1); level.terrain[12 * 26 + 12] = TerrainKind.Brick;
            var map = new MapState(level);
            Spawn(new Vector2(100, 64), CardinalDirection.Up); bullets.Step(map);
            Assert.That(bullets.Hits.Single().Terrain, Is.EqualTo(TerrainKind.Brick));
            Assert.That(map.ApplyBulletHits(bullets.Hits), Is.EqualTo(2));
            Assert.That(map.BrickMaskAt(12, 12), Is.EqualTo(12));
            Assert.That(map.TerrainAt(13, 12), Is.EqualTo(TerrainKind.Steel));
        }

        [Test] public void OrdinaryShotCannotRemoveEitherSteelCell()
        {
            Fill(12, 12, 2, 2); tank.canDestroySteel = false;
            var map = new MapState(level);
            Assert.That(Fire(map, new Vector2(104, 64), CardinalDirection.Up), Is.Zero);
            Assert.That(map.Revision, Is.Zero);
            for (int y = 12; y < 14; y++) for (int x = 12; x < 14; x++)
                Assert.That(map.TerrainAt(x, y), Is.EqualTo(TerrainKind.Steel));
        }
    }
}
