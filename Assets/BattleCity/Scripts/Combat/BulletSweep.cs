using System;
using UnityEngine;

namespace BattleCity
{
    /// <summary>Segment versus expanded AABB. Uses doubles for time, including very fast projectiles.</summary>
    internal static class BulletSweep
    {
        public static bool TryHit(Vector2 origin, Vector2 delta, Rect bounds, float halfSize, out double time)
        {
            double enter = double.NegativeInfinity, exit = double.PositiveInfinity;
            time = 0;
            if (!Clip(origin.x, delta.x, (double)bounds.xMin - halfSize, (double)bounds.xMax + halfSize, ref enter, ref exit) ||
                !Clip(origin.y, delta.y, (double)bounds.yMin - halfSize, (double)bounds.yMax + halfSize, ref enter, ref exit))
                return false;
            // Starting contact while moving away, or a zero-area corner graze, is not penetration.
            // First contact while entering (including exactly t=1) is a hit.
            time = Math.Max(0, enter);
            return time <= 1 && exit > time;
        }

        private static bool Clip(double start, double delta, double min, double max, ref double enter, ref double exit)
        {
            if (delta == 0)
                return start > min + MapState.Epsilon && start < max - MapState.Epsilon;
            double a = (min - start) / delta, b = (max - start) / delta;
            if (a > b) { double swap = a; a = b; b = swap; }
            enter = Math.Max(enter, a); exit = Math.Min(exit, b);
            return enter <= exit;
        }
    }
}
