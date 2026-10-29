using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

 public class BrakeRiggingTests
{
    private const double Empty = 24000;
    private const double Loaded = 86000;
    private static readonly BrakeRigging Hopper = new(Empty, Loaded);

    /// <summary>The car's deceleration at full service, as a share of the game's (which is the same at any load).</summary>
    private static double Deceleration(double mass, LoadSetting setting)
    {
        var spec = new UicDistributorSpec { EmptyRatio = Hopper.EmptyRatio(3.8 * Air.Bar) };
        var cylinder = spec.MaximumCylinder * (setting == LoadSetting.Empty ? spec.EmptyRatio : 1);
        return BrakeRigging.StockBrakingFactor(cylinder) * Hopper.ForceShare(mass);
    }

    [Fact]
    public void AFullCarOnLoadedStopsAsTheGameDid() => Assert.Equal(1, Deceleration(Loaded, LoadSetting.Loaded), 6);

    [Fact]
    public void AnEmptyCarOnEmptyStopsAsTheGameDid() => Assert.Equal(1, Deceleration(Empty, LoadSetting.Empty), 6);

    [Fact]
    public void AnEmptyCarLeftOnLoadedBrakesAboutThreeAndAHalfTimesAsHard() =>
        Assert.Equal(Loaded / Empty, Deceleration(Empty, LoadSetting.Loaded), 6);

    [Fact]
    public void AFullCarLeftOnEmptyBrakesWeakly() =>
        Assert.InRange(Deceleration(Loaded, LoadSetting.Empty), 0.25, 0.3);

    [Fact]
    public void APeriodCarBrakesWithItsEmptyForceAtFullPressure()
    {
        // No empty/load equipment: full service at full pressure, and rigging sized to the light weight (ICS pp. 2, 245).
        Assert.Equal(1, Hopper.ForceMultiplier(1, 0, Empty, Hopper.PeriodLeverage), 6);
        Assert.Equal(Empty / Loaded, Hopper.ForceMultiplier(1, 0, Loaded, Hopper.PeriodLeverage), 6);
        // The hand brake stays the game's.
        Assert.Equal(Hopper.ForceMultiplier(0, 1, Loaded), Hopper.ForceMultiplier(0, 1, Loaded, Hopper.PeriodLeverage), 6);
    }

    [Fact]
    public void TheLeverBelongsAtLoadedFromHalfwayUp()
    {
        Assert.Equal(LoadSetting.Empty, Hopper.SettingFor(Empty));
        Assert.Equal(LoadSetting.Empty, Hopper.SettingFor(54999));
        Assert.Equal(LoadSetting.Loaded, Hopper.SettingFor(55000));
        Assert.Equal(LoadSetting.Loaded, Hopper.SettingFor(Loaded));
    }

    [Fact]
    public void TheEmptySettingSitsInAUicEmptyCarsRange()
    {
        // About 1.34 bar for the hopper: real two-stage empty settings run about 1.2 to 1.8 bar.
        var cylinder = 3.8 * Hopper.EmptyRatio(3.8 * Air.Bar);
        Assert.InRange(cylinder, 1.2, 1.8);
    }

    [Fact]
    public void AHandBrakeFollowsTheGamesForceAtHalfAnApplication()
    {
        // Full hand brake, no air, on an empty hopper: half the game's own force, not 3.6 times it.
        Assert.Equal(Holding.HandBrakeShare, Hopper.ForceMultiplier(0, 1, Empty) * 1, 6);
        // Full air on loaded, empty car: the rigging's force, past the hand brake's.
        Assert.Equal(Loaded / Empty, Hopper.ForceMultiplier(1, 1, Empty), 6);
        // Half a hand brake and a light application: whichever gives more.
        var air = 0.1;
        var multiplier = Hopper.ForceMultiplier(air, 0.5, Empty);
        Assert.Equal(Math.Max(air * Loaded / Empty, 0.5 * Holding.HandBrakeShare), multiplier * Math.Max(air, 0.5), 6);
    }
}
