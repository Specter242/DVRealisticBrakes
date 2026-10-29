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

        /// <summary>Between running and lap on the H-6: holding, the train fed as in running and the engine's brake held.</summary>
        Holding,
    }

 public sealed class WidenedHandle
    {
 public const double FillHoldTime = 0.4;

        private WidenedHandle(int stockNotches, bool releaseNotch, bool emergencyNotch, bool springsBack, double fillHold, bool holdingNotch = false)
        {
            if (stockNotches < 2)
                throw new ArgumentOutOfRangeException(nameof(stockNotches), stockNotches, "a handle has at least two notches");
            StockNotches = stockNotches;
            ReleaseNotch = releaseNotch;
            HoldingNotch = holdingNotch;
            EmergencyNotch = emergencyNotch;
            SpringsBack = springsBack;
            FillHold = fillHold;
        }

        /// <summary>
        /// A diesel's self-lapping handle of <paramref name="stockNotches"/>
        /// notches: emergency added, and a sprung filling stroke if the valve
        /// has one (<paramref name="fillingStroke"/>).
        /// </summary>
        public static WidenedHandle SelfLapping(int stockNotches, bool fillingStroke = true) =>
            new WidenedHandle(stockNotches, fillingStroke, true, fillingStroke, FillHoldTime);

        /// <summary>
        /// A steam engine's manual-lap handle of <paramref name="stockNotches"/>
        /// notches: release added, where it stays, and the H-6's holding after
        /// running if <paramref name="holding"/>.
        /// </summary>
        public static WidenedHandle ManualLap(int stockNotches, bool holding = false) => new WidenedHandle(stockNotches, true, false, false, 0, holding);

        /// <summary>The stock handle's notches.</summary>
        public int StockNotches { get; }

        /// <summary>The widened handle's notches.</summary>
        public int Notches => StockNotches + First + (HoldingNotch ? 1 : 0) + (EmergencyNotch ? 1 : 0);

        /// <summary>Whether a notch is added after the first stock notch, running, for the H-6's holding.</summary>
        public bool HoldingNotch { get; }

        // The holding notch, or −1.
        private int Holding => HoldingNotch ? First + 1 : -1;

        /// <summary>Whether a notch is added before release: a diesel's filling stroke, or a steam engine's release.</summary>
        public bool ReleaseNotch { get; }

        // The first stock notch: after the added release notch, if there is one.
        private int First => ReleaseNotch ? 1 : 0;

        /// <summary>Whether a notch is added past full service, for emergency.</summary>
        public bool EmergencyNotch { get; }

        /// <summary>Whether the first notch springs back to the stock travel when let go.</summary>
        public bool SpringsBack { get; }

        /// <summary>How long, in s, the first notch must be held before it counts.</summary>
        public double FillHold { get; }

        /// <summary>Where <paramref name="notch"/> stands, held past the stock travel for <paramref name="heldPastRelease"/> s.</summary>
        public HandleZone Zone(int notch, double heldPastRelease) =>
            ReleaseNotch && notch <= 0 ? (heldPastRelease >= FillHold ? HandleZone.Fill : HandleZone.Travel)
            : EmergencyNotch && notch >= Notches - 1 ? HandleZone.Emergency
            : notch == Holding ? HandleZone.Holding
            : HandleZone.Travel;

        /// <summary>The widened notch of the stock handle's <paramref name="stockNotch"/>.</summary>
        public int NotchOf(int stockNotch) => First + stockNotch + (HoldingNotch && stockNotch >= 1 ? 1 : 0);

        /// <summary>The stock value, 0 to 1, of <paramref name="notch"/>: the new notches read as their stock neighbors (holding as running).</summary>
        public float StockValue(int notch)
        {
            var stock = notch - First;
            if (HoldingNotch && stock >= 1)
                stock--;
            return Clamp01(stock / (float)(StockNotches - 1));
        }

        /// <summary>
        /// Where on the widened handle's travel, 0 to 1, a stock
        /// <paramref name="value"/> puts it: on the stock notches, never in the
        /// new ones. Between running and the next stock notch it spans the
        /// holding notch, which no stock notch's value lands on.
        /// </summary>
        public float HandleValue(float value)
        {
            var stock = Clamp01(value) * (StockNotches - 1);
            var widened = First + stock + (HoldingNotch ? Math.Min(stock, 1f) : 0f);
            return widened / (Notches - 1);
        }

        /// <summary>The nearest notch to a stock <paramref name="value"/>.</summary>
        public int NotchFor(float value) => NotchOf((int)Math.Round(Clamp01(value) * (StockNotches - 1)));

        private static float Clamp01(float value) => value < 0 ? 0 : value > 1 ? 1 : value;
    }
}
