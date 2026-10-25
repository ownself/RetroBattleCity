using NUnit.Framework;

namespace BattleCity.Tests
{
    public sealed class SinglePlayerFlowTests
    {
        private static void Press(SinglePlayerFlowState flow, PlayerButtons button, SessionPhase phase = SessionPhase.Ready)
        {
            flow.Sample(PlayerButtons.None, true, phase);
            flow.Sample(button, true, phase);
        }

        [Test]
        public void HeldConfirmCannotSkipIntroOrLeakIntoBattle()
        {
            var flow = new SinglePlayerFlowState();
            Assert.AreEqual(SinglePlayerAction.Restart, flow.Sample(PlayerButtons.Start, true, SessionPhase.Ready));
            for (int i = 0; i < 100; i++) flow.Sample(PlayerButtons.Start, true, SessionPhase.Ready);
            Assert.AreEqual(SinglePlayerScreen.StageIntro, flow.Screen);
            Press(flow, PlayerButtons.Start);
            Assert.AreEqual(SinglePlayerScreen.Battle, flow.Screen);
        }

        [TestCase(SessionPhase.Won, SinglePlayerScreen.Complete)]
        [TestCase(SessionPhase.Lost, SinglePlayerScreen.GameOver)]
        public void TerminalResultRequiresFreshConfirmAndCanReturnToTitle(SessionPhase outcome, SinglePlayerScreen expected)
        {
            var flow = new SinglePlayerFlowState();
            Press(flow, PlayerButtons.Start); Press(flow, PlayerButtons.Start);
            flow.Sample(PlayerButtons.Fire, true, outcome);
            Assert.AreEqual(SinglePlayerScreen.Results, flow.Screen);
            flow.Sample(PlayerButtons.Fire, true, outcome);
            Assert.AreEqual(SinglePlayerScreen.Results, flow.Screen);
            Press(flow, PlayerButtons.Start, outcome);
            Assert.AreEqual(expected, flow.Screen);
            Press(flow, PlayerButtons.Down, outcome);
            Assert.AreEqual(1, flow.Selection);
            for (int i = 0; i < 10; i++) flow.Sample(PlayerButtons.Down, true, outcome);
            Assert.AreEqual(1, flow.Selection);
            Press(flow, PlayerButtons.Start, outcome);
            Assert.AreEqual(SinglePlayerScreen.Title, flow.Screen);
        }

        [Test]
        public void UnfocusedAndSuppressedHeldConfirmCannotAdvance()
        {
            var flow = new SinglePlayerFlowState();
            flow.Sample(PlayerButtons.Start, false, SessionPhase.Ready);
            flow.Sample(PlayerButtons.Start, true, SessionPhase.Ready);
            Assert.AreEqual(SinglePlayerScreen.Title, flow.Screen);
            flow.Suppress(PlayerButtons.Fire);
            flow.Sample(PlayerButtons.Fire, true, SessionPhase.Ready);
            Assert.AreEqual(SinglePlayerScreen.Title, flow.Screen);
            Press(flow, PlayerButtons.Fire);
            Assert.AreEqual(SinglePlayerScreen.StageIntro, flow.Screen);
        }

        [Test]
        public void RestartBattleReturnsToIntroOnlyOnce()
        {
            var flow = new SinglePlayerFlowState();
            Press(flow, PlayerButtons.Start); Press(flow, PlayerButtons.Start);
            Assert.AreEqual(SinglePlayerAction.Restart, flow.Sample(PlayerButtons.Restart, true, SessionPhase.Playing));
            Assert.AreEqual(SinglePlayerScreen.StageIntro, flow.Screen);
            Assert.AreEqual(SinglePlayerAction.None, flow.Sample(PlayerButtons.Restart, true, SessionPhase.Ready));
        }

        [Test]
        public void AlternateConfirmKeysCannotBypassHeldConfirmation()
        {
            var flow = new SinglePlayerFlowState();
            flow.Sample(PlayerButtons.Start, true, SessionPhase.Ready);
            flow.Sample(PlayerButtons.Start | PlayerButtons.Fire, true, SessionPhase.Ready);
            flow.Sample(PlayerButtons.Fire, true, SessionPhase.Ready);
            Assert.AreEqual(SinglePlayerScreen.StageIntro, flow.Screen);
        }
    }
}
