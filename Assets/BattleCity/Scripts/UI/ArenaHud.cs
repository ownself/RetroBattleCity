using System;
using UnityEngine;

namespace BattleCity
{
    /// <summary>Read-only settled-session projection. The controller pushes after render and replacement; no event ownership.</summary>
    public sealed class ArenaHud : MonoBehaviour
    {
        public Canvas hudCanvas;
        public SpriteCatalog sprites;
        public UnityEngine.UI.Image[] stageDigits;
        public UnityEngine.UI.Image[] enemyDigits;
        public UnityEngine.UI.Image[] lifeDigits;
        public PixelHudText phaseCaption;
        public PixelHudText restartCaption;
        public int DisplayedStage { get; private set; }
        public int DisplayedEnemies { get; private set; }
        /// <summary>Total remaining appearances, including the current life while alive, not spare tanks.</summary>
        public int DisplayedLives { get; private set; }
        public ArenaSession DisplayedSession { get; private set; }
        private readonly Sprite[] digits = new Sprite[10];
        private bool initialized;

        public static int RemainingEnemies(ArenaSession session)
            => (session.EnemyWave?.PendingCount ?? 0) + session.AliveEnemyCount;

        public static string PhaseCaption(SessionPhase phase)
        {
            switch (phase)
            {
                case SessionPhase.Ready: return "READY  ENTER START";
                case SessionPhase.Paused: return "PAUSED  ESC RESUME";
                case SessionPhase.Won: return "WON";
                case SessionPhase.Lost: return "LOST";
                default: return "";
            }
        }

        public void Render(ArenaSession session, int initialStageNumber)
        {
            if (session == null || !isActiveAndEnabled) { Clear(); return; }
            if (!initialized)
            {
                ValidateBindings();
                for (int i = 0; i < digits.Length; i++) digits[i] = sprites.Get("hud.digit." + i);
                initialized = true;
            }
            DisplayedSession = session;
            DisplayedStage = initialStageNumber;
            DisplayedEnemies = RemainingEnemies(session);
            DisplayedLives = session.PlayerLife?.RemainingLives ?? (session.PlayerTank.IsAlive ? 1 : 0);
            SetNumber(stageDigits, DisplayedStage);
            SetNumber(enemyDigits, DisplayedEnemies);
            SetNumber(lifeDigits, DisplayedLives);
            phaseCaption.Text = PhaseCaption(session.Phase);
            restartCaption.Text = "R RESTART";
            hudCanvas.enabled = isActiveAndEnabled;
        }

        private void SetNumber(UnityEngine.UI.Image[] slots, int number)
        {
            string value = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (number < 0 || value.Length > slots.Length) throw new InvalidOperationException("HUD number exceeds authored slots.");
            int offset = slots.Length - value.Length;
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].enabled = i >= offset;
                slots[i].sprite = i >= offset ? digits[value[i - offset] - '0'] : null;
            }
        }

        public void Clear()
        {
            DisplayedSession = null;
            if (hudCanvas != null) hudCanvas.enabled = false;
        }

        private void OnDisable() => Clear();
        private void OnDestroy() => Clear();

        public void ValidateBindings()
        {
            if (hudCanvas == null || sprites == null || phaseCaption == null || restartCaption == null)
                throw new InvalidOperationException("Missing HUD reference.");
            foreach (var row in new[] { stageDigits, enemyDigits, lifeDigits })
            {
                if (row == null || row.Length != 4) throw new InvalidOperationException("HUD requires four digit slots per number.");
                foreach (var image in row) if (image == null) throw new InvalidOperationException("Missing HUD digit image.");
            }
            for (int i = 0; i < 10; i++) sprites.Get("hud.digit." + i);
        }
    }
}
