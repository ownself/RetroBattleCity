using System;
using UnityEngine;

namespace BattleCity
{
    public enum CardinalDirection { None = -1, Up, Left, Down, Right }

    public static class CardinalDirections
    {
        public static Vector2 Vector(this CardinalDirection direction)
        {
            switch (direction)
            {
                case CardinalDirection.None: return Vector2.zero;
                case CardinalDirection.Up: return Vector2.up;
                case CardinalDirection.Left: return Vector2.left;
                case CardinalDirection.Down: return Vector2.down;
                case CardinalDirection.Right: return Vector2.right;
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        public static bool IsVertical(this CardinalDirection direction) =>
            direction == CardinalDirection.Up || direction == CardinalDirection.Down;

        public static string SpriteKey(this CardinalDirection direction)
        {
            switch (direction)
            {
                case CardinalDirection.Up: return "up";
                case CardinalDirection.Left: return "left";
                case CardinalDirection.Down: return "down";
                case CardinalDirection.Right: return "right";
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }
    }
}
