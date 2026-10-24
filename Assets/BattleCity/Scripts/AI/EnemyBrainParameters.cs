using System;

namespace BattleCity
{
    /// <summary>Prototype tuning only: none of these values claim original-game accuracy.</summary>
    public sealed class EnemyBrainParameters
    {
        public int MinDirectionTicks { get; }
        public int MaxDirectionTicks { get; }
        public int DownWeight { get; }
        public int OtherDirectionWeight { get; }
        public bool RequiresCalibration => true;
        public static EnemyBrainParameters Prototype { get; } = new EnemyBrainParameters(60, 180, 4, 1);

        public EnemyBrainParameters(int minDirectionTicks, int maxDirectionTicks, int downWeight, int otherDirectionWeight)
        {
            if (minDirectionTicks < 1 || maxDirectionTicks < minDirectionTicks || maxDirectionTicks == int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(minDirectionTicks));
            if (downWeight < 1 || otherDirectionWeight < 1 || (long)downWeight + 3L * otherDirectionWeight > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(downWeight));
            MinDirectionTicks = minDirectionTicks;
            MaxDirectionTicks = maxDirectionTicks;
            DownWeight = downWeight;
            OtherDirectionWeight = otherDirectionWeight;
        }
    }
}
