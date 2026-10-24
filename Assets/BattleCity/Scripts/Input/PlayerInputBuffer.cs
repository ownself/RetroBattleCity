using System;

namespace BattleCity
{
    [Flags]
    public enum PlayerButtons
    {
        None = 0, W = 1, Up = 2, A = 4, Left = 8, S = 16, Down = 32,
        D = 64, Right = 128, Fire = 256, Pause = 512, Start = 1024, Restart = 2048
    }

    public readonly struct TankCommand
    {
        public readonly CardinalDirection Direction;
        public readonly bool FireHeld;
        public readonly bool FirePressed;
        public TankCommand(CardinalDirection direction, bool fireHeld = false, bool firePressed = false)
        { Direction = direction; FireHeld = fireHeld; FirePressed = firePressed; }
    }

    /// <summary>Render samples become level state plus a one-tick fire edge. Tracks physical aliases separately.</summary>
    public sealed class PlayerInputBuffer
    {
        private readonly long[] pressedOrder = new long[8];
        private PlayerButtons previous, held, blocked;
        private long sample;
        private bool pendingFire;

        public bool Sample(PlayerButtons buttons, bool acceptCombat)
        {
            bool pausePressed = (buttons & PlayerButtons.Pause) != 0 && (previous & PlayerButtons.Pause) == 0;
            blocked &= buttons; // Each key rearms only after a release.
            var pressed = buttons & ~previous & ~blocked;
            previous = buttons;
            if (!acceptCombat)
            {
                SuppressHeld();
                return pausePressed;
            }
            held = buttons & ~blocked;
            sample++;
            for (int i = 0; i < 8; i++)
                if (((int)pressed & (1 << i)) != 0) pressedOrder[i] = sample * 8 + 8 - i;
            if ((pressed & PlayerButtons.Fire) != 0) pendingFire = true;
            return pausePressed;
        }

        public TankCommand ConsumeTick()
        {
            int best = -1; long order = 0;
            for (int i = 0; i < 8; i++)
                if (((int)held & (1 << i)) != 0 && pressedOrder[i] > order)
                { best = i; order = pressedOrder[i]; }
            var command = new TankCommand(best < 0 ? CardinalDirection.None : (CardinalDirection)(best / 2),
                (held & PlayerButtons.Fire) != 0, pendingFire);
            pendingFire = false;
            return command;
        }

        public void SuppressHeld()
        {
            blocked |= previous;
            held = PlayerButtons.None;
            pendingFire = false;
            Array.Clear(pressedOrder, 0, pressedOrder.Length);
        }
    }
}
