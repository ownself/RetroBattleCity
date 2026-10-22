using System;

namespace BattleCity
{
    /// <summary>One owner advances this clock from render time. No Unity timeScale or FixedUpdate dependency.</summary>
    public sealed class LogicClock
    {
        public const double StepSeconds = 1.0 / PixelGrid.LogicHz;
        public const int MaxTicksPerFrame = 8;
        private double accumulator;
        public long Tick { get; private set; }
        public long DroppedTicks { get; private set; }
        public bool Paused { get; private set; }

        public void SetPaused(bool paused)
        {
            Paused = paused;
            accumulator = 0;
        }

        public int Advance(double elapsedSeconds, Action step)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (step == null) throw new ArgumentNullException(nameof(step));
            if (Paused) return 0;
            // Bound even pathological deltas, without converting an unbounded double to an integer.
            double elapsedTicks = Math.Floor(elapsedSeconds / StepSeconds);
            if (elapsedTicks > MaxTicksPerFrame)
            {
                long discarded = (long)Math.Min(elapsedTicks - MaxTicksPerFrame, long.MaxValue - DroppedTicks);
                DroppedTicks += discarded;
                elapsedSeconds = elapsedSeconds % StepSeconds + MaxTicksPerFrame * StepSeconds;
            }
            accumulator += elapsedSeconds;
            int advanced = 0;
            while (!Paused && advanced < MaxTicksPerFrame && accumulator + 1e-10 >= StepSeconds)
            {
                accumulator -= StepSeconds;
                if (accumulator < 0) accumulator = 0;
                Tick++; advanced++; step();
            }
            if (accumulator + 1e-10 >= StepSeconds)
            {
                long dropped = (long)Math.Floor((accumulator + 1e-10) / StepSeconds);
                DroppedTicks += dropped;
                accumulator = Math.Max(0, accumulator - dropped * StepSeconds);
            }
            return advanced;
        }
    }
}
