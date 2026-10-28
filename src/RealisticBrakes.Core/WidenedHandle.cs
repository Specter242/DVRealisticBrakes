using System;

namespace RealisticBrakes.Core
{
    /// <summary>Where a widened train brake handle stands.</summary>
    public enum HandleZone
    {
        /// <summary>Past running: a filling stroke on a diesel, held there; release on a steam engine's valve.</summary>
        Fill,

        /// <summary>The stock travel.</summary>
        Travel,

        /// <summary>Past full service on a diesel: emergency, where the handle stays.</summary>
        Emergency,
    }

 public sealed class WidenedHandle
    {
 public const double FillHoldTime = 0.4;

        private WidenedHandle(int stockNotches, bool emergencyNotch, bool springsBack, double fillHold)
        {
            if (stockNotches < 2)
                throw new ArgumentOutOfRangeException(nameof(stockNotches), stockNotches, "a handle has at least two notches");
            StockNotches = stockNotches;
            EmergencyNotch = emergencyNotch;
            SpringsBack = springsBack;
            FillHold = fillHold;
        }

        /// <summary>A diesel's self-lapping handle of <paramref name="stockNotches"/> notches: a sprung filling stroke and emergency added.</summary>
        public static WidenedHandle SelfLapping(int stockNotches) => new WidenedHandle(stockNotches, true, true, FillHoldTime);

        /// <summary>A steam engine's manual-lap handle of <paramref name="stockNotches"/> notches: release added, where it stays.</summary>
        public static WidenedHandle ManualLap(int stockNotches) => new WidenedHandle(stockNotches, false, false, 0);

        /// <summary>The stock handle's notches.</summary>
        public int StockNotches { get; }

        /// <summary>The widened handle's notches.</summary>
        public int Notches => StockNotches + 1 + (EmergencyNotch ? 1 : 0);

        /// <summary>Whether a notch is added past full service, for emergency.</summary>
        public bool EmergencyNotch { get; }

        /// <summary>Whether the first notch springs back to the stock travel when let go.</summary>
        public bool SpringsBack { get; }

        /// <summary>How long, in s, the first notch must be held before it counts.</summary>
        public double FillHold { get; }

        /// <summary>Where <paramref name="notch"/> stands, held past the stock travel for <paramref name="heldPastRelease"/> s.</summary>
        public HandleZone Zone(int notch, double heldPastRelease) =>
            notch <= 0 ? (heldPastRelease >= FillHold ? HandleZone.Fill : HandleZone.Travel)
            : EmergencyNotch && notch >= Notches - 1 ? HandleZone.Emergency
            : HandleZone.Travel;

        /// <summary>The stock value, 0 to 1, of <paramref name="notch"/>: the new notches read as their stock neighbors.</summary>
        public float StockValue(int notch) => Clamp01((notch - 1) / (float)(StockNotches - 1));

        /// <summary>
        /// Where on the widened handle's travel, 0 to 1, a stock
        /// <paramref name="value"/> puts it: among the stock notches, never in
        /// the new ones.
        /// </summary>
        public float HandleValue(float value) => (1 + Clamp01(value) * (StockNotches - 1)) / (Notches - 1);

        /// <summary>The nearest notch to a stock <paramref name="value"/>.</summary>
        public int NotchFor(float value) => 1 + (int)Math.Round(Clamp01(value) * (StockNotches - 1));

        private static float Clamp01(float value) => value < 0 ? 0 : value > 1 ? 1 : value;
    }
}
