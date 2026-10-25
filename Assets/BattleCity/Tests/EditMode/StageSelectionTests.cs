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
        private static SinglePlayerFlowState NextStageIntro()
        {
            var state = Intro();
            state.Sample(PlayerButtons.Start, true, SessionPhase.Ready);
            state.Sample(PlayerButtons.None, true, SessionPhase.Won, true);
            Assert.AreEqual(SinglePlayerScreen.Results, state.Screen);
            Assert.AreEqual(SinglePlayerAction.NextStage, state.Sample(PlayerButtons.Start, true, SessionPhase.Won, true));
            state.Sample(PlayerButtons.None, true, SessionPhase.Ready);
            return state;
        }

        [TestCase(PlayerButtons.Up)]
        [TestCase(PlayerButtons.W)]
        [TestCase(PlayerButtons.Down)]
        [TestCase(PlayerButtons.S)]
        public void NormalProgressionLocksSelectionButFreshConfirmStillStartsNextStage(PlayerButtons key)
        {
            var state = NextStageIntro();
            Assert.That(state.CanSelectStage, Is.False);
            Assert.AreEqual(SinglePlayerAction.None, state.Sample(key, true, SessionPhase.Ready));
            Assert.AreEqual(SinglePlayerScreen.StageIntro, state.Screen);
            state.Sample(PlayerButtons.None, true, SessionPhase.Ready);
            Assert.AreEqual(SinglePlayerAction.StartBattle, state.Sample(key | PlayerButtons.Start, true, SessionPhase.Ready),
                "Disabled navigation must not steal the next-stage confirmation.");
            Assert.That(state.CanSelectStage, Is.False);
        }

        [Test] public void ExplicitRestartReopensSelectionAfterNormalProgression()
        {
            var state = NextStageIntro();
            state.Sample(PlayerButtons.Start, true, SessionPhase.Ready);
            Assert.AreEqual(SinglePlayerAction.Restart, state.Sample(PlayerButtons.Restart, true, SessionPhase.Playing));
            Assert.That(state.CanSelectStage, Is.True);
            state.Sample(PlayerButtons.None, true, SessionPhase.Ready);
            Assert.AreEqual(SinglePlayerAction.PreviousSelection, state.Sample(PlayerButtons.Up, true, SessionPhase.Ready));
        }

        [TestCase(SessionPhase.Won)]
        [TestCase(SessionPhase.Lost)]
        public void PlayAgainReopensSelectionAfterCompletionOrGameOver(SessionPhase outcome)
        {
            var state = Intro();
            Assert.That(state.CanSelectStage, Is.True);
            state.Sample(PlayerButtons.Start, true, SessionPhase.Ready);
            Assert.That(state.CanSelectStage, Is.False);
            state.Sample(PlayerButtons.None, true, outcome);
            Assert.AreEqual(SinglePlayerAction.None, state.Sample(PlayerButtons.Up, true, outcome), "Results never select stages.");
            state.Sample(PlayerButtons.Start, true, outcome);
            state.Sample(PlayerButtons.None, true, outcome);
            Assert.AreEqual(SinglePlayerAction.Restart, state.Sample(PlayerButtons.Start, true, outcome));
            Assert.That(state.CanSelectStage, Is.True);
            state.Sample(PlayerButtons.None, true, SessionPhase.Ready);
            Assert.AreEqual(SinglePlayerAction.NextSelection, state.Sample(PlayerButtons.Down, true, SessionPhase.Ready));
        }

        [Test] public void BattleDirectionsNeverSelectStage()
        {
            var state = Intro(); state.Sample(PlayerButtons.Start, true, SessionPhase.Ready);
            Assert.AreEqual(SinglePlayerAction.None, state.Sample(PlayerButtons.Up, true, SessionPhase.Playing));
            Assert.AreEqual(SinglePlayerScreen.Battle, state.Screen);
        }
    }
}
