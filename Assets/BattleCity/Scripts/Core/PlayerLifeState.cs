using System;

namespace BattleCity
{
    /// <summary>Edges from one completed combat tick, not sticky events. Exhaustion is also latched on the state.</summary>
    public readonly struct PlayerLifeStepResult
    {
        public bool DeathRecorded { get; }
        public bool Respawned { get; }
        public bool LivesExhaustedThisTick { get; }

        internal PlayerLifeStepResult(bool deathRecorded, bool respawned, bool livesExhaustedThisTick)
        {
            DeathRecorded = deathRecorded;
            Respawned = respawned;
            LivesExhaustedThisTick = livesExhaustedThisTick;
        }
    }

    /// <summary>
    /// Pure lifecycle model. The host calls Step once after damage per combat tick, before enemy spawning,
    /// never while paused. Read IsProtected before damage. There is no independent clock or damage handling.
    /// </summary>
    public sealed class PlayerLifeState
    {
        private bool stepping;

        public PlayerLifeParameters Parameters { get; }
        /// <summary>Total lives still available, including the current life when alive. Death, not respawn, spends one.</summary>
        public int RemainingLives { get; private set; }
        public bool IsAlive { get; private set; } = true;
        public int RespawnRemainingTicks { get; private set; }
        /// <summary>Full future damage phases protected; decremented only after each such phase.</summary>
        public int ProtectionRemainingTicks { get; private set; }
        public bool IsProtected => IsAlive && ProtectionRemainingTicks > 0;
        public bool IsExhausted => RemainingLives == 0;
        public bool IsWaitingForRespawn => !IsAlive && !IsExhausted;
        public bool IsRespawnReady => IsWaitingForRespawn && RespawnRemainingTicks == 0;

        public PlayerLifeState(PlayerLifeParameters parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            Parameters = new PlayerLifeParameters(parameters.InitialLives, parameters.RespawnDelayTicks, parameters.ProtectionTicks);
            RemainingLives = Parameters.InitialLives;
        }

        /// <summary>Pickup reward only. The ceiling never truncates existing lives or revives a dead generation.</summary>
        public bool TryAddLife(int maximumLives)
        {
            if (maximumLives < 1) throw new ArgumentOutOfRangeException(nameof(maximumLives));
            if (stepping || !IsAlive || IsExhausted || RemainingLives >= maximumLives) return false;
            RemainingLives++; // RemainingLives < maximumLives <= int.MaxValue, so cannot overflow.
            return true;
        }

        /// <summary>
        /// playerAliveAfterDamage is the host's settled snapshot of the current generation. Only this model's
        /// successful callback may revive it. Repeated dead snapshots cannot spend another life.
        /// At death D the delay is established, not decremented; D+delay first attempts respawn.
        /// tryRespawn must atomically check occupancy and reset/register the SAME player, or return false
        /// without side effects. It is called at most once per ready tick, must not reenter Step, and must
        /// leave no host changes if it throws. False/throw leaves ready state retryable on the next tick.
        /// True commits protection without decrementing it on this tick; the host must not run movement,
        /// firing or damage again until the next tick. The host must not retry Step within the same tick.
        /// </summary>
        public PlayerLifeStepResult Step(bool playerAliveAfterDamage, Func<bool> tryRespawn)
        {
            if (stepping) throw new InvalidOperationException("Player life Step is not reentrant.");
            if (tryRespawn == null) throw new ArgumentNullException(nameof(tryRespawn));
            if (!IsAlive && playerAliveAfterDamage)
                throw new InvalidOperationException("Only a successful player life respawn callback may revive the player.");
            stepping = true;
            try
            {
                if (IsAlive)
                {
                    if (playerAliveAfterDamage)
                    {
                        if (ProtectionRemainingTicks > 0) ProtectionRemainingTicks--;
                        return default;
                    }
                    IsAlive = false;
                    ProtectionRemainingTicks = 0;
                    RemainingLives--;
                    RespawnRemainingTicks = IsExhausted ? 0 : Parameters.RespawnDelayTicks;
                    return new PlayerLifeStepResult(true, false, IsExhausted);
                }
                if (IsExhausted) return default;
                if (RespawnRemainingTicks > 0) RespawnRemainingTicks--;
                if (RespawnRemainingTicks > 0 || !tryRespawn()) return default;
                IsAlive = true;
                ProtectionRemainingTicks = Parameters.ProtectionTicks;
                return new PlayerLifeStepResult(false, true, false);
            }
            finally { stepping = false; }
        }
    }
}
