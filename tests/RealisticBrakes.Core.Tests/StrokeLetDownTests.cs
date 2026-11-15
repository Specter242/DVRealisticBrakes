using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

 public class StrokeLetDownTests
{
    private static double EnginePipe(Rig rig) => rig.Cars[0].PipeSection.GaugeBar;

    // A stroke held until the pipe at the engine stands at 6 bar, and then for `more` seconds.
    private static Rig Stroked(int cars, double more)
    {
        var rig = new Rig(cars);
        rig.Run(30);
        rig.Valve.Position = DriverValvePosition.Fill;
        Assert.False(double.IsNaN(rig.RunUntil(() => EnginePipe(rig) >= 5.95, 60)), "the stroke never reached 6 bar");
        rig.Run(more);
        rig.Valve.Position = DriverValvePosition.Running;
        return rig;
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 30)]
    [InlineData(10, 2)]
    [InlineData(10, 30)]
    [InlineData(40, 2)]
    [InlineData(40, 30)]
    public void TheSurgeComesDownOverAFewSecondsAndTheOverchargeOverMinutes(int cars, double more)
    {
        var rig = Stroked(cars, more);
        // Not within a second, as before: about 0.35 bar a second.
        rig.Run(1);
        Assert.True(EnginePipe(rig) > 5.55, $"{EnginePipe(rig):F2} bar a second after the stroke");
        var atOverchargeAt = rig.RunUntil(() => EnginePipe(rig) < 5.43, 30) + 1;
        Assert.InRange(atOverchargeAt, 1.4, 4.0);
        // Then the four minutes' tail, as before.
        rig.Run(30 - atOverchargeAt);
        Assert.InRange(EnginePipe(rig), 5.28, 5.40);
        rig.Run(90);
        Assert.InRange(EnginePipe(rig), 5.15, 5.27);
        rig.Run(150);
        Assert.InRange(EnginePipe(rig), 4.97, 5.03);
        Assert.Equal(0, rig.Valve.OverchargeLeft);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(20, 2)]
    [InlineData(20, 60)]
    [InlineData(40, 30)]
    public void NothingAppliesOrDragsWhileItComesDown(int cars, double more)
    {
        var rig = Stroked(cars, more);
        var applied = rig.RunUntil(() => rig.Engine.Cylinder.GaugeBar > 0.05 || rig.Cars.Skip(1).Any(c => c.Distributor!.Applied || c.CylinderGauge > 0.05 * Air.Bar), 300);
        Assert.True(double.IsNaN(applied), $"a brake came on {applied:F1} s after the stroke");
        // The cars' control reservoirs held no more than the overcharge, and came down with it.
        Assert.All(rig.Cars.Skip(1), c => Assert.InRange(c.Distributor!.ControlReservoir.GaugeBar, 4.95, 5.05));
    }

    [Fact]
    public void ABrakeMadeDuringTheSurgeIsMadeFromTheOverchargeNotFromAboveIt()
    {
        var rig = Stroked(20, 10);
        rig.Run(0.5);
        Assert.True(rig.Valve.OverchargeLeft > 0.45 * Air.Bar, "the surge was already down");
        rig.Service(0.5);
        rig.Run(0.1);
        Assert.InRange(rig.Valve.OverchargeLeft / Air.Bar, 0.399, 0.401);
        rig.Run(40);
        // The equalizing reservoir asks running plus the overcharge less the reduction: 4.9 bar.
        Assert.InRange(rig.Valve.EqualizingReservoir.GaugeBar, 4.88, 4.92);
        Assert.InRange(rig.Rear.CylinderGauge / Air.Bar, 0.6, 1.4);
    }

    public static IEnumerable<object[]> BrakesAfterAStroke()
    {
        foreach (var cars in new[] { 1, 10, 20, 40 })
        foreach (var mode in new[] { BrakingMode.Goods, BrakingMode.Passenger })
        foreach (var held in new[] { 0.0, 10, 30 })
        foreach (var brakeAfter in new[] { 0.0, 2, 5, 10, 30 })
            yield return new object[] { cars, mode, held, brakeAfter };
    }

    [Theory]
    [MemberData(nameof(BrakesAfterAStroke))]
    public void ABrakeMadeAnyTimeAfterAStrokeReleasesFully(int cars, BrakingMode mode, double held, double brakeAfter)
    {
        var rig = new Rig(cars, mode);
        rig.Run(30);
        rig.Valve.Position = DriverValvePosition.Fill;
        Assert.False(double.IsNaN(rig.RunUntil(() => EnginePipe(rig) >= 5.95, 60)), "the stroke never reached 6 bar");
        rig.Run(held);
        rig.Valve.Position = DriverValvePosition.Running;
        rig.Run(brakeAfter);
        rig.Service(0.5);
        rig.Run(40);
        Assert.True(rig.Cars.Skip(1).Any(c => c.CylinderGauge > 0.3 * Air.Bar), "the brake didn't apply");
        rig.Valve.Position = DriverValvePosition.Running;
        bool Released() => rig.Engine.Cylinder.GaugeBar < 0.01 && rig.Cars.Skip(1).All(c => c.CylinderGauge < 0.01 * Air.Bar);
        var released = rig.RunUntil(Released, 600);
        string Worst() => $"{rig.Cars.Skip(1).Max(c => c.CylinderGauge) / Air.Bar:F2} bar on a car, {rig.Engine.Cylinder.GaugeBar:F2} on the engine";
        Assert.False(double.IsNaN(released), $"not released 600 s after the release: {Worst()}");
        // And nothing comes on again as the overcharge comes down, all of it.
        bool Again() => rig.Engine.Cylinder.GaugeBar > 0.05 || rig.Cars.Skip(1).Any(c => c.CylinderGauge > 0.05 * Air.Bar);
        var end = rig.RunUntil(() => Again() || rig.Valve.OverchargeLeft <= 0, 900);
        Assert.False(Again(), $"a brake came on again {end:F0} s after the train released: {Worst()}");
        Assert.False(double.IsNaN(end), $"the overcharge stood at {rig.Valve.OverchargeLeft / Air.Bar:F2} bar 900 s after the train released");
        rig.Run(30);
        Assert.False(Again(), $"a brake came on after the overcharge was down: {Worst()}");
    }

    [Fact]
    public void ALeakingPipeStillLetsTheOverchargeDown()
    {
        // The let-down waits for the release at most ReleaseHoldLimit.
        var rig = Stroked(10, 5);
        rig.Service(0.5);
        rig.Run(40);
        rig.Rear.PipeLeak = new Orifice(2.0);
        rig.Valve.Position = DriverValvePosition.Running;
        rig.Run(rig.Valve.Spec.ReleaseHoldLimit + 300);
        Assert.InRange(rig.Valve.OverchargeLeft / Air.Bar, 0, 0.001);
    }

    [Fact]
    public void ASavedSurgeIsRestored()
    {
        var valve = new DriverBrakeValve();
        valve.RestoreOvercharge(0.8 * Air.Bar);
        Assert.Equal(0.8 * Air.Bar, valve.OverchargeLeft, 3);
        valve.RestoreOvercharge(2 * Air.Bar);
        Assert.Equal(valve.Spec.FillLimit, valve.OverchargeLeft, 3);
    }

    [Fact]
    public void AmericanValvesKeepTheirOwnRelease()
    {
        Assert.Equal(0, DriverBrakeValveSpec.American().StrokeLetDown);
        Assert.Equal(0, DriverBrakeValveSpec.H6().StrokeLetDown);
        // Every new path is gated on StrokeLetDown, so their release is as before; AarControlValveTests, EtEquipmentTests,
        // and MixedEquipmentTests hold it.
    }
}

 public class AdjustTests
{
    private static double EnginePipe(Rig rig) => rig.Cars[0].PipeSection.GaugeBar;

