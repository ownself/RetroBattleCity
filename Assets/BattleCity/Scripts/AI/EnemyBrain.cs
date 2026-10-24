using System;

namespace BattleCity
{
    public enum EnemyDecisionState { Moving, Blocked, Dead }

    /// <summary>Tick-only local controller. Observes current movement options, never player input or a route.</summary>
    public sealed class EnemyBrain
    {
        public uint InitialSeed { get; }
        public uint RandomState { get; private set; }
        public EnemyBrainParameters Parameters { get; }
        public EnemyDecisionState State { get; private set; } = EnemyDecisionState.Moving;
        public CardinalDirection Direction { get; private set; }
        public int DirectionTicksRemaining { get; private set; }
        public long DecisionTicks { get; private set; }

        internal EnemyBrain(uint seed, EnemyBrainParameters parameters, CardinalDirection initialDirection)
        {
            if (seed == 0) throw new ArgumentOutOfRangeException(nameof(seed), "Xorshift32 requires a nonzero explicit seed.");
            if (initialDirection == CardinalDirection.None) throw new ArgumentOutOfRangeException(nameof(initialDirection));
            initialDirection.Vector();
            InitialSeed = RandomState = seed;
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            Direction = initialDirection;
            DirectionTicksRemaining = Parameters.MinDirectionTicks;
        }

        internal TankCommand Decide(bool alive, int movableDirections)
        {
            if (!alive)
            {
                State = EnemyDecisionState.Dead;
                return new TankCommand(CardinalDirection.None);
            }
            DecisionTicks++;
            if (DirectionTicksRemaining > 0) DirectionTicksRemaining--;
            if (movableDirections == 0)
            {
                State = EnemyDecisionState.Blocked;
                // Retain heading and retry next tick; a stationary tank can still shoot through brick.
                return new TankCommand(Direction, true);
            }
            bool blocked = (movableDirections & (1 << (int)Direction)) == 0;
            if (blocked || State == EnemyDecisionState.Blocked || DirectionTicksRemaining == 0)
            {
                Direction = ChooseDirection(movableDirections);
                DirectionTicksRemaining = Parameters.MinDirectionTicks + (int)(NextRandom() %
                    (uint)(Parameters.MaxDirectionTicks - Parameters.MinDirectionTicks + 1));
            }
            State = EnemyDecisionState.Moving;
            return new TankCommand(Direction, true); // Shared WeaponState owns cadence and projectile capacity.
        }

        private CardinalDirection ChooseDirection(int mask)
        {
            int total = 0;
            for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) total += Weight(i);
            uint roll = NextRandom() % (uint)total;
            for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0)
            {
                int weight = Weight(i);
                if (roll < weight) return (CardinalDirection)i;
                roll -= (uint)weight;
            }
            throw new InvalidOperationException("Direction mask must contain a cardinal option.");
        }

        private int Weight(int direction) => direction == (int)CardinalDirection.Down
            ? Parameters.DownWeight : Parameters.OtherDirectionWeight;

        // Explicit xorshift32 state and fixed draw order: direction, then inclusive duration.
        private uint NextRandom()
        {
            uint value = RandomState;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            return RandomState = value;
        }
    }
}
