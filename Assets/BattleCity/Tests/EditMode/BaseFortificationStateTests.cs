using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace BattleCity.Tests
{
    [Category("BC306Map")]
    public sealed class BaseFortificationStateTests
    {
        private static readonly Rect[] Unoccupied = Array.Empty<Rect>();
        private static readonly Vector2Int BaseCenter = new Vector2Int(104, 8);
        private LevelDefinition level;
        private MapState map;
        [SetUp] public void SetUp()
        {
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            map = new MapState(level);
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(level);
        private static Rect CellBounds(int x, int y) => new Rect(x * 8, y * 8, 8, 8);
        private static Rect[] Occupied(int x, int y) => new[] { CellBounds(x, y) };
        private BaseFortificationState State(int ticks = 1200) => new BaseFortificationState(map, BaseCenter, ticks);
        private static void Tick(BaseFortificationState state, int count, IReadOnlyList<Rect> occupants = null)
        { for (int i = 0; i < count; i++) state.Step(occupants ?? Unoccupied); }
        private void AssertCell(int x, int y, TerrainKind kind, byte mask = 0)
        {
            Assert.That(map.TerrainAt(x, y), Is.EqualTo(kind), $"terrain ({x},{y})");
            Assert.That(map.BrickMaskAt(x, y), Is.EqualTo(mask), $"mask ({x},{y})");
        }

        [Test]
        public void Stage01Uring_IsEightCells_LeftTopRight_NoBaseOrBottomOrOtherChanges()
        {
            var state = State(); state.Activate(Unoccupied);
            for (int y = 0; y < 26; y++) for (int x = 0; x < 26; x++)
            {
                bool ring = (x == 11 || x == 14) && y <= 2 || y == 2 && x >= 11 && x <= 14;
                AssertCell(x, y, ring ? TerrainKind.Steel : TerrainKind.Empty);
            }
            Assert.That(map.Revision, Is.EqualTo(8));
            state.Cancel();
            Assert.That(map.Revision, Is.EqualTo(16));
            var elevated = new BaseFortificationState(map, new Vector2Int(104, 80));
            elevated.Activate(Unoccupied);
            AssertCell(12, 8, TerrainKind.Empty); AssertCell(13, 8, TerrainKind.Empty); // No bottom bar.
            AssertCell(11, 9, TerrainKind.Steel); AssertCell(14, 10, TerrainKind.Steel);
            AssertCell(12, 11, TerrainKind.Steel); AssertCell(12, 9, TerrainKind.Empty);
        }

        [TestCase(8, 8, 5)] [TestCase(200, 8, 5)]
        [TestCase(8, 200, 2)] [TestCase(200, 200, 2)]
        [TestCase(104, 200, 4)]
        public void EdgeAndCornerBases_ClipRingToMapWithoutWrapping(int x, int y, int count)
        {
            var state = new BaseFortificationState(map, new Vector2Int(x, y));
            state.Activate(Unoccupied);
            int actual = 0;
            for (int cy = 0; cy < 26; cy++) for (int cx = 0; cx < 26; cx++)
            {
                bool expected = (cx == x / 8 - 2 || cx == x / 8 + 1 || cy == y / 8 + 1) &&
                    cx >= x / 8 - 2 && cx <= x / 8 + 1 && cy >= y / 8 - 1 && cy <= y / 8 + 1;
                AssertCell(cx, cy, expected ? TerrainKind.Steel : TerrainKind.Empty);
                if (map.TerrainAt(cx, cy) == TerrainKind.Steel) actual++;
            }
            Assert.That(actual, Is.EqualTo(count));
            state.Cancel(); Assert.That(map.Revision, Is.EqualTo(count * 2));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
        [TestCase(11)] [TestCase(12)] [TestCase(13)] [TestCase(14)] [TestCase(15)]
        public void ActivationSnapshotsCurrentPartialBrick_AllMasksRestoreExactly_NotConstructorLayout(byte mask)
        {
            var state = State(2);
            map.SetTerrainCell(11, 0, TerrainKind.Brick, mask); // After construction, before first activation.
            state.Activate(Unoccupied); AssertCell(11, 0, TerrainKind.Steel);
            state.Activate(Unoccupied); // Never overwrite the first mask with steel.
            Tick(state, 2); AssertCell(11, 0, TerrainKind.Brick, mask);
            for (int bit = 0; bit < 4; bit++)
                Assert.That(map.IsSolid(22 + bit % 2, bit / 2), Is.EqualTo((mask & 1 << bit) != 0));
            Assert.That(state.IsActive, Is.False); Assert.That(state.IsRestoring, Is.False);
            Assert.That(state.PendingCellCount, Is.Zero);
        }

        [TestCase(TerrainKind.Empty, (byte)0)] [TestCase(TerrainKind.Brick, (byte)9)]
        [TestCase(TerrainKind.Steel, (byte)0)] [TestCase(TerrainKind.Water, (byte)0)]
        [TestCase(TerrainKind.Grass, (byte)0)] [TestCase(TerrainKind.Ice, (byte)0)]
        public void MixedOriginalLayouts_IncludeEmptyAndNonWallTerrain_ExactRestore(TerrainKind kind, byte mask)
        {
            map.SetTerrainCell(11, 0, kind, mask);
            map.SetTerrainCell(11, 1, TerrainKind.Brick, 6);
            map.SetTerrainCell(14, 1, TerrainKind.Water);
            map.SetTerrainCell(3, 4, TerrainKind.Brick, 10); // Outside ring stays untouched.
            var state = State(1); state.Activate(Unoccupied);
            AssertCell(11, 0, TerrainKind.Steel); AssertCell(14, 1, TerrainKind.Steel);
            Tick(state, 1);
            AssertCell(11, 0, kind, mask); AssertCell(11, 1, TerrainKind.Brick, 6);
            AssertCell(14, 1, TerrainKind.Water); AssertCell(3, 4, TerrainKind.Brick, 10);
        }

        [TestCase(TerrainKind.Grass)] [TestCase(TerrainKind.Ice)]
        public void RestoringNonblockingTerrainNeverWaitsForOccupants(TerrainKind kind)
        {
            map.SetTerrainCell(11, 0, kind);
            var state = State(1); state.Activate(Unoccupied);
            state.Step(Occupied(11, 0));
            AssertCell(11, 0, kind); Assert.That(state.IsRestoring, Is.False);
            Assert.That(state.PendingCellCount, Is.Zero);
        }

        [Test]
        public void RealBulletDamagedBrickMaskAndProjectionRevisionSurviveFortifyAndRestore()
        {
            var definition = ScriptableObject.CreateInstance<TankDefinition>();
            try
            {
                definition.bulletSpeedPixelsPerSecond = 6000;
                map.SetTerrainCell(11, 0, TerrainKind.Brick, 15);
                var bullets = new BulletWorld(); var weapon = new WeaponState(definition, bullets);
                weapon.Step(new TankCommand(CardinalDirection.Right, true), new Vector2(64, 4), CardinalDirection.Right);
                bullets.Step(map); Assert.That(map.ApplyBulletHits(bullets.Hits), Is.EqualTo(2));
                byte mask = map.BrickMaskAt(11, 0); Assert.That(mask, Is.EqualTo(10));
                int revision = map.CellRevisionAt(11, 0);
                var state = State(1); state.Activate(Unoccupied);
                Assert.That(map.CellRevisionAt(11, 0), Is.GreaterThan(revision));
                revision = map.CellRevisionAt(11, 0); state.Step(Unoccupied);
                AssertCell(11, 0, TerrainKind.Brick, mask);
                Assert.That(map.CellRevisionAt(11, 0), Is.GreaterThan(revision));
            }
            finally { Object.DestroyImmediate(definition); }
        }

        [Test]
        public void DurationIs1200FutureCompleteTicks_RefreshNotAddition_ExpiryAtEndOfLastTick()
        {
            var state = State();
            Tick(state, 5); Assert.That(map.Revision, Is.Zero);
            state.Activate(Unoccupied);
            Assert.That(state.DurationTicks, Is.EqualTo(1200)); Assert.That(state.RemainingTicks, Is.EqualTo(1200));
            Tick(state, 1199);
            Assert.That(state.RemainingTicks, Is.EqualTo(1)); AssertCell(11, 0, TerrainKind.Steel);
            state.Activate(Unoccupied);
            Assert.That(state.RemainingTicks, Is.EqualTo(1200)); Assert.That(map.Revision, Is.EqualTo(8));
            Tick(state, 1199); Assert.That(state.IsActive, Is.True); AssertCell(11, 0, TerrainKind.Steel);
            Tick(state, 1); Assert.That(state.RemainingTicks, Is.Zero); AssertCell(11, 0, TerrainKind.Empty);
            int settledRevision = map.Revision;
            Tick(state, 1201); Assert.That(map.Revision, Is.EqualTo(settledRevision));
            Assert.That(state.IsRestoring, Is.False);
        }

        [Test]
        public void ActivationAndRefresh_DeferLiveTankAndBirthReservationIndependently_NoPoseMutation()
        {
            // Combined caller-owned bounds: first a living tank, second a birth reservation.
            var occupants = new List<Rect> { new Rect(80, 0, 16, 16), new Rect(112, 0, 16, 16) };
            Rect tankPose = occupants[0], reservation = occupants[1];
            var state = State(8); state.Activate(occupants);
            Assert.That(state.PendingCellCount, Is.EqualTo(4));
            AssertCell(11, 0, TerrainKind.Empty); AssertCell(14, 0, TerrainKind.Empty);
            AssertCell(12, 2, TerrainKind.Steel);
            Tick(state, 1, occupants); Assert.That(state.PendingCellCount, Is.EqualTo(4));
            Assert.That(occupants[0], Is.EqualTo(tankPose)); Assert.That(occupants[1], Is.EqualTo(reservation));
            occupants.RemoveAt(0); Tick(state, 1, occupants);
            AssertCell(11, 0, TerrainKind.Steel); Assert.That(state.PendingCellCount, Is.EqualTo(2));
            map.SetTerrainCell(11, 0, TerrainKind.Empty);
            occupants.Insert(0, tankPose); state.Activate(occupants);
            AssertCell(11, 0, TerrainKind.Empty); Assert.That(state.PendingCellCount, Is.EqualTo(3));
            occupants.Clear(); Tick(state, 1, occupants);
            AssertCell(11, 0, TerrainKind.Steel); AssertCell(14, 0, TerrainKind.Steel);
            Assert.That(state.PendingCellCount, Is.Zero);
        }

        [Test]
        public void TouchingCellEdgeIsNotOccupation_StrictOverlapDefers()
        {
            var state = State();
            state.Activate(new[] { new Rect(72, 0, 16, 8), new Rect(96, 0, 16, 16) });
            AssertCell(11, 0, TerrainKind.Steel); // Both rectangles only touch the wall's x faces.
            Assert.That(state.PendingCellCount, Is.Zero);
            state.Cancel();
            state.Activate(new[] { new Rect(88 - .01f, 0, .02f, 8) });
            AssertCell(11, 0, TerrainKind.Empty); Assert.That(state.PendingCellCount, Is.EqualTo(1));
        }

        [Test]
        public void Expiry_DefersBlockingOriginalsPerCell_IncludingReservation_RestoresEmptyImmediately()
        {
            map.SetTerrainCell(11, 0, TerrainKind.Brick, 5);
            map.SetTerrainCell(14, 0, TerrainKind.Water);
            var state = State(1); state.Activate(Unoccupied);
            // Highest-level shots can open these cells; simulate settled map holes for the occupancy test.
            map.SetTerrainCell(11, 0, TerrainKind.Empty); map.SetTerrainCell(14, 0, TerrainKind.Empty);
            var occupants = new List<Rect> { CellBounds(11, 0), CellBounds(14, 0), CellBounds(12, 2) };
            state.Step(occupants);
            Assert.That(state.IsActive, Is.False); Assert.That(state.IsRestoring, Is.True);
            Assert.That(state.PendingCellCount, Is.EqualTo(2));
            AssertCell(11, 0, TerrainKind.Empty); AssertCell(14, 0, TerrainKind.Empty);
            AssertCell(12, 2, TerrainKind.Empty); // Removal never waits for the occupant.
            int revision = map.Revision; Tick(state, 5, occupants);
            Assert.That(map.Revision, Is.EqualTo(revision)); Assert.That(state.RemainingTicks, Is.Zero);
            occupants.RemoveAt(0); state.Step(occupants);
            AssertCell(11, 0, TerrainKind.Brick, 5); AssertCell(14, 0, TerrainKind.Empty);
            Assert.That(state.PendingCellCount, Is.EqualTo(1));
            occupants.Clear(); state.Step(occupants);
            AssertCell(14, 0, TerrainKind.Water); Assert.That(state.IsRestoring, Is.False);
            Assert.That(map.Revision, Is.EqualTo(revision + 2));
        }

        [Test]
        public void ExpiredActivationPendingNeverBuildsSteelLater_OnlyOriginalRestorationRetries()
        {
            var state = State(1); state.Activate(Occupied(11, 0));
            Assert.That(state.PendingCellCount, Is.EqualTo(1));
            state.Step(Occupied(11, 0)); AssertCell(11, 0, TerrainKind.Empty);
            Assert.That(state.IsRestoring, Is.False); Assert.That(state.PendingCellCount, Is.Zero);
            int revision = map.Revision; state.Step(Unoccupied);
            AssertCell(11, 0, TerrainKind.Empty); Assert.That(map.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void ReactivationWhileRestoringKeepsFirstSnapshot_CompletedCycleAllowsNewSnapshot()
        {
            map.SetTerrainCell(11, 0, TerrainKind.Brick, 3);
            var state = State(1); state.Activate(Unoccupied);
            map.SetTerrainCell(11, 0, TerrainKind.Empty); state.Step(Occupied(11, 0));
            Assert.That(state.IsRestoring, Is.True);
            state.Activate(Occupied(11, 0)); Assert.That(state.IsActive, Is.True);
            Assert.That(state.IsRestoring, Is.False); AssertCell(11, 0, TerrainKind.Empty);
            state.Step(Unoccupied); AssertCell(11, 0, TerrainKind.Brick, 3);
            map.SetTerrainCell(11, 0, TerrainKind.Grass);
            state.Activate(Unoccupied); state.Step(Unoccupied);
            AssertCell(11, 0, TerrainKind.Grass);
        }

        [TestCase(false)] [TestCase(true)]
        public void CancelFromActiveOrPendingRestore_ForcesExactSnapshotAndClearsAllWork_Idempotent(bool restoring)
        {
            map.SetTerrainCell(11, 0, TerrainKind.Brick, 10);
            var state = State(1); state.Activate(Occupied(14, 0));
            map.SetTerrainCell(11, 0, TerrainKind.Empty);
            if (restoring) state.Step(Occupied(11, 0));
            state.Cancel(); AssertCell(11, 0, TerrainKind.Brick, 10); AssertCell(14, 0, TerrainKind.Empty);
            Assert.That(state.IsActive, Is.False); Assert.That(state.IsRestoring, Is.False);
            Assert.That(state.RemainingTicks, Is.Zero); Assert.That(state.PendingCellCount, Is.Zero);
            int revision = map.Revision;
            state.Cancel(); Tick(state, 2); Assert.That(map.Revision, Is.EqualTo(revision));
            map.SetTerrainCell(11, 0, TerrainKind.Brick, 7);
            state.Activate(Unoccupied); state.Cancel(); AssertCell(11, 0, TerrainKind.Brick, 7);
        }

        [TestCase(false)] [TestCase(true)]
        public void FortifiedCellsUseExistingSteelBulletRules_DestroyedWallStaysOpenUntilNewActivation(bool topLevel)
        {
            var definition = ScriptableObject.CreateInstance<TankDefinition>();
            try
            {
                definition.canDestroySteel = topLevel; definition.bulletSpeedPixelsPerSecond = 6000;
                var state = State(3); state.Activate(Unoccupied);
                var bullets = new BulletWorld(); var weapon = new WeaponState(definition, bullets);
                Assert.That(weapon.Step(new TankCommand(CardinalDirection.Right, true), new Vector2(64, 4), CardinalDirection.Right), Is.True);
                bullets.Step(map); Assert.That(bullets.Hits.Count, Is.EqualTo(1));
                Assert.That(bullets.Hits[0].Terrain, Is.EqualTo(TerrainKind.Steel));
                Assert.That(bullets.Active.Count, Is.Zero);
                Assert.That(map.ApplyBulletHits(bullets.Hits), Is.EqualTo(topLevel ? 4 : 0));
                AssertCell(11, 0, topLevel ? TerrainKind.Empty : TerrainKind.Steel);
                int revision = map.Revision; state.Step(Unoccupied);
                Assert.That(map.Revision, Is.EqualTo(revision), "No passive repair of already completed steel cells");
                state.Activate(Unoccupied); AssertCell(11, 0, TerrainKind.Steel);
                Tick(state, 3); AssertCell(11, 0, TerrainKind.Empty);
            }
            finally { Object.DestroyImmediate(definition); }
        }

        [Test]
        public void SetTerrainCell_ExactDataAndCellRevision_OnlyChangedCellDirty_InvalidWritesAtomic()
        {
            Assert.That(map.SetTerrainCell(11, 0, TerrainKind.Brick, 5), Is.True);
            Assert.That(map.Revision, Is.EqualTo(1)); Assert.That(map.CellRevisionAt(11, 0), Is.EqualTo(1));
            Assert.That(map.CellRevisionAt(12, 0), Is.Zero);
            Assert.That(map.SetTerrainCell(11, 0, TerrainKind.Brick, 5), Is.False);
            Assert.That(map.Revision, Is.EqualTo(1));
            map.SetTerrainCell(11, 0, TerrainKind.Brick, 6);
            Assert.That(map.CellRevisionAt(11, 0), Is.EqualTo(2));
            foreach (byte mask in new byte[] { 0, 16, 255 })
                Assert.Throws<ArgumentException>(() => map.SetTerrainCell(11, 0, TerrainKind.Brick, mask));
            Assert.Throws<ArgumentException>(() => map.SetTerrainCell(11, 0, TerrainKind.Steel, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => map.SetTerrainCell(11, 0, (TerrainKind)100));
            Assert.Throws<ArgumentOutOfRangeException>(() => map.SetTerrainCell(-1, 0, TerrainKind.Empty));
            Assert.Throws<ArgumentOutOfRangeException>(() => map.SetTerrainCell(26, 0, TerrainKind.Empty));
            AssertCell(11, 0, TerrainKind.Brick, 6); Assert.That(map.Revision, Is.EqualTo(2));
            map.SetTerrainCell(11, 0, TerrainKind.Empty); AssertCell(11, 0, TerrainKind.Empty);
            Assert.That(map.IsSolid(22, 0), Is.False);
        }

        [Test]
        public void ArenaMapView_IncrementalRevisionProjectsActivationAndRestoration_NoSceneOrAssetChanges()
        {
            var objects = new List<Object>();
            try
            {
                var root = new GameObject("BC306 transient projection", typeof(Grid)); objects.Add(root);
                var tileObject = new GameObject("BC306 tiles", typeof(Tilemap)); objects.Add(tileObject);
                tileObject.transform.SetParent(root.transform, false);
                var view = root.AddComponent<ArenaMapView>(); view.obstacles = tileObject.GetComponent<Tilemap>();
                var texture = new Texture2D(8, 8); objects.Add(texture);
                var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * .5f, 16); objects.Add(sprite);
                for (int i = 0; i < 4; i++)
                {
                    var brick = ScriptableObject.CreateInstance<Tile>(); objects.Add(brick); brick.sprite = sprite;
                    var steel = ScriptableObject.CreateInstance<Tile>(); objects.Add(steel); steel.sprite = sprite;
                    view.brickTiles[i] = brick; view.steelTiles[i] = steel;
                }
                map.SetTerrainCell(11, 0, TerrainKind.Brick, 15);
                map.SetTerrainCell(2, 3, TerrainKind.Steel);
                view.Render(map); var untouched = view.obstacles.GetTile(new Vector3Int(2, 3, 0));
                Assert.That(view.obstacles.GetTile(new Vector3Int(11, 0, 0)), Is.SameAs(view.brickTiles[3]));
                var state = State(1); state.Activate(Unoccupied); view.Render(map);
                Assert.That(view.obstacles.GetTile(new Vector3Int(11, 0, 0)), Is.SameAs(view.steelTiles[3]));
                Assert.That(view.obstacles.GetTile(new Vector3Int(12, 2, 0)), Is.SameAs(view.steelTiles[2]));
                state.Step(Unoccupied); view.Render(map);
                Assert.That(view.obstacles.GetTile(new Vector3Int(11, 0, 0)), Is.SameAs(view.brickTiles[3]));
                Assert.That(view.obstacles.HasTile(new Vector3Int(12, 2, 0)), Is.False);
                Assert.That(view.obstacles.GetTile(new Vector3Int(2, 3, 0)), Is.SameAs(untouched));
                view.Render(map); Assert.That(view.obstacles.GetTile(new Vector3Int(11, 0, 0)), Is.SameAs(view.brickTiles[3]));
            }
            finally
            {
                // Destroy the view before its source tiles/sprite.
                if (objects.Count > 0) Object.DestroyImmediate(objects[0]);
                for (int i = objects.Count - 1; i >= 1; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            }
        }

        [TestCase(105, 8)] [TestCase(104, 9)] [TestCase(0, 8)] [TestCase(208, 8)]
        [TestCase(104, 0)] [TestCase(104, 208)]
        public void InvalidBaseCentersAreRejectedWithoutRoundingOrMapMutation(int x, int y)
        {
            Assert.Throws<ArgumentException>(() => new BaseFortificationState(map, new Vector2Int(x, y)));
            Assert.That(map.Revision, Is.Zero);
        }

        [Test]
        public void InvalidDurationAndNullDependenciesAreRejected_CancelBeforeActivationIsSafe()
        {
            Assert.Throws<ArgumentNullException>(() => new BaseFortificationState(null, BaseCenter));
            Assert.Throws<ArgumentOutOfRangeException>(() => State(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => State(-1));
            var state = State(7); state.Cancel(); Assert.That(state.DurationTicks, Is.EqualTo(7));
            Assert.Throws<ArgumentNullException>(() => state.Activate(null));
            Assert.Throws<ArgumentNullException>(() => state.Step(null));
            Assert.That(state.RemainingTicks, Is.Zero); Assert.That(map.Revision, Is.Zero);
        }
    }
}
