using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

/// <summary>The DE6's handle: 12 stock notches, 14 widened.</summary>
public class SelfLappingHandleTests
{
    private static readonly WidenedHandle Handle = WidenedHandle.SelfLapping(12);

    [Fact]
    public void ItGainsANotchAtEachEnd() => Assert.Equal(14, Handle.Notches);

    [Fact]
    public void TheNewEndsAreTheFillingStrokeAndEmergency()
    {
        Assert.Equal(HandleZone.Fill, Handle.Zone(0, 1.0));
        Assert.Equal(HandleZone.Travel, Handle.Zone(1, 0));
        Assert.Equal(HandleZone.Travel, Handle.Zone(12, 0));
        Assert.Equal(HandleZone.Emergency, Handle.Zone(13, 0));
    }

    [Fact]
    public void AMomentPastReleaseIsStillRelease()
    {
        // A scroll's overshoot rests there 0.3 s before the game's spring takes the handle back.
        Assert.Equal(HandleZone.Travel, Handle.Zone(0, 0));
        Assert.Equal(HandleZone.Travel, Handle.Zone(0, 0.3));
        Assert.Equal(HandleZone.Fill, Handle.Zone(0, WidenedHandle.FillHoldTime));
        Assert.True(Handle.SpringsBack);
    }

    [Fact]
    public void TheStockNotchesKeepTheirStockValues()
    {
        for (var stock = 0; stock < 12; stock++)
            Assert.Equal(stock / 11f, Handle.StockValue(stock + 1), 5);
    }

    [Fact]
    public void TheNewNotchesReadAsReleaseAndFullService()
    {
        Assert.Equal(0f, Handle.StockValue(0));
        Assert.Equal(1f, Handle.StockValue(13));
    }

    [Fact]
    public void AStockValueSetFromOutsideLandsOnTheStockNotchesOnly()
    {
        // Saves, the remote, multiple-unit cables, and AI drivers set the stock value: never a filling stroke or an emergency.
        Assert.Equal(1, Handle.NotchFor(0f));
        Assert.Equal(12, Handle.NotchFor(1f));
        Assert.Equal(1 / 13f, Handle.HandleValue(0f), 5);
        Assert.Equal(12 / 13f, Handle.HandleValue(1f), 5);
        Assert.Equal(6.5f / 13f, Handle.HandleValue(0.5f), 5);
    }

    [Fact]
    public void ValuesRoundTripThroughTheHandle()
    {
        for (var stock = 0; stock < 12; stock++)
        {
            var value = stock / 11f;
            Assert.Equal(value, Handle.StockValue(Handle.NotchFor(value)), 5);
        }
    }

    [Fact]
    public void AHandleWithoutTravelIsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => WidenedHandle.SelfLapping(1));
}

/// <summary>The DE6's handle on American equipment: the 26-L has no filling stroke, so emergency only.</summary>
public class AmericanSelfLappingHandleTests
{
    private static readonly WidenedHandle Handle = WidenedHandle.SelfLapping(12, fillingStroke: false);

    [Fact]
    public void ItGainsEmergencyOnly()
    {
        Assert.Equal(13, Handle.Notches);
        Assert.False(Handle.ReleaseNotch);
        Assert.False(Handle.SpringsBack);
    }

    [Fact]
    public void ReleaseIsTheFirstNotchAndNeverFills()
    {
        Assert.Equal(HandleZone.Travel, Handle.Zone(0, 10));
        Assert.Equal(0f, Handle.StockValue(0));
        Assert.Equal(HandleZone.Emergency, Handle.Zone(12, 0));
    }

    [Fact]
    public void TheStockNotchesKeepTheirStockValues()
    {
        for (var stock = 0; stock < 12; stock++)
        {
            Assert.Equal(stock / 11f, Handle.StockValue(stock), 5);
            Assert.Equal(stock, Handle.NotchFor(stock / 11f));
        }
        Assert.Equal(1f, Handle.StockValue(12));
        Assert.Equal(0f, Handle.HandleValue(0f));
        Assert.Equal(11 / 12f, Handle.HandleValue(1f), 5);
    }
}

/// <summary>The S282's handle: running, lap, service, and emergency, with release added before running.</summary>
public class ManualLapHandleTests
{
    private static readonly WidenedHandle Handle = WidenedHandle.ManualLap(4);

    [Fact]
    public void ItGainsReleaseOnly()
    {
        Assert.Equal(5, Handle.Notches);
        Assert.False(Handle.EmergencyNotch);
    }

    [Fact]
    public void ReleaseIsAtOnceAndStays()
    {
        // As on the H-6: no hold, no spring; the driver brings it back to running.
        Assert.Equal(HandleZone.Fill, Handle.Zone(0, 0));
        Assert.False(Handle.SpringsBack);
    }

    [Fact]
    public void TheStockPositionsKeepTheirStockValues()
    {
        // Running, lap, service, emergency: the game's zones read from these.
        Assert.Equal(0f, Handle.StockValue(1));
        Assert.Equal(1 / 3f, Handle.StockValue(2), 5);
        Assert.Equal(2 / 3f, Handle.StockValue(3), 5);
        Assert.Equal(1f, Handle.StockValue(4));
        Assert.Equal(HandleZone.Travel, Handle.Zone(4, 0));
    }

    [Fact]
    public void ReleaseReadsAsRunning() => Assert.Equal(0f, Handle.StockValue(0));

    [Fact]
    public void AStockValueSetFromOutsideNeverLandsInRelease()
    {
        Assert.Equal(1, Handle.NotchFor(0f));
        Assert.Equal(4, Handle.NotchFor(1f));
        Assert.Equal(0.25f, Handle.HandleValue(0f), 5);
        Assert.Equal(1f, Handle.HandleValue(1f), 5);
    }
}
