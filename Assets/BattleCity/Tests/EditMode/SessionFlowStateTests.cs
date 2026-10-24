using System;
using NUnit.Framework;

namespace BattleCity.Tests
{
    public sealed class SessionFlowStateTests
    {
        private static SessionFlowState At(SessionPhase phase)
        {
            var state = new SessionFlowState();
            if (phase == SessionPhase.Ready) return state;
            state.TryStart();
            if (phase == SessionPhase.Paused) state.SetPaused(true);
            if (phase == SessionPhase.Won) state.Settle(false, false, true, 0, 0, 0);
            if (phase == SessionPhase.Lost) state.Settle(true, true, true, 0, 0, 0);
            return state;
        }

        private static string Snapshot(SessionFlowState state) =>
            $"{state.Phase}|{state.FailureReason}|{state.IsTerminal}";

        [Test]
        public void NewSession_IsReadyWithNoFailureAndNotTerminal()
        {
            var state = new SessionFlowState();
            Assert.That(state.Phase, Is.EqualTo(SessionPhase.Ready));
            Assert.That(state.FailureReason, Is.EqualTo(SessionFailureReason.None));
            Assert.That(state.IsTerminal, Is.False);
            Assert.That((int)SessionFailureReason.None, Is.Zero);
            Assert.That((int)SessionFailureReason.BaseDestroyed, Is.EqualTo(1));
            Assert.That((int)SessionFailureReason.PlayerLivesExhausted, Is.EqualTo(2));
            Assert.That(typeof(SessionFailureReason).IsDefined(typeof(FlagsAttribute), false), Is.True);
        }

        [TestCase(SessionPhase.Ready)]
        [TestCase(SessionPhase.Playing)]
        [TestCase(SessionPhase.Paused)]
        [TestCase(SessionPhase.Won)]
        [TestCase(SessionPhase.Lost)]
        public void TryStart_TransitionsOnlyFromReady_AndRepeatedCallIsFalse(SessionPhase phase)
        {
            var state = At(phase);
            string before = Snapshot(state);
            Assert.That(state.TryStart(), Is.EqualTo(phase == SessionPhase.Ready));
            if (phase == SessionPhase.Ready)
            {
                Assert.That(state.Phase, Is.EqualTo(SessionPhase.Playing));
                Assert.That(state.FailureReason, Is.EqualTo(SessionFailureReason.None));
                Assert.That(state.IsTerminal, Is.False);
            }
            else Assert.That(Snapshot(state), Is.EqualTo(before));
            before = Snapshot(state);
            Assert.That(state.TryStart(), Is.False);
            Assert.That(Snapshot(state), Is.EqualTo(before));
        }

        [TestCase(SessionPhase.Ready, false)]
        [TestCase(SessionPhase.Ready, true)]
        [TestCase(SessionPhase.Playing, false)]
        [TestCase(SessionPhase.Playing, true)]
        [TestCase(SessionPhase.Paused, false)]
        [TestCase(SessionPhase.Paused, true)]
        [TestCase(SessionPhase.Won, false)]
        [TestCase(SessionPhase.Won, true)]
        [TestCase(SessionPhase.Lost, false)]
        [TestCase(SessionPhase.Lost, true)]
        public void SetPaused_ReturnsTrueOnlyForActualPlayingPausedTransition(SessionPhase phase, bool paused)
        {
            var state = At(phase);
            string before = Snapshot(state);
            bool changes = (phase == SessionPhase.Playing && paused) || (phase == SessionPhase.Paused && !paused);
            Assert.That(state.SetPaused(paused), Is.EqualTo(changes));
            if (changes)
            {
                Assert.That(state.Phase, Is.EqualTo(paused ? SessionPhase.Paused : SessionPhase.Playing));
                Assert.That(state.FailureReason, Is.EqualTo(SessionFailureReason.None));
                Assert.That(state.IsTerminal, Is.False);
            }
            else Assert.That(Snapshot(state), Is.EqualTo(before));
            before = Snapshot(state);
            Assert.That(state.SetPaused(paused), Is.False);
            Assert.That(Snapshot(state), Is.EqualTo(before));
        }

        [TestCase(false, 0, 0, 0)] // No enemies yet is not sequence exhaustion.
        [TestCase(false, 0, 1, 0)] // Waiting for the next wave.
        [TestCase(false, 0, 1, 1)] // Preparing or blocked at a reserved spawn.
        [TestCase(true, 1, 0, 0)]
        [TestCase(true, 0, 1, 0)]
        [TestCase(true, 0, 1, 1)]
        [TestCase(true, int.MaxValue, int.MaxValue, int.MaxValue)]
        public void Settle_IncompleteEnemySequenceOrOutstandingEnemiesNeverWin(
            bool exhausted, int alive, int pending, int reserved)
        {
            var state = At(SessionPhase.Playing);
            string before = Snapshot(state);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(state.Settle(false, false, exhausted, alive, pending, reserved), Is.False);
                Assert.That(Snapshot(state), Is.EqualTo(before));
            }
            Assert.That(state.Settle(false, false, true, 0, 0, 0), Is.True);
            Assert.That(state.Phase, Is.EqualTo(SessionPhase.Won));
            Assert.That(state.FailureReason, Is.EqualTo(SessionFailureReason.None));
            Assert.That(state.IsTerminal, Is.True);
        }