    [Fact]
    public void HeldItRaisesThePipeSlowlyToFiveAndAHalfBar()
    {
        var rig = new Rig(10);
        rig.Run(30);
        rig.Valve.Adjusting = true;
        rig.Run(10);
        Assert.InRange(EnginePipe(rig), 5.15, 5.25);
        var at54 = rig.RunUntil(() => EnginePipe(rig) >= 5.39, 60) + 10;
        Assert.InRange(at54, 18, 24);
        rig.Run(60);
        Assert.InRange(EnginePipe(rig), 5.48, 5.51);
        Assert.Equal(0.5, rig.Valve.OverchargeLeft / Air.Bar, 3);
    }

    [Fact]
    public void LetGoItComesDownAtATenthOfABarAMinuteWithoutApplyingAnything()
    {
        var rig = new Rig(20);
        rig.Run(30);
        rig.Valve.Adjusting = true;
        rig.Run(40);
        rig.Valve.Adjusting = false;
        // The valve's reference, which the pipe follows once the train has charged to it.
        var from = rig.Valve.EqualizingReservoir.GaugeBar;
        rig.Run(60);
        Assert.InRange(from - rig.Valve.EqualizingReservoir.GaugeBar, 0.09, 0.11);
        var applied = rig.RunUntil(() => rig.Engine.Cylinder.GaugeBar > 0.05 || rig.Cars.Skip(1).Any(c => c.Distributor!.Applied || c.CylinderGauge > 0.05 * Air.Bar), 300);
        Assert.True(double.IsNaN(applied), $"a brake came on {applied:F0} s after Angleichen was let go");
        Assert.InRange(EnginePipe(rig), 4.98, 5.02);
    }

