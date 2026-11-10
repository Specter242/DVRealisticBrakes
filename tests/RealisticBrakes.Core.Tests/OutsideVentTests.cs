using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

 public class OutsideVentTests
{
    private static double PipeBar(CarBrake car) => Air.ToGaugeBar(car.PipeSection.Pressure);

    private static double CylinderBar(CarBrake car) => car.CylinderGauge / Air.Bar;

    [Fact]
    public void AnOutsideVentAtTheEngineEmptiesThePipeAgainstTheValveAndAppliesTheTrain()
    {
        var rig = new Rig(10, BrakingMode.Passenger);
        rig.Run(30);
        rig.Cars[0].OutsideVent = true;
        rig.Run(1);
        Assert.True(rig.Cars[0].OutsideVentFlow > 0.1, $"the vent blows {rig.Cars[0].OutsideVentFlow:F3} kg/s");
        rig.Run(19);
        // The brake valve, left in running, feeds the pipe all the while; the emergency port's bore beats it.
        Assert.Equal(DriverValvePosition.Running, rig.Valve.Position);
        Assert.InRange(PipeBar(rig.Cars[0]), 0, 0.3);
        Assert.InRange(PipeBar(rig.Rear), 0, 0.3);
        Assert.InRange(CylinderBar(rig.Front), 3.7, 3.9);
        Assert.InRange(CylinderBar(rig.Rear), 3.7, 3.9);
        Assert.InRange(CylinderBar(rig.Cars[0]), 3.7, 3.9);
    }

    [Fact]
    public void ClosedAgainTheValveRechargesThePipeAndTheTrainReleases()
    {
        var rig = new Rig(10, BrakingMode.Passenger);
        rig.Run(30);
        rig.Cars[0].OutsideVent = true;
        rig.Run(20);
        rig.Cars[0].OutsideVent = false;
        rig.Run(1);
        Assert.Equal(0, rig.Cars[0].OutsideVentFlow);
        rig.Run(59);
        Assert.InRange(PipeBar(rig.Rear), 4.9, 5.1);
        Assert.InRange(CylinderBar(rig.Front), 0, 0.05);
        Assert.InRange(CylinderBar(rig.Rear), 0, 0.05);
    }

    [Fact]
    public void ACarWithNoVentOpenLetsNothingOut()
    {
        var rig = new Rig(3);
        rig.Run(30);
        Assert.All(rig.Cars, car => Assert.Equal(0, car.OutsideVentFlow));
        Assert.InRange(PipeBar(rig.Rear), 4.95, 5.05);
    }
}

public class OutsideVentWatchTests
{
    [Fact]
    public void AFallUnderWhatWasWrittenTheTickBeforeOpensTheVent()
    {
        var watch = new OutsideVentWatch();
        watch.Wrote(6.0, tick: 10);
        Assert.True(watch.Check(5.9, 0.016, tick: 11));
        Assert.True(watch.Fell);
    }

    [Fact]
    public void TheValueAsWrittenOrARiseIsNoVent()
    {
        var watch = new OutsideVentWatch();
        watch.Wrote(6.0, tick: 10);
        Assert.False(watch.Check(6.0, 0.016, tick: 11));
        watch.Wrote(6.0, tick: 11);
        Assert.False(watch.Check(6.0 - OutsideVentWatch.Drop / 2, 0.016, tick: 12));
        watch.Wrote(6.0, tick: 12);
        Assert.False(watch.Check(6.3, 0.016, tick: 13));
    }

    [Fact]
    public void TheVentStaysOpenAcrossFramesWithNoPhysicsStepThenCloses()
    {
        var watch = new OutsideVentWatch();
        watch.Wrote(6.0, tick: 1);
        Assert.True(watch.Check(5.8, 0.01, tick: 2));
        // Frames between the other mod's physics steps: the field stands as written.
        watch.Wrote(5.7, tick: 2);
        Assert.True(watch.Check(5.7, 0.01, tick: 3));
        Assert.False(watch.Fell);
        var tick = 3L;
        var time = 0.0;
        while (watch.Open && time < 1)
        {
            watch.Wrote(5.7, tick);
            tick++;
            time += 0.01;
            watch.Check(5.7, 0.01, tick);
        }
        Assert.InRange(time, OutsideVentWatch.Hold - 0.02, OutsideVentWatch.Hold + 0.02);
    }

    [Fact]
    public void AValueWrittenLongerAgoIsNotComparedWith()
    {
        // A frame the stock brake ran the set (AI Traffic's train, a failure), which writes the field itself.
        var watch = new OutsideVentWatch();
        watch.Wrote(6.0, tick: 10);
        Assert.False(watch.Check(4.0, 0.016, tick: 12));
        Assert.False(watch.Check(4.0, 0.016, tick: 13));
    }

    [Fact]
    public void AStockSetterIsNoVent()
    {
        var watch = new OutsideVentWatch();
        watch.Wrote(6.0, tick: 10);
        watch.Took(3.0);
        Assert.False(watch.Check(3.0, 0.016, tick: 11));
    }

    [Fact]
    public void AGuestNeverVents()
    {
        // A multiplayer guest's field is written from the host's packets.
        var watch = new OutsideVentWatch();
        watch.Wrote(6.0, tick: 10);
        Assert.False(watch.Check(1.0, 0.016, tick: 11, watching: false));
        Assert.False(watch.Fell);
    }
}
