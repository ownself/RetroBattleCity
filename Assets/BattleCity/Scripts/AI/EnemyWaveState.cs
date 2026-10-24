using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleCity
{
    /// <summary>A reservation becomes a live enemy only when the host's atomic creation callback succeeds.</summary>
    public readonly struct EnemySpawnRequest
    {
        public int SequenceIndex { get; }
        public string DefinitionId { get; }
        public TankDefinition Definition { get; }
        public Vector2Int PositionPixels { get; }
        public uint BrainSeed { get; }

        internal EnemySpawnRequest(int index, string id, TankDefinition definition, Vector2Int position, uint seed)
        {
            SequenceIndex = index; DefinitionId = id; Definition = definition;
            PositionPixels = position; BrainSeed = seed;
        }
    }

    /// <summary>
    /// Scene-free single-reservation wave. Call once after damage/deaths per combat tick, never while paused.
    /// Host occupancy includes terrain, player, live tanks and OTHER reservations, not this state's own reservation.
    /// </summary>
    public sealed class EnemyWaveState
    {
        private readonly string[] sequence;
        private readonly TankDefinition[] definitions;
        private readonly Vector2Int[] spawnPoints;
        private readonly List<Vector2Int> availablePoints = new List<Vector2Int>();
        private bool stepping;

        public int TotalCount => sequence.Length;
        public int SpawnedCount { get; private set; }
        /// <summary>Not yet successfully created; includes ReservedCount. Total = Spawned + Pending.</summary>
        public int PendingCount => TotalCount - SpawnedCount;
        public int ReservedCount => Reservation.HasValue ? 1 : 0;
        public int UnreservedCount => PendingCount - ReservedCount;
        /// <summary>Latest host snapshot, plus a successful creation in that Step; not a death/event ledger.</summary>
        public int AliveCount { get; private set; }
        public int MaxActiveEnemies { get; }
        public int PreparationTicks { get; }
        public int SpawnIntervalTicks { get; }
        public uint InitialSeed { get; }
        public uint RandomState { get; private set; }
        public EnemySpawnRequest? Reservation { get; private set; }
        public int PreparationRemainingTicks { get; private set; }
        public int IntervalRemainingTicks { get; private set; }
        public bool SequenceExhausted => PendingCount == 0;

        public EnemyWaveState(LevelDefinition level, EnemyWaveDefinition configuration)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            level.Validate(); configuration.Validate();
            sequence = (string[])level.enemySequence.Clone();
            spawnPoints = (Vector2Int[])level.enemySpawnPixels.Clone();
            definitions = new TankDefinition[sequence.Length];
            var mapping = new Dictionary<string, TankDefinition>(StringComparer.Ordinal);
            foreach (var definition in configuration.tankDefinitions) mapping.Add(definition.definitionId, definition);
            for (int i = 0; i < sequence.Length; i++)
                if (!mapping.TryGetValue(sequence[i], out definitions[i]))
                    throw new InvalidOperationException("Unknown enemy wave definition ID: " + sequence[i]);
            MaxActiveEnemies = level.maxActiveEnemies;
            PreparationTicks = configuration.preparationTicks;
            SpawnIntervalTicks = configuration.spawnIntervalTicks;
            InitialSeed = RandomState = configuration.seed;
        }

        /// <summary>
        /// Returns true only on successful materialization. No callback may reenter Step.
        /// tryCreate must atomically register the tank AND its AI, or return false without side effects.
        /// New enemies must not move/fire until the next host tick. No death notification is needed.
        /// </summary>
        public bool Step(int aliveCount, Func<Vector2Int, bool> isOccupied, Func<EnemySpawnRequest, bool> tryCreate)
        {
            if (stepping) throw new InvalidOperationException("Enemy wave Step is not reentrant.");
            if (aliveCount < 0 || aliveCount > MaxActiveEnemies - ReservedCount)
                throw new ArgumentOutOfRangeException(nameof(aliveCount), "Live enemies plus reservations exceed wave capacity.");
            if (isOccupied == null) throw new ArgumentNullException(nameof(isOccupied));
            if (tryCreate == null) throw new ArgumentNullException(nameof(tryCreate));
            stepping = true;
            try
            {
                AliveCount = aliveCount;
                if (IntervalRemainingTicks > 0 && --IntervalRemainingTicks > 0) return false;
                if (Reservation.HasValue)
                {
                    if (PreparationRemainingTicks > 0) PreparationRemainingTicks--;
                    if (PreparationRemainingTicks > 0) return false;
                    var request = Reservation.Value;
                    // Occupancy can change during preparation. Never push an existing tank aside.
                    if (AliveCount >= MaxActiveEnemies || isOccupied(request.PositionPixels)) return false;
                    if (!tryCreate(request)) return false;
                    SpawnedCount++;
                    AliveCount++;
                    Reservation = null;
                    IntervalRemainingTicks = SpawnIntervalTicks;
                    return true;
                }
                if (SequenceExhausted || AliveCount >= MaxActiveEnemies) return false;
                availablePoints.Clear();
                foreach (var point in spawnPoints)
                    if (!isOccupied(point)) availablePoints.Add(point);
                if (availablePoints.Count == 0) return false;
                RandomState = NextRandom(RandomState);
                var position = availablePoints[(int)(RandomState % (uint)availablePoints.Count)];
                Reservation = new EnemySpawnRequest(SpawnedCount, sequence[SpawnedCount], definitions[SpawnedCount],
                    position, DeriveBrainSeed(InitialSeed, SpawnedCount));
                PreparationRemainingTicks = PreparationTicks;
                return false;
            }
            finally { stepping = false; }
        }

        private static uint NextRandom(uint value)
        {
            value ^= value << 13; value ^= value >> 17; value ^= value << 5; return value;
        }

        // Index-based derivation is independent of blocked ticks or creation retries. Zero is forbidden by EnemyBrain.
        private static uint DeriveBrainSeed(uint seed, int index)
        {
            unchecked
            {
                uint value = seed + 0x9e3779b9u * ((uint)index + 1u);
                value = (value ^ (value >> 16)) * 0x85ebca6bu;
                value = (value ^ (value >> 13)) * 0xc2b2ae35u;
                value ^= value >> 16;
                return value == 0 ? 1u : value;
            }
        }
    }
}