    [Fact]
    public void LetGoPartWayItComesDownFromWhereItReached()
    {
        var rig = new Rig(5);
        rig.Run(30);
        rig.Valve.Adjusting = true;
        rig.Run(10);
        rig.Valve.Adjusting = false;
        Assert.InRange(rig.Valve.OverchargeLeft / Air.Bar, 0.19, 0.21);
        rig.Run(120);
        Assert.InRange(rig.Valve.OverchargeLeft / Air.Bar, 0, 0.005);
    }

    [Fact]
    public void ABrakeMadeAfterAngleichenReleasesFully()
    {
        foreach (var mode in new[] { BrakingMode.Goods, BrakingMode.Passenger })
        foreach (var brakeAfter in new[] { 0.0, 5, 30 })
        {
            var rig = new Rig(20, mode);
            rig.Run(30);
            rig.Valve.Adjusting = true;
            rig.Run(40);
            rig.Valve.Adjusting = false;
            rig.Run(brakeAfter);
            rig.Service(0.5);
            rig.Run(40);
            Assert.True(rig.Cars.Skip(1).Any(c => c.CylinderGauge > 0.3 * Air.Bar), "the brake didn't apply");
            rig.Valve.Position = DriverValvePosition.Running;
            var released = rig.RunUntil(() => rig.Engine.Cylinder.GaugeBar < 0.01 && rig.Cars.Skip(1).All(c => c.CylinderGauge < 0.01 * Air.Bar), 600);
            Assert.False(double.IsNaN(released), $"{mode}, braked {brakeAfter} s after: {rig.Cars.Skip(1).Max(c => c.CylinderGauge) / Air.Bar:F2} bar left");
            var again = rig.RunUntil(() => rig.Cars.Skip(1).Any(c => c.CylinderGauge > 0.05 * Air.Bar) || rig.Valve.OverchargeLeft <= 0, 900);
            Assert.True(rig.Valve.OverchargeLeft <= 0, $"{mode}, braked {brakeAfter} s after: a car came on again at {again:F0} s");
        }
    }

    [Fact]
    public void HeldOutsideRunningOrOnAnAmericanValveItDoesNothing()
    {
        var rig = new Rig(5);
        rig.Run(30);
        rig.Service(0.5);
        rig.Valve.Adjusting = true;
        rig.Run(20);
        Assert.InRange(rig.Valve.OverchargeLeft / Air.Bar, 0, 0.001);
        Assert.Equal(0, DriverBrakeValveSpec.American().AdjustRate);
        Assert.Equal(0, DriverBrakeValveSpec.H6().AdjustRate);
    }
}
