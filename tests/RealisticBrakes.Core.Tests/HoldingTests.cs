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

    [Fact]
    public void WeighReturnsMassGradeForcesAndBrakeState()
    {
        var rolling = Holding.Weigh(Cut(10, 1, 1.5));
        Assert.Equal(10 * Mass, rolling.Mass, 3);
        Assert.Equal(1.5, rolling.GradePercent, 4);
        Assert.Equal(Holding.HoldingForce(Cut(10, 1, 1.5)), rolling.HoldingForce, 3);
        Assert.Equal(Holding.Pull(Cut(10, 1, 1.5)), rolling.Pull, 3);
        Assert.True(rolling.HasBrakes);
        Assert.False(rolling.Holds);

        var unbraked = Holding.Weigh(Cut(4, 0, 0.5));
        Assert.False(unbraked.HasBrakes);
        Assert.Equal(0.5, unbraked.GradePercent, 4);
        Assert.False(unbraked.Holds);
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

    [Fact]
    public void RecordReportsBrakedRollOnceAndIgnoresGradeJitterAndUnbrakedCuts()
    {
        var cut = new Holding.Weighed();
        var holdsWithMargin = new Holding.CutWeight(800_000, 120_000, 100_000, hasBrakes: true);
        var holdsBarely = new Holding.CutWeight(800_000, 102_000, 100_000, hasBrakes: true);
        var rolling = new Holding.CutWeight(800_000, 99_000, 100_000, hasBrakes: true);

        // Newly weighed and holding: no roll log.
        Assert.False(cut.Record(1, 10.0, holdsWithMargin));
        Assert.True(cut.Holds);

        // Transitions from holding to rolling: logged once.
        Assert.True(cut.Record(2, 10.5, rolling));
        Assert.False(cut.Holds);

        // Still rolling on later weighings: no repeat spam.
        Assert.False(cut.Record(2, 11.0, rolling));
        Assert.False(cut.Record(3, 11.2, rolling));

        // Grade jitter across the threshold (holds by <10% margin, then fails again): physics Holds flips cleanly,
        // but the roll log stays disarmed so jitter never spams.
        Assert.False(cut.Record(3, 11.7, holdsBarely));
        Assert.True(cut.Holds);
        Assert.False(cut.Record(3, 12.2, rolling));
        Assert.False(cut.Holds);

        // Once the cut genuinely holds with >=10% margin, the roll log rearms.
        Assert.False(cut.Record(4, 12.5, holdsWithMargin));
        Assert.True(cut.Holds);
        Assert.True(cut.Record(5, 13.0, rolling));
        Assert.False(cut.Holds);

        // Newly weighed braked cut already not holding: logged once, then quiet.
        var freshRolling = new Holding.Weighed();
        Assert.True(freshRolling.Record(1, 1.0, rolling));
        Assert.False(freshRolling.Record(1, 1.5, rolling));

        // Completely unbraked cuts never log, even on a grade.
        var unbraked = new Holding.Weighed();
        var unbrakedRolling = new Holding.CutWeight(320_000, 6_278, 31_392, hasBrakes: false);
        Assert.False(unbraked.Record(1, 1.0, unbrakedRolling));
        Assert.False(unbraked.Holds);
        Assert.False(unbraked.Record(1, 1.5, unbrakedRolling));
        // Once brakes are applied and still can't hold, it logs once.
        Assert.True(unbraked.Record(2, 2.0, rolling));
        Assert.False(unbraked.Record(2, 2.5, rolling));

        // Occupied/non-loggable cuts do not log or consume the armed state: once vacated while still rolling, it logs once.
        var occupied = new Holding.Weighed();
        Assert.False(occupied.Record(1, 1.0, rolling, loggable: false));
        Assert.False(occupied.Holds);
        Assert.True(occupied.Record(1, 1.5, rolling, loggable: true));
        Assert.False(occupied.Record(1, 2.0, rolling, loggable: true));

        // Occupied/non-loggable cuts also do not rearm a disarmed roll log: holding with margin while occupied, then
        // losing hold and vacating, must not re-log.
        Assert.False(occupied.Record(2, 2.5, holdsWithMargin, loggable: false));
        Assert.True(occupied.Holds);
        Assert.False(occupied.Record(3, 3.0, rolling, loggable: false));
        Assert.False(occupied.Holds);
        Assert.False(occupied.Record(3, 3.5, rolling, loggable: true));
    }
}
