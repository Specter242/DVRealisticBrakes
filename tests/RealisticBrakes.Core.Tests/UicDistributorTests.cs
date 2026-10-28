using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

/// <summary>One car's distributor against a pipe held at a set pressure.</summary>
public class UicDistributorTests
{
    private const double Dt = 0.002;

    private static (UicDistributor valve, Chamber pipe) Charged(BrakingMode mode = BrakingMode.Goods)
    {
        var valve = new UicDistributor { Mode = mode };
        var pipe = new Chamber(1000, Air.FromGaugeBar(5));
        valve.ChargeTo(pipe.Pressure);
        return (valve, pipe);
    }

    /// <summary>Holds the pipe at <paramref name="bar"/> gauge and runs until <paramref name="done"/>; returns the time, or NaN.</summary>
    private static double Hold(UicDistributor valve, Chamber pipe, double bar, Func<bool> done, double limit = 300)
    {
        for (var t = 0.0; t < limit; t += Dt)
        {
            pipe.Pressure = Air.FromGaugeBar(bar);
            valve.Step(pipe, Dt);
            if (done())
                return t + Dt;
        }
        return double.NaN;
    }

    [Theory]
    [InlineData(BrakingMode.Goods, 18, 30, 45, 60)]
    [InlineData(BrakingMode.Passenger, 3, 5, 15, 20)]
    public void FillsAndReleasesInUic543sTimes(BrakingMode mode, double applyMin, double applyMax, double releaseMin, double releaseMax)
    {
        var (valve, pipe) = Charged(mode);
        // Application time: to 95 % of the full 3.8 bar (UIC 543).
        var apply = Hold(valve, pipe, 3.0, () => valve.Cylinder.GaugeBar >= 0.95 * 3.8);
        Assert.InRange(apply, applyMin, applyMax);
        Hold(valve, pipe, 3.0, () => false, 30);
        Assert.Equal(3.8, valve.Cylinder.GaugeBar, 2);
        // Release time: down to 0.4 bar.
        var release = Hold(valve, pipe, 5.0, () => valve.Cylinder.GaugeBar <= 0.4);
        Assert.InRange(release, releaseMin, releaseMax);
    }

    [Fact]
    public void AppliesWithin1Point2SecondsOfA0Point6BarDropOver6Seconds()
    {
        var (valve, pipe) = Charged();
        var t = 0.0;
        while (!valve.Applied && t < 6)
        {
            pipe.Pressure = Air.FromGaugeBar(5 - 0.1 * t);
            valve.Step(pipe, Dt);
            t += Dt;
        }
        Assert.InRange(t, 0, 1.2);
    }

    [Fact]
    public void IgnoresASlowLeakOf0Point3BarAMinute()
    {
        var (valve, pipe) = Charged();
        for (var t = 0.0; t < 180; t += Dt)
        {
            pipe.Pressure = Air.FromGaugeBar(5 - 0.3 * t / 60);
            valve.Step(pipe, Dt);
            Assert.False(valve.Applied, $"applied at {t:F1} s on a slow leak");
        }
    }

    [Fact]
    public void TheCheckValveKeepsTheReservoirOutOfThePipe()
    {
        var (valve, pipe) = Charged();
        var pipeMass = 0.0;
        for (var t = 0.0; t < 60; t += Dt)
        {
            pipe.Pressure = Air.FromGaugeBar(3.5);
            pipeMass = pipe.Mass;
            valve.Step(pipe, Dt);
            Assert.True(pipe.Mass <= pipeMass + 1e-12, "the reservoir fed the pipe");
        }
        // The reservoir gave only what the cylinder took.
        Assert.InRange(valve.AuxiliaryReservoir.GaugeBar, 4.3, 4.7);
    }

    [Fact]
    public void ReleasesInSteps()
    {
        var (valve, pipe) = Charged(BrakingMode.Passenger);
        Hold(valve, pipe, 3.5, () => false, 30);
        Assert.Equal(3.8, valve.Cylinder.GaugeBar, 1);
        // Back up halfway: the cylinder comes down halfway and holds there.
        Hold(valve, pipe, 4.25, () => false, 30);
        Assert.True(valve.Applied);
        Assert.Equal(1.9, valve.Cylinder.GaugeBar, 1);
        Hold(valve, pipe, 5.0, () => false, 30);
        Assert.False(valve.Applied);
        Assert.Equal(0, valve.Cylinder.GaugeBar, 2);
    }

    [Fact]
    public void AMinimumReductionGivesTheFirstStage()
    {
        var (valve, pipe) = Charged(BrakingMode.Passenger);
        Hold(valve, pipe, 4.8, () => false, 20);
        Assert.True(valve.Applied);
        Assert.Equal(0.6, valve.Cylinder.GaugeBar, 1);
    }

