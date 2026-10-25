using System;
using System.Globalization;
using UnityEngine;

namespace BattleCity
{
    /// <summary>Controller-driven screen projection. No Update, event subscriptions or second keyboard reader.</summary>
    public sealed class SinglePlayerFlow : MonoBehaviour
    {
        public Canvas screenCanvas;
        public PixelHudText heading;
        public PixelHudText[] lines;
        public UnityEngine.UI.Image titleArt;
        public SinglePlayerFlowState State { get; } = new SinglePlayerFlowState();
        public StageResult DisplayedResult { get; private set; }
        private SinglePlayerScreen? rendered;
        private int selection = -1;

        public bool Route(ArenaController controller, PlayerButtons buttons, bool focused)
        {
            var before = State.Screen;
            var action = State.Sample(buttons, focused, controller.Session.Phase);
            if (action == SinglePlayerAction.Restart) controller.RestartSession();
            if (action == SinglePlayerAction.StartBattle) controller.StartBattle();
            bool combat = before == SinglePlayerScreen.Battle && State.Screen == SinglePlayerScreen.Battle;
            if (!combat) controller.Session.SuppressInput(buttons);
            return combat;
        }

        public void Render(ArenaSession session, int stage)
        {
            bool visible = State.Screen != SinglePlayerScreen.Battle;
            screenCanvas.enabled = visible && isActiveAndEnabled;
            if (!visible) { rendered = State.Screen; return; }
            if (rendered == State.Screen && selection == State.Selection && DisplayedResult == session?.Result) return;
            rendered = State.Screen; selection = State.Selection; DisplayedResult = session?.Result;
            titleArt.enabled = State.Screen == SinglePlayerScreen.Title;
            foreach (var line in lines) line.Text = "";
            string number = stage.ToString("00", CultureInfo.InvariantCulture);
            switch (State.Screen)
            {
                case SinglePlayerScreen.Title:
                    heading.Text = "";
                    lines[2].Text = "> 1 PLAYER";
                    lines[4].Text = "ENTER OR SPACE TO START";
                    lines[6].Text = "WASD OR ARROWS  SPACE FIRE";
                    lines[7].Text = "ESC PAUSE  R RESTART STAGE";
                    break;
                case SinglePlayerScreen.StageIntro:
                    heading.Text = "STAGE " + number;
                    lines[2].Text = "PROTECT YOUR BASE";
                    lines[4].Text = "ENTER OR SPACE TO BEGIN";
                    break;
                case SinglePlayerScreen.Results:
                    heading.Text = "STAGE " + number + " RESULTS";
                    var result = session.Result;
                    if (result == null) break;
                    lines[0].Text = result.Outcome == SessionPhase.Won ? "VICTORY" : "DEFEAT";
                    int row = 1;
                    foreach (var enemy in result.Enemies)
                    {
                        if (row > 4) break;
                        // Current four-family roster; the underlying ID remains the score authority.
                        string id = enemy.DefinitionId.ToUpperInvariant().Replace("ENEMY.", "").Replace(".PROTOTYPE", "");
                        lines[row++].Text = id + " " + enemy.Count + "  " + enemy.Points;
                    }
                    lines[5].Text = "PICKUPS " + result.CollectedCount + "  " + result.PickupPoints;
                    lines[6].Text = "TOTAL " + result.Total;
                    lines[7].Text = "ENTER OR SPACE TO CONTINUE";
                    break;
                default:
                    heading.Text = State.Screen == SinglePlayerScreen.Complete ? "STAGE COMPLETE" : "GAME OVER";
                    lines[0].Text = State.Screen == SinglePlayerScreen.Complete ? "END OF CURRENT SINGLE STAGE" : FailureCaption(session.Result);
                    lines[3].Text = (selection == 0 ? "> " : "  ") + "PLAY AGAIN";
                    lines[4].Text = (selection == 1 ? "> " : "  ") + "TITLE";
                    lines[6].Text = "UP DOWN SELECT  ENTER CONFIRM";
                    break;
            }
        }

        public void ValidateBindings()
        {
            if (screenCanvas == null || heading == null || titleArt == null || titleArt.sprite == null ||
                lines == null || lines.Length != 8)
                throw new InvalidOperationException("Missing single-player screen reference.");
            foreach (var line in lines)
                if (line == null) throw new InvalidOperationException("Missing single-player screen row.");
        }

        public void Clear() { if (screenCanvas != null) screenCanvas.enabled = false; rendered = null; }

        private static string FailureCaption(StageResult result)
            => result != null && (result.FailureReason & SessionFailureReason.BaseDestroyed) != 0
                ? "BASE DESTROYED" : "NO LIVES REMAINING";

        private void OnDisable() => Clear();
    }
}
