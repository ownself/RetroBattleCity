using NUnit.Framework;

namespace BattleCity.Tests
{
    public sealed class StageSelectionTests
    {
        private static SinglePlayerFlowState Intro()
        {
            var state = new SinglePlayerFlowState();
            state.Sample(PlayerButtons.Start, true, SessionPhase.Ready);
            state.Sample(PlayerButtons.None, true, SessionPhase.Ready);
            return state;
        }
        [TestCase(PlayerButtons.Up, SinglePlayerAction.PreviousSelection)]
        [TestCase(PlayerButtons.W, SinglePlayerAction.PreviousSelection)]
        [TestCase(PlayerButtons.Down, SinglePlayerAction.NextSelection)]
        [TestCase(PlayerButtons.S, SinglePlayerAction.NextSelection)]
        public void DirectionEmitsOnceUntilReleased(PlayerButtons key, SinglePlayerAction expected)
        {
            var state = Intro();
            Assert.AreEqual(expected, state.Sample(key, true, SessionPhase.Ready));
            for (int i = 0; i < 100; i++) Assert.AreEqual(SinglePlayerAction.None, state.Sample(key, true, SessionPhase.Ready));
            Assert.AreEqual(SinglePlayerScreen.StageIntro, state.Screen);
            state.Sample(PlayerButtons.None, true, SessionPhase.Ready);
            Assert.AreEqual(expected, state.Sample(key, true, SessionPhase.Ready));
        }
        [Test] public void AliasesAndOpposedKeysDoNotDuplicateSelection()
        {
            var state = Intro();
            state.Sample(PlayerButtons.Up, true, SessionPhase.Ready);
            Assert.AreEqual(SinglePlayerAction.None, state.Sample(PlayerButtons.Up | PlayerButtons.W, true, SessionPhase.Ready));
            Assert.AreEqual(SinglePlayerAction.None, state.Sample(PlayerButtons.W, true, SessionPhase.Ready));
            Assert.AreEqual(SinglePlayerAction.None, state.Sample(PlayerButtons.Up | PlayerButtons.Down, true, SessionPhase.Ready));
        }
        [Test] public void NavigationConsumesSimultaneousConfirmAndFocusBlocksHeldKeys()
        {
            var state = Intro();
            Assert.AreEqual(SinglePlayerAction.NextSelection, state.Sample(PlayerButtons.Down | PlayerButtons.Start, true, SessionPhase.Ready));
            Assert.AreEqual(SinglePlayerAction.None, state.Sample(PlayerButtons.Start, true, SessionPhase.Ready));
            state.Sample(PlayerButtons.Up, false, SessionPhase.Ready);
            Assert.AreEqual(SinglePlayerAction.None, state.Sample(PlayerButtons.Up, true, SessionPhase.Ready));
            state.Suppress(PlayerButtons.Down);
            Assert.AreEqual(SinglePlayerAction.None, state.Sample(PlayerButtons.Down, true, SessionPhase.Ready));
        }
        [Test] public void BattleDirectionsNeverSelectStage()
        {
            var state = Intro(); state.Sample(PlayerButtons.Start, true, SessionPhase.Ready);
            Assert.AreEqual(SinglePlayerAction.None, state.Sample(PlayerButtons.Up, true, SessionPhase.Playing));
            Assert.AreEqual(SinglePlayerScreen.Battle, state.Screen);
        }
    }
}