    [Fact]
    public void RepeatedApplicationsDontUseTheBrakeUp()
    {
        var (valve, pipe) = Charged(BrakingMode.Passenger);
        for (var cycle = 0; cycle < 5; cycle++)
        {
            Hold(valve, pipe, 4.0, () => false, 20);
            Hold(valve, pipe, 4.6, () => false, 20);
        }
        // The pipe stood above the reservoir between applications and topped it up through the check valve.
        Hold(valve, pipe, 3.5, () => false, 30);
        Assert.Equal(3.8, valve.Cylinder.GaugeBar, 1);
    }

    [Fact]
    public void AnOverchargeDroppedQuicklyAppliesTheBrake()
    {
        var (valve, pipe) = Charged();
        // A long filling stroke: the control reservoir follows the pipe up.
        Hold(valve, pipe, 5.4, () => false, 120);
        Assert.InRange(valve.ControlReservoir.GaugeBar, 5.35, 5.4);
        Hold(valve, pipe, 5.0, () => false, 10);
        Assert.True(valve.Applied, "brakes should drag after an overcharge dropped at once");
    }

    [Fact]
    public void AnOverchargeLetDownSlowlyDoesNot()
    {
        var (valve, pipe) = Charged();
        Hold(valve, pipe, 5.4, () => false, 120);
        for (var t = 0.0; t < 240; t += Dt)
        {
            pipe.Pressure = Air.FromGaugeBar(5.4 - 0.4 * t / 240);
            valve.Step(pipe, Dt);
            Assert.False(valve.Applied, $"applied at {t:F1} s while the overcharge was let down");
        }
    }

    [Fact]
    public void TheReleaseValveFreesACarStandingOffAir()
    {
        var (valve, pipe) = Charged();
        Hold(valve, pipe, 0, () => false, 60);
        Assert.True(valve.Cylinder.GaugeBar > 3.5);
        var aux = valve.AuxiliaryReservoir.GaugeBar;
        valve.PullRelease();
        Hold(valve, pipe, 0, () => false, 120);
        Assert.False(valve.Applied);
        Assert.Equal(0, valve.Cylinder.GaugeBar, 2);
        Assert.Equal(aux, valve.AuxiliaryReservoir.GaugeBar, 6);
    }

    [Fact]
    public void AShortPullLetsGoWithinSecondsAndClosesByItself()
    {
        var (valve, pipe) = Charged();
        Hold(valve, pipe, 0, () => false, 60);
        valve.PullRelease();
        var t = 0.0;
        while (valve.Applied && t < 30)
        {
            Hold(valve, pipe, 0, () => false, 0.1);
            t += 0.1;
        }
        Assert.InRange(t, 0.5, 10);
        // Closed by itself: the control reservoir rests at the pipe, not drained to nothing over the next minute.
        var control = valve.ControlReservoir.Pressure;
        Hold(valve, pipe, 0, () => false, 60);
        Assert.False(valve.Applied);
        Assert.InRange(valve.ControlReservoir.Pressure, Air.Atmosphere - 1, control + 1);
    }

    [Fact]
    public void AHeldPullDrainsTheAuxiliaryReservoir()
    {
        var (valve, pipe) = Charged();
        Hold(valve, pipe, 0, () => false, 60);
        valve.ReleaseHeld = true;
        Hold(valve, pipe, 0, () => false, 120);
        valve.ReleaseHeld = false;
        Assert.False(valve.Applied);
        Assert.True(valve.AuxiliaryReservoir.GaugeBar < 0.05);
        Assert.True(valve.ControlReservoir.GaugeBar < 0.05);
    }

    [Fact]
    public void ACarReleasedByHandAppliesAgainOnAFurtherReduction()
    {
        var (valve, pipe) = Charged();
        Hold(valve, pipe, 4.0, () => false, 40);
        Assert.True(valve.Applied);
        valve.PullRelease();
        Hold(valve, pipe, 4.0, () => false, 90);
        Assert.False(valve.Applied);
        Assert.Equal(0, valve.Cylinder.GaugeBar, 2);
        Hold(valve, pipe, 3.3, () => false, 40);
        Assert.True(valve.Applied);
    }

    [Fact]
    public void TheEmptySettingBrakesLess()
    {
        var (valve, pipe) = Charged(BrakingMode.Passenger);
        valve.Load = LoadSetting.Empty;
        Hold(valve, pipe, 3.0, () => false, 30);
        Assert.InRange(valve.Cylinder.GaugeBar, 1.5, 1.7);
    }

    [Fact]
    public void AnIsolatedCarStaysReleased()
    {
        var (valve, pipe) = Charged();
        valve.Isolated = true;
        Hold(valve, pipe, 0, () => false, 30);
        Assert.Equal(0, valve.Cylinder.GaugeBar, 3);
        Assert.Equal(0, valve.PipeDraw);
    }
}
