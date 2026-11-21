using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

/// <summary>The added hand's gap on dials modeled at any scale (<see cref="GaugeHand"/>).</summary>
public class GaugeHandTests
{
    [Fact]
    public void OnADialAtTheCabsScaleTheGapIsAsSet()
    {
        Assert.Equal(GaugeHand.Gap, GaugeHand.LocalGap(1), 12);
    }

    [Fact]
    public void OnADialScaledAHundredTimesTheGapStaysOneAndAHalfMillimetres()
    {
        // The LMS 4F's main reservoir hand moves in a frame scaled 100 times: one of its units is 100 m in the cab.
        var local = GaugeHand.LocalGap(100);
        Assert.Equal(0.000015, local, 12);
        Assert.Equal(0.0015, local * 100, 12);
    }

    [Fact]
    public void OnADialScaledDownTheGapGrowsInItsUnits()
    {
        Assert.Equal(0.15, GaugeHand.LocalGap(0.01), 12);
    }

    [Fact]
    public void AFrameOfNoSizeGetsNoGap()
    {
        Assert.Equal(0, GaugeHand.LocalGap(0));
        Assert.Equal(0, GaugeHand.LocalGap(-1));
        Assert.Equal(0, GaugeHand.LocalGap(double.NaN));
        Assert.Equal(0, GaugeHand.LocalGap(double.PositiveInfinity));
    }
}
