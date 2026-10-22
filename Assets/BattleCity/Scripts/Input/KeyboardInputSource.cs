using UnityEngine.InputSystem;

namespace BattleCity
{
    public static class KeyboardInputSource
    {
        public static PlayerButtons Read()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return PlayerButtons.None;
            PlayerButtons buttons = PlayerButtons.None;
            if (keyboard.wKey.isPressed) buttons |= PlayerButtons.W;
            if (keyboard.upArrowKey.isPressed) buttons |= PlayerButtons.Up;
            if (keyboard.aKey.isPressed) buttons |= PlayerButtons.A;
            if (keyboard.leftArrowKey.isPressed) buttons |= PlayerButtons.Left;
            if (keyboard.sKey.isPressed) buttons |= PlayerButtons.S;
            if (keyboard.downArrowKey.isPressed) buttons |= PlayerButtons.Down;
            if (keyboard.dKey.isPressed) buttons |= PlayerButtons.D;
            if (keyboard.rightArrowKey.isPressed) buttons |= PlayerButtons.Right;
            if (keyboard.spaceKey.isPressed) buttons |= PlayerButtons.Fire;
            if (keyboard.escapeKey.isPressed) buttons |= PlayerButtons.Pause;
            return buttons;
        }
    }
}
