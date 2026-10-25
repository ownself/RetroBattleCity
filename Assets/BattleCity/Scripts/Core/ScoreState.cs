using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BattleCity
{
    /// <summary>Immutable row. Definition IDs, not visual color or current HP, classify enemy kills.</summary>
    public sealed class EnemyScoreRow
    {
        public string DefinitionId { get; }
        public int Count { get; }
        public long Points { get; }
        internal EnemyScoreRow(string id, int count, long points) { DefinitionId = id; Count = count; Points = points; }
    }

    public sealed class PickupScoreRow
    {
        public PickupKind Kind { get; }
        public int Count { get; }
        public long Points { get; }
        internal PickupScoreRow(PickupKind kind, int count, long points) { Kind = kind; Count = count; Points = points; }
    }

    /// <summary>One per arena; only committed deaths and fresh collection receipts can write it.</summary>
    public sealed class ScoreState
    {
        private readonly SortedDictionary<string, EnemyScoreRow> enemies = new SortedDictionary<string, EnemyScoreRow>(StringComparer.Ordinal);
        private readonly PickupScoreRow[] pickups = new PickupScoreRow[Enum.GetValues(typeof(PickupKind)).Length];
        public long EnemyPoints { get; private set; }
        public long PickupPoints { get; private set; }
        public long Total => EnemyPoints + PickupPoints;
        public int DefeatedCount { get; private set; }
        public int CollectedCount { get; private set; }
        public ReadOnlyCollection<EnemyScoreRow> Enemies => new List<EnemyScoreRow>(enemies.Values).AsReadOnly();
        public ReadOnlyCollection<PickupScoreRow> Pickups => Array.AsReadOnly((PickupScoreRow[])pickups.Clone());

        public ScoreState()
        {
            for (int i = 0; i < pickups.Length; i++) pickups[i] = new PickupScoreRow((PickupKind)i, 0, 0);
        }

        internal void RecordEnemy(TankState tank)
        {
            enemies.TryGetValue(tank.DefinitionId, out var row);
            enemies[tank.DefinitionId] = new EnemyScoreRow(tank.DefinitionId, (row?.Count ?? 0) + 1, (row?.Points ?? 0) + tank.KillScore);
            EnemyPoints += tank.KillScore;
            DefeatedCount++;
        }

        internal void RecordPickup(PickupKind kind, int points)
        {
            var row = pickups[(int)kind];
            pickups[(int)kind] = new PickupScoreRow(kind, row.Count + 1, row.Points + points);
            PickupPoints += points;
            CollectedCount++;
        }
    }

    /// <summary>Frozen once before terminal notifications. No live session/asset/terrain references.</summary>
    public sealed class StageResult
    {
        public int StageNumber { get; }
        public SessionPhase Outcome { get; }
        public SessionFailureReason FailureReason { get; }
        public long EndTick { get; }
        public int RemainingLives { get; }
        public long EnemyPoints { get; }
        public long PickupPoints { get; }
        public long Total { get; }
        public int DefeatedCount { get; }
        public int CollectedCount { get; }
        public ReadOnlyCollection<EnemyScoreRow> Enemies { get; }
        public ReadOnlyCollection<PickupScoreRow> Pickups { get; }

        internal StageResult(int stage, SessionPhase outcome, SessionFailureReason failure, long tick, int lives, ScoreState score)
        {
            StageNumber = stage; Outcome = outcome; FailureReason = failure; EndTick = tick; RemainingLives = lives;
            EnemyPoints = score.EnemyPoints; PickupPoints = score.PickupPoints; Total = score.Total;
            DefeatedCount = score.DefeatedCount; CollectedCount = score.CollectedCount;
            Enemies = score.Enemies; Pickups = score.Pickups;
        }
    }
}