        [TestCase(true, false, true, 0, 0, 0, SessionFailureReason.BaseDestroyed)]
        [TestCase(false, true, true, 0, 0, 0, SessionFailureReason.PlayerLivesExhausted)]
        [TestCase(true, true, true, 0, 0, 0, SessionFailureReason.BaseDestroyed | SessionFailureReason.PlayerLivesExhausted)]
        [TestCase(true, false, false, 1, 2, 1, SessionFailureReason.BaseDestroyed)]
        [TestCase(false, true, false, 1, 2, 1, SessionFailureReason.PlayerLivesExhausted)]
        [TestCase(true, true, false, 1, 2, 1, SessionFailureReason.BaseDestroyed | SessionFailureReason.PlayerLivesExhausted)]
        public void Settle_FailurePrecedesWinAndEnemyGuards_AndRetainsBothReasons(
            bool baseFailure, bool lifeFailure, bool exhausted, int alive, int pending, int reserved,
            SessionFailureReason reason)
        {
            var state = At(SessionPhase.Playing);
            Assert.That(state.Settle(baseFailure, lifeFailure, exhausted, alive, pending, reserved), Is.True);
            Assert.That(state.Phase, Is.EqualTo(SessionPhase.Lost));
            Assert.That(state.FailureReason, Is.EqualTo(reason));
            Assert.That(state.IsTerminal, Is.True);
            string terminal = Snapshot(state);
            Assert.That(state.Settle(!baseFailure, !lifeFailure, true, 0, 0, 0), Is.False);
            Assert.That(Snapshot(state), Is.EqualTo(terminal));
        }

        [TestCase(SessionPhase.Ready)]
        [TestCase(SessionPhase.Paused)]
        [TestCase(SessionPhase.Won)]
        [TestCase(SessionPhase.Lost)]
        public void Settle_OutsidePlayingIgnoresBothWinAndFailureSnapshots(SessionPhase phase)
        {
            var state = At(phase);
            string before = Snapshot(state);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(state.Settle(false, false, true, 0, 0, 0), Is.False);
                Assert.That(state.Settle(true, true, true, 0, 0, 0), Is.False);
                Assert.That(Snapshot(state), Is.EqualTo(before));
            }
        }

        [TestCase(SessionPhase.Ready)]
        [TestCase(SessionPhase.Playing)]
        [TestCase(SessionPhase.Paused)]
        [TestCase(SessionPhase.Won)]
        [TestCase(SessionPhase.Lost)]
        public void Settle_InvalidCountsThrowInEveryPhaseWithoutChangingState(SessionPhase phase)
        {
            var state = At(phase);
            string before = Snapshot(state);
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => state.Settle(true, true, true, -1, 0, 0)).ParamName,
                Is.EqualTo("aliveEnemyCount"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => state.Settle(true, true, true, 0, -1, 0)).ParamName,
                Is.EqualTo("pendingEnemyCount"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => state.Settle(true, true, true, 0, 0, -1)).ParamName,
                Is.EqualTo("reservedEnemyCount"));
            Assert.That(Assert.Throws<ArgumentException>(() => state.Settle(true, true, true, 0, 0, 1)).ParamName,
                Is.EqualTo("reservedEnemyCount"));
            Assert.That(Assert.Throws<ArgumentException>(() => state.Settle(false, false, false, 0, int.MaxValue - 1, int.MaxValue)).ParamName,
                Is.EqualTo("reservedEnemyCount"));
            Assert.That(Snapshot(state), Is.EqualTo(before));
        }

        [Test]
        public void PauseResume_DoesNotLatchIgnoredFailure_AndAllowsSubsequentWin()
        {
            var state = At(SessionPhase.Playing);
            Assert.That(state.SetPaused(true), Is.True);
            Assert.That(state.Settle(true, true, true, 0, 0, 0), Is.False);
            Assert.That(state.SetPaused(false), Is.True);
            Assert.That(state.Settle(false, false, true, 0, 0, 0), Is.True);
            Assert.That(state.Phase, Is.EqualTo(SessionPhase.Won));
            Assert.That(state.FailureReason, Is.EqualTo(SessionFailureReason.None));
            Assert.That(state.IsTerminal, Is.True);
        }

        [Test]
        public void NewSessions_AreIndependentAndDoNotResetAnExistingTerminalState()
        {
            var first = At(SessionPhase.Lost);
            string terminal = Snapshot(first);
            var second = new SessionFlowState();
            Assert.That(second.Phase, Is.EqualTo(SessionPhase.Ready));
            Assert.That(second.FailureReason, Is.EqualTo(SessionFailureReason.None));
            Assert.That(second.TryStart(), Is.True);
            Assert.That(second.Settle(false, false, true, 0, 0, 0), Is.True);
            Assert.That(Snapshot(first), Is.EqualTo(terminal));
        }
    }
}
