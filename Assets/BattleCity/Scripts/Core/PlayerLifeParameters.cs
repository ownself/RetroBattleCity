using System;

namespace BattleCity
{
    /// <summary>Immutable prototype tuning; these values do not claim original-game accuracy.</summary>
    public sealed class PlayerLifeParameters
    {
        public int InitialLives { get; }
        public int RespawnDelayTicks { get; }
        public int ProtectionTicks { get; }
        public bool RequiresCalibration => true;
        public static PlayerLifeParameters Prototype { get; } = new PlayerLifeParameters(3, 60, 120);

        public PlayerLifeParameters(int initialLives, int respawnDelayTicks, int protectionTicks)
        {
            if (initialLives < 1) throw new ArgumentOutOfRangeException(nameof(initialLives));
            if (respawnDelayTicks < 1) throw new ArgumentOutOfRangeException(nameof(respawnDelayTicks));
            if (protectionTicks < 1) throw new ArgumentOutOfRangeException(nameof(protectionTicks));
            InitialLives = initialLives;
            RespawnDelayTicks = respawnDelayTicks;
            ProtectionTicks = protectionTicks;
        }
    }
}
