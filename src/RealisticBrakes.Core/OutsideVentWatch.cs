using System;

namespace RealisticBrakes.Core
{
 public sealed class OutsideVentWatch
    {
        /// <summary>A fall under what was written of more than this, bar, is another mod venting: far over a float's rounding.</summary>
        public const double Drop = 0.001;

        /// <summary>
        /// How long, s, the vent stays open after the last fall seen. The game ticks brake sets each frame, and another mod
        /// may vent only on physics steps, which some frames don't have; it stays open across them.
        /// </summary>
        public const double Hold = 0.25;

        private double written = double.NaN;
        private long writtenAt = long.MinValue;
        private double left;

        /// <summary>Whether the vent stands open.</summary>
        public bool Open => left > 0;

        /// <summary>Whether the last <see cref="Check"/> saw a fall.</summary>
        public bool Fell { get; private set; }

        /// <summary>This mod wrote <paramref name="bar"/> into the field at tick <paramref name="tick"/>.</summary>
        public void Wrote(double bar, long tick)
        {
            written = bar;
            writtenAt = tick;
        }

        /// <summary>A stock setter put <paramref name="bar"/> in the field, which the model takes as it is: not a vent.</summary>
        public void Took(double bar) => written = bar;

        /// <summary>
        /// Before tick <paramref name="tick"/>, <paramref name="dt"/> s on: the field stands at <paramref name="bar"/>.
        /// Only a value written the tick before counts, so a frame the stock brake ran the set (which writes the field
        /// itself) is never taken for a vent. Not <paramref name="watching"/> (a multiplayer guest, whose field the host's
        /// packets write), it never opens. Returns whether the vent stands open for this tick.
        /// </summary>
        public bool Check(double bar, double dt, long tick, bool watching = true)
        {
            Fell = watching && writtenAt == tick - 1 && bar < written - Drop;
            left = Fell ? Hold : Math.Max(0, left - Math.Max(0, dt));
            if (!watching)
                left = 0;
            return Open;
        }
    }
}
