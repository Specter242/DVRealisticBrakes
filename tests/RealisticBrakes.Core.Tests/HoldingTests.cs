using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

/// <summary>Cuts of loaded 80 t cars, standing; the game's full braking is about 0.652 m/s².</summary>
public class HoldingTests
{
    private const double Mass = 80000;
    private const double FullForce = 0.652 * Mass;
    private const double HandBrake = FullForce * Holding.HandBrakeShare;
    private const double Rolling = 0.002;

    private static HeldCar[] Cut(int cars, int handBrakes, double gradePercent) =>
        Enumerable.Range(0, cars)
            .Select(i => new HeldCar(Mass, i < handBrakes ? HandBrake : 0, gradePercent / 100, Rolling))
            .ToArray();

    [Fact]
    public void OneHandBrakeHoldsItsOwnCarOnTwoPercent() => Assert.True(Holding.Holds(Cut(1, 1, 2)));

    [Fact]
    public void OneHandBrakeDoesNotHoldTenCarsOnOnePercent() => Assert.False(Holding.Holds(Cut(10, 1, 1)));

    [Fact]
    public void AboutOneInThreeHoldsACutOnOnePercent()
    {
        Assert.False(Holding.Holds(Cut(10, 2, 1)));
        Assert.True(Holding.Holds(Cut(10, 3, 1)));
    }

    [Fact]
    public void RollingResistanceAloneHoldsALevelCut() => Assert.True(Holding.Holds(Cut(20, 0, 0.1)));

    [Fact]
    public void AFullAirApplicationHoldsALongTrainOnTheSteepestGrades() =>
        Assert.True(Holding.Holds(Enumerable.Range(0, 40).Select(_ => new HeldCar(Mass, FullForce, 0.03, Rolling)).ToArray()));

    [Fact]
    public void AWheelHoldsNoMoreThanItsGrip()
    {
        // Brakes far past the rails' grip on a 30 % grade still slide.
        var cut = new[] { new HeldCar(Mass, 10 * FullForce, 0.3, Rolling) };
        Assert.False(Holding.Holds(cut));
    }

    [Fact]
    public void ACutBendingOverACrestPullsBothWays() =>
        // Half on 1 % up, half on 1 % down: nothing pulls it either way.
        Assert.True(Holding.Holds(Cut(5, 0, 1).Concat(Cut(5, 0, -1)).ToArray()));

    [Fact]
    public void TheWardale5ATsKssPluginKeepsItsEngineAndTendersForceWhileItRuns()
    {
        Assert.True(Holding.ForceKeptByOtherMod("PROTO5AT", wardaleBrakesActive: true));
        Assert.True(Holding.ForceKeptByOtherMod("PROTO5AT_tender", wardaleBrakesActive: true));
        Assert.False(Holding.ForceKeptByOtherMod("PROTO5AT", wardaleBrakesActive: false));
        Assert.False(Holding.ForceKeptByOtherMod("LocoS282A", wardaleBrakesActive: true));
        Assert.False(Holding.ForceKeptByOtherMod(null, wardaleBrakesActive: true));
    }
}

public class WeighedCutTests
{
    [Fact]
    public void ACutIsWeighedOnceWhileNothingChanges()
    {
        var cut = new Holding.Weighed();
        Assert.True(cut.Due(7, 10.0));
        cut.Record(7, 10.0, holds: true);
        Assert.True(cut.Holds);
        Assert.False(cut.Due(7, 10.0 + 1 / 60.0));
        Assert.False(cut.Due(7, 10.4));
    }

    [Fact]
    public void AChangedInputWeighsItAgainAtOnce()
    {
        var cut = new Holding.Weighed();
        cut.Record(7, 10.0, holds: true);
        Assert.True(cut.Due(8, 10.0 + 1 / 60.0));
        cut.Record(8, 10.02, holds: false);
        Assert.False(cut.Holds);
        Assert.False(cut.Due(8, 10.1));
    }

    [Fact]
    public void ItIsWeighedAgainAfterHalfASecondWhateverItsInputs()
    {
        var cut = new Holding.Weighed();
        cut.Record(7, 10.0, holds: true);
        Assert.False(cut.Due(7, 10.49));
        Assert.True(cut.Due(7, 10.5));
        cut.Record(7, 10.5, holds: true);
        Assert.False(cut.Due(7, 10.9));
    }

    [Fact]
    public void AnOffsetBringsOnlyTheFirstRefreshForward()
    {
        var cut = new Holding.Weighed(0.3);
        cut.Record(7, 10.0, holds: true);
        Assert.False(cut.Due(7, 10.19));
        Assert.True(cut.Due(7, 10.2));
        cut.Record(7, 10.2, holds: true);
        Assert.False(cut.Due(7, 10.69));
        Assert.True(cut.Due(7, 10.7));
    }

    [Fact]
    public void AClockSetBackWeighsItAgain()
    {
        // A new session's clock starts over.
        var cut = new Holding.Weighed();
        cut.Record(7, 500.0, holds: true);
        Assert.True(cut.Due(7, 3.0));
    }
}
