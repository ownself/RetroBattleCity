namespace BattleCity
{
    public enum SinglePlayerScreen { Title, StageIntro, Battle, Results, Complete, GameOver }
    public enum SinglePlayerAction { None, Restart, StartBattle }

    /// <summary>One input owner; screen changes consume their edge and never tick combat in the same frame.</summary>
    public sealed class SinglePlayerFlowState
    {
        public SinglePlayerScreen Screen { get; private set; } = SinglePlayerScreen.Title;
        public int Selection { get; private set; }
        private PlayerButtons previous;
        private bool won;
        private const PlayerButtons Confirm = PlayerButtons.Start | PlayerButtons.Fire;
        private const PlayerButtons Up = PlayerButtons.W | PlayerButtons.Up;
        private const PlayerButtons Down = PlayerButtons.S | PlayerButtons.Down;

        public SinglePlayerAction Sample(PlayerButtons buttons, bool focused, SessionPhase phase)
        {
            var old = previous;
            previous = buttons;
            if (!focused) return SinglePlayerAction.None;
            if (Screen == SinglePlayerScreen.Battle)
            {
                if (phase == SessionPhase.Won || phase == SessionPhase.Lost)
                {
                    won = phase == SessionPhase.Won;
                    Screen = SinglePlayerScreen.Results;
                    return SinglePlayerAction.None; // Terminal-frame input cannot skip the result.
                }
                if ((buttons & PlayerButtons.Restart) != 0 && (old & PlayerButtons.Restart) == 0)
                {
                    Screen = SinglePlayerScreen.StageIntro;
                    return SinglePlayerAction.Restart;
                }
                return SinglePlayerAction.None;
            }
            if (Screen == SinglePlayerScreen.Complete || Screen == SinglePlayerScreen.GameOver)
            {
                bool up = (buttons & Up) != 0 && (old & Up) == 0;
                bool down = (buttons & Down) != 0 && (old & Down) == 0;
                if (up != down) Selection = 1 - Selection;
            }
            if ((buttons & Confirm) == 0 || (old & Confirm) != 0) return SinglePlayerAction.None;
            switch (Screen)
            {
                case SinglePlayerScreen.Title:
                    Screen = SinglePlayerScreen.StageIntro;
                    return SinglePlayerAction.Restart;
                case SinglePlayerScreen.StageIntro:
                    Screen = SinglePlayerScreen.Battle;
                    return SinglePlayerAction.StartBattle;
                case SinglePlayerScreen.Results:
                    Screen = won ? SinglePlayerScreen.Complete : SinglePlayerScreen.GameOver;
                    Selection = 0;
                    break;
                case SinglePlayerScreen.Complete:
                case SinglePlayerScreen.GameOver:
                    Screen = Selection == 0 ? SinglePlayerScreen.StageIntro : SinglePlayerScreen.Title;
                    Selection = 0;
                    return SinglePlayerAction.Restart;
            }
            return SinglePlayerAction.None;
        }

        // Focus changes block all currently held keys until a physical release.
        public void Suppress(PlayerButtons buttons) => previous = buttons;
    }
}
