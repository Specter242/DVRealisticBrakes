using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

 public class TakeOverTests
{
    // A train standing charged at `found` bar, every reservoir with it, as the stock fields hand a rebuilt model; its new
    // brake valve set up by `setUp`. Returns the most any cylinder (the engine's too) shows over the next five minutes.
    private static double Dragging(bool american, double found, Action<DriverBrakeValve, double> setUp)
    {
        var rig = american
            ? new Rig(6, american: true, engine: LocomotiveBrakeSpec.American(), driver: DriverBrakeValveSpec.American())
            : new Rig(6);
        var pipe = Air.FromGaugeBar(found);
        foreach (var car in rig.Cars)
            car.ChargeTo(pipe);
        setUp(rig.Valve, pipe);
        var most = 0.0;
        rig.RunUntil(() =>
        {
            most = Math.Max(most, Math.Max(rig.Engine.Cylinder.GaugeBar, rig.Cars.Skip(1).Max(c => c.CylinderGauge) / Air.Bar));
            return false;
        }, 300);
        return most;
    }

    [Fact]
    public void ChargedStraightToThePipeAnOverchargedTrainDrags()
    {
        // The rebuild as it was: the valve charged to the pipe with no overcharge took the pipe back to running at once.
        Assert.True(Dragging(true, 5.24, (valve, pipe) => valve.ChargeTo(pipe)) > 0.3);
    }

    [Theory]
    [InlineData(true, 5.24)]
    [InlineData(true, 5.4)]
    [InlineData(false, 5.24)]
    [InlineData(false, 5.4)]
    public void TakenOverItLetsTheOverchargeDownWithoutApplyingAnything(bool american, double found)
    {
        Assert.InRange(Dragging(american, found, (valve, pipe) => valve.TakeOver(pipe)), 0, 0.05);
    }

    [Fact]
    public void TakenOverAtRunningOrUnderItStartsWithNoOvercharge()
    {
        var valve = new DriverBrakeValve(DriverBrakeValveSpec.American());
        valve.TakeOver(Air.FromGaugeBar(5.0));
        Assert.Equal(0, valve.OverchargeLeft);
        valve.TakeOver(Air.FromGaugeBar(3.2));
        Assert.Equal(0, valve.OverchargeLeft);
        valve.TakeOver(Air.FromGaugeBar(5.3));
        Assert.Equal(0.3, valve.OverchargeLeft / Air.Bar, 3);
    }
}
