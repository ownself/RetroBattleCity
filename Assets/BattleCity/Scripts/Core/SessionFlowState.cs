using System;

namespace BattleCity
{
    public enum SessionPhase
    {
        Ready,
        Playing,
        Paused,
        Won,
        Lost
    }

    [Flags]
    public enum SessionFailureReason
    {
        None = 0,
        BaseDestroyed = 1,
        PlayerLivesExhausted = 2
    }

    /// <summary>Pure session state. The host settles once at the end of a complete combat tick, after damage and spawning.</summary>
    public sealed class SessionFlowState
    {
        public SessionPhase Phase { get; private set; } = SessionPhase.Ready;
        public SessionFailureReason FailureReason { get; private set; } = SessionFailureReason.None;
        public bool IsTerminal => Phase == SessionPhase.Won || Phase == SessionPhase.Lost;

        public bool TryStart()
        {
            if (Phase != SessionPhase.Ready) return false;
            Phase = SessionPhase.Playing;
            return true;
        }

        public bool SetPaused(bool paused)
        {
            if (paused && Phase == SessionPhase.Playing)
            {
                Phase = SessionPhase.Paused;
                return true;
            }
            if (!paused && Phase == SessionPhase.Paused)
            {
                Phase = SessionPhase.Playing;
                return true;
            }
            return false;
        }

        public bool Settle(bool baseFailure, bool playerLifeFailure, bool sequenceExhausted,
            int aliveEnemyCount, int pendingEnemyCount, int reservedEnemyCount)
        {
            if (aliveEnemyCount < 0) throw new ArgumentOutOfRangeException(nameof(aliveEnemyCount));
            if (pendingEnemyCount < 0) throw new ArgumentOutOfRangeException(nameof(pendingEnemyCount));
            if (reservedEnemyCount < 0) throw new ArgumentOutOfRangeException(nameof(reservedEnemyCount));
            if (reservedEnemyCount > pendingEnemyCount)
                throw new ArgumentException("Reserved enemies cannot exceed pending enemies.", nameof(reservedEnemyCount));
            if (Phase != SessionPhase.Playing) return false;

            var reason = SessionFailureReason.None;
            if (baseFailure) reason |= SessionFailureReason.BaseDestroyed;
            if (playerLifeFailure) reason |= SessionFailureReason.PlayerLivesExhausted;
            if (reason != SessionFailureReason.None)
            {
                FailureReason = reason;
                Phase = SessionPhase.Lost;
                return true;
            }
            if (sequenceExhausted && aliveEnemyCount == 0 && pendingEnemyCount == 0 && reservedEnemyCount == 0)
            {
                Phase = SessionPhase.Won;
                return true;
            }
            return false;
        }
    }
}
