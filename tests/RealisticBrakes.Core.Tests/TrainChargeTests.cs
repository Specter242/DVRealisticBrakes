using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

/// <summary>The notice for a train braked before its cars have charged (<see cref="TrainCharge"/>).</summary>
public class TrainChargeTests
{
    private static (int braked, int shortOfAir) Count(Rig rig)
    {
        TrainCharge.Count(rig.Cars, rig.Valve.Spec.Running, out var braked, out var shortOfAir);
        return (braked, shortOfAir);
    }

    private static Rig Cold(int cars, double seconds, bool american = false)
    {
        var rig = new Rig(cars, charged: false, american: american);
        rig.Engine.Distributor.ChargeTo(Air.FromGaugeBar(5));
        rig.Valve.ChargeTo(Air.FromGaugeBar(5));
        rig.Run(seconds);
        return rig;
    }

    [Fact]
    public void AChargedTrainIsNotShort()
    {
        Assert.Equal((15, 0), Count(new Rig(15)));
        Assert.Equal((15, 0), Count(new Rig(15, american: true)));
    }

    [Fact]
    public void ATrainJustCoupledIsShortThoughItsPipeReadsNearlyFull()
    {
        // Crazylife, 2026-11-09: a minute after coupling the front of the pipe reads over 4 bar, and the cars can't brake.
        var rig = Cold(15, 60);
        Assert.True(rig.Front.PipeSection.GaugeBar > 4);
        Assert.Equal((15, 15), Count(rig));
        rig.Service(1.5);
        rig.Run(60);
        Assert.True(rig.Cars.Skip(1).Average(c => c.CylinderGauge) < 1.0 * Air.Bar);
        Assert.True(rig.Engine.Cylinder.GaugeBar > 3.5);
    }

    [Fact]
    public void OnceItHasChargedItIsNotShortAndBrakesInFull()
    {
        var rig = Cold(15, 300);
        Assert.Equal((15, 0), Count(rig));
        rig.Service(1.5);
        rig.Run(60);
        Assert.True(rig.Cars.Skip(1).Min(c => c.CylinderGauge) > 3.5 * Air.Bar);
    }

    [Fact]
    public void ACarCountsAsChargedOnlyOnceItBrakesWell()
    {
        // Wherever the count says no car is short, a full service gives every car most of its pressure.
        var checkedTrains = 0;
        foreach (var american in new[] { false, true })
            foreach (var seconds in new[] { 90, 150, 210, 300 })
            {
                var rig = Cold(10, seconds, american);
                var (braked, shortOfAir) = Count(rig);
                if (shortOfAir > 0)
                    continue;
                rig.Service(1.5);
                rig.Run(60);
                var least = rig.Cars.Skip(1).Min(c => c.CylinderGauge) / rig.Cars[1].Valve!.FullServiceCylinder;
                Assert.True(least > 0.7, $"{(american ? "American" : "European")}, {seconds} s: {braked} cars, least {least:P0}");
                checkedTrains++;
            }
        Assert.True(checkedTrains >= 2, $"only {checkedTrains} trains had charged");
    }

    [Fact]
    public void AEuropeanTrainBrakedAgainStraightAfterAReleaseIsNotShort()
    {
        var rig = new Rig(15);
        rig.Service(1.5);
        rig.Run(60);
        rig.Valve.Position = DriverValvePosition.Running;
        rig.Run(20);
        Assert.Equal(0, Count(rig).shortOfAir);
    }

    [Fact]
    public void EnginesAndCutOutCarsDontCount()
    {
        var rig = Cold(4, 5);
        rig.Cars[2].Valve!.Isolated = true;
        Assert.Equal((3, 3), Count(rig));
        Assert.True(double.IsNaN(TrainCharge.Supply(rig.Cars[0])));
    }

    [Fact]
    public void TheHandleAppliesInServiceApplyAndEmergencyOnly()
    {
        var valve = new DriverBrakeValve();
        Assert.False(TrainCharge.Applying(valve));
        Assert.False(TrainCharge.Applying(null));
        valve.Position = DriverValvePosition.Service;
        valve.ServiceReduction = 0.5 * Air.Bar;
        Assert.True(TrainCharge.Applying(valve));
        valve.CutIn = false;
        Assert.False(TrainCharge.Applying(valve));
        valve.CutIn = true;
        foreach (var position in new[] { DriverValvePosition.Apply, DriverValvePosition.Emergency })
        {
            valve.Position = position;
            Assert.True(TrainCharge.Applying(valve));
        }
        foreach (var position in new[] { DriverValvePosition.Running, DriverValvePosition.Lap, DriverValvePosition.Fill, DriverValvePosition.Holding })
        {
            valve.Position = position;
            Assert.False(TrainCharge.Applying(valve));
        }
    }

    [Fact]
    public void TheDriverIsToldOnceAsTheBrakeGoesOn()
    {
        Assert.True(TrainCharge.Tell(wasApplying: false, applying: true, braked: 15, shortOfAir: 15, sinceLast: double.PositiveInfinity));
        // Not while the brake stays on, not for a few cars, not twice within the minute, and not for an engine alone.
        Assert.False(TrainCharge.Tell(true, true, 15, 15, double.PositiveInfinity));
        Assert.False(TrainCharge.Tell(false, false, 15, 15, double.PositiveInfinity));
        Assert.False(TrainCharge.Tell(false, true, 16, 3, double.PositiveInfinity));
        Assert.True(TrainCharge.Tell(false, true, 16, 4, double.PositiveInfinity));
        Assert.False(TrainCharge.Tell(false, true, 15, 15, TrainCharge.RepeatAfter - 1));
        Assert.False(TrainCharge.Tell(false, true, 0, 0, double.PositiveInfinity));
        Assert.True(TrainCharge.Tell(false, true, 2, 1, double.PositiveInfinity));
    }

    [Fact]
    public void TheNoticeSaysHowMany()
    {
        Assert.Equal("12 of 15 cars are short of air and will brake weakly: give the train time to charge", TrainCharge.Message(15, 12));
        Assert.Equal("All 15 cars are short of air and will brake weakly: give the train time to charge", TrainCharge.Message(15, 15));
        Assert.Equal("The car is short of air and will brake weakly: give the train time to charge", TrainCharge.Message(1, 1));
    }
}
