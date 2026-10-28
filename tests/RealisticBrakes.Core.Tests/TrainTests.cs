using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

/// <summary>Whole trains: the pipe, the cars' valves, and the engine together.</summary>
public class TrainTests
{
    [Fact]
    public void ALightEngineMakesFullServiceInAboutFiveSeconds()
    {
        var rig = new Rig(0);
        rig.Service(1.5);
        var t = rig.RunUntil(() => rig.Cars[0].PipeSection.GaugeBar <= 3.55, 30);
        Assert.InRange(t, 4, 7);
        rig.Run(10);
        Assert.InRange(rig.Engine.Cylinder.GaugeBar, 3.6, 3.8);
    }

    [Fact]
    public void AnEmergencyReachesTheRearAtUicsBrakeSpeed()
    {
        var rig = new Rig(50);
        rig.Valve.Position = DriverValvePosition.Emergency;
        var t = rig.RunUntil(() => rig.Rear.Distributor!.Applied, 30);
        // UIC 540: at least 250 m/s from the driver's valve to the last car's brake.
        Assert.InRange(rig.Length / t, 245, 300);
    }

    [Fact]
    public void TheRearOfALongTrainAppliesLaterAndSlower()
    {
        var rig = new Rig(50);
        rig.Service(1.5);
        var front = double.NaN;
        var rear = rig.RunUntil(() =>
        {
            if (double.IsNaN(front) && rig.Front.CylinderGauge >= 0.9 * 3.8 * Air.Bar)
                front = rig.Time;
            return rig.Rear.CylinderGauge >= 0.9 * 3.8 * Air.Bar;
        }, 120);
        Assert.InRange(front, 18, 30);
        Assert.True(rig.Time > front + 10, $"rear at {rig.Time:F1} s, front at {front:F1} s");
        Assert.False(double.IsNaN(rear));
    }

    [Fact]
    public void ALongGoodsTrainTakesMinutesToReleaseAndRecharge()
    {
        var rig = new Rig(50);
        rig.Service(1.5);
        rig.Run(60);
        rig.Valve.Position = DriverValvePosition.Running;
        var released = rig.RunUntil(() => rig.Rear.CylinderGauge < 0.4 * Air.Bar, 900);
        var recharged = rig.RunUntil(() => rig.Cars.Skip(1).All(c => c.Distributor!.AuxiliaryReservoir.GaugeBar > 4.9), 900);
        Assert.InRange(released, 90, 300);
        Assert.False(double.IsNaN(recharged));
    }

    [Fact]
    public void TenTimesTheTimeInBigStepsMatchesTenTimesTheFrames()
    {
        // The game's default setting runs the air ten times as fast: the adapter advances each
        // frame by ten frames' time, so long steps must give what short ones do.
        Rig Run(double step, int frames)
        {
            var rig = new Rig(30);
            rig.Service(1.5);
            for (var i = 0; i < frames; i++)
                rig.Line.Advance(step);
            rig.Valve.Position = DriverValvePosition.Running;
            for (var i = 0; i < frames; i++)
                rig.Line.Advance(step);
            return rig;
        }

        var fine = Run(1 / 60.0, 60 * 60);
        var coarse = Run(10 / 60.0, 6 * 60);
        Assert.Equal(fine.Rear.CylinderGauge / Air.Bar, coarse.Rear.CylinderGauge / Air.Bar, 2);
        Assert.Equal(fine.Rear.Distributor!.AuxiliaryReservoir.GaugeBar, coarse.Rear.Distributor!.AuxiliaryReservoir.GaugeBar, 2);
        Assert.Equal(fine.Rear.PipeSection.GaugeBar, coarse.Rear.PipeSection.GaugeBar, 2);
    }

    [Fact]
    public void AStandingTrainSettlesAndALeakKeepsItAwake()
    {
        var rig = new Rig(10);
        rig.Valve.CutIn = false;
        rig.Engine.Independent = 0;
        var settled = rig.RunUntil(() => rig.Line.ChangeRate < 1, 120);
        Assert.False(double.IsNaN(settled), $"still changing at {rig.Line.ChangeRate:F1} Pa/s");
        rig.Rear.PipeLeak = new Orifice(0.3);
        rig.Run(1);
        Assert.True(rig.Line.ChangeRate > 1, $"a leaking train reads {rig.Line.ChangeRate:F2} Pa/s");
    }

    [Fact]
    public void AChargedEngineOnColdCarsWaitsForTheTrainToCharge()
    {
        var rig = new Rig(30, charged: false);
        rig.Engine.Distributor.ChargeTo(Air.FromGaugeBar(5));
        rig.Valve.ChargeTo(Air.FromGaugeBar(5));
        rig.Run(5);
        // The empty reservoirs pull the pipe down behind the engine.
        Assert.True(rig.Rear.PipeSection.GaugeBar < 2);
        var charged = rig.RunUntil(() => rig.Cars.Skip(1).All(c => c.Distributor!.AuxiliaryReservoir.GaugeBar > 4.9), 3600);
        Assert.InRange(charged, 3 * 60, 20 * 60);
    }

    [Fact]
    public void AFillingStrokeReleasesFasterWithoutDraggingAfterward()
    {
        double Release(bool stroke)
        {
            var rig = new Rig(40);
            rig.Service(1.5);
            rig.Run(60);
            var start = rig.Time;
            if (stroke)
            {
                rig.Valve.Position = DriverValvePosition.Fill;
                rig.Run(6);
            }
            rig.Valve.Position = DriverValvePosition.Running;
            rig.RunUntil(() => rig.Rear.CylinderGauge < 0.4 * Air.Bar, 600);
            var released = rig.Time - start;
            var allReleased = rig.RunUntil(() => rig.Cars.Skip(1).All(c => !c.Distributor!.Applied), 600);
            Assert.False(double.IsNaN(allReleased), "the train never released");
            // The overcharge is let down over minutes without applying a car again.
            var dragged = rig.RunUntil(() => rig.Cars.Skip(1).Any(c => c.Distributor!.Applied), 300);
            Assert.True(double.IsNaN(dragged), "a car applied as the overcharge came down");
            return released;
        }

        var plain = Release(false);
        var stroked = Release(true);
        Assert.True(stroked < plain * 0.8, $"with a stroke {stroked:F0} s, without {plain:F0} s");
    }

    [Fact]
    public void AFillingStrokeHeldLongLeavesNothingDragging()
    {
        // A player's report (2026-10-28): a stroke or release held 20 s or more left the engine and front cars dragging,
        // more and more as the overcharge came down. The distributors' overload limiter keeps it out of their memory.
        var rig = new Rig(20);
        rig.Service(1.5);
        rig.Run(60);
        rig.Valve.Position = DriverValvePosition.Fill;
        rig.Run(60);
        rig.Valve.Position = DriverValvePosition.Running;
        var applied = rig.RunUntil(() => rig.Engine.Cylinder.GaugeBar > 0.05 || rig.Cars.Skip(1).Any(c => c.Distributor!.Applied), 360);
        Assert.True(double.IsNaN(applied), $"a brake came on {applied:F0} s after the stroke");
    }

    [Fact]
    public void ABrakeMadeWhileAnOverchargeComesDownReleasesFully()
    {
        // The same report's other half: braking again within the overcharge's four minutes left the train dragging after the release.
        var rig = new Rig(20);
        rig.Service(1.5);
        rig.Run(60);
        rig.Valve.Position = DriverValvePosition.Fill;
        rig.Run(10);
        rig.Valve.Position = DriverValvePosition.Running;
        rig.Run(30);
        rig.Service(0.5);
        rig.Run(40);
        Assert.InRange(rig.Rear.CylinderGauge / Air.Bar, 1.1, 1.4);
        rig.Valve.Position = DriverValvePosition.Running;
        rig.Run(180);
        Assert.InRange(rig.Engine.Cylinder.GaugeBar, 0, 0.05);
        Assert.All(rig.Cars.Skip(1), c => Assert.InRange(c.CylinderGauge / Air.Bar, 0, 0.05));
    }
}

public class ManualLapAndRestoreTests
{
    [Fact]
    public void AManualLapValveMakesAndHoldsAPreciseReduction()
    {
        var rig = new Rig(10, BrakingMode.Passenger);
        rig.Valve.Position = DriverValvePosition.Apply;
        rig.RunUntil(() => rig.Valve.EqualizingReservoir.GaugeBar <= 4.6, 10);
        rig.Valve.Position = DriverValvePosition.Lap;
        var er = rig.Valve.EqualizingReservoir.GaugeBar;
        Assert.InRange(er, 4.55, 4.6);
        rig.Run(40);
        // The pipe follows the lapped reservoir and stays; the cars hold a 0.4 bar reduction's brake.
        Assert.Equal(er, rig.Valve.EqualizingReservoir.GaugeBar, 6);
        Assert.InRange(rig.Rear.PipeSection.GaugeBar, er - 0.03, er + 0.03);
        Assert.InRange(rig.Rear.CylinderGauge / Air.Bar, 0.9, 1.2);
    }

    [Fact]
    public void ARestoredCarStandsAsTheGameLeftIt()
    {
        var valve = new UicDistributor();
        var pipe = new Chamber(1000, Air.FromGaugeBar(4.0));
        valve.Restore(Air.FromGaugeBar(5), Air.FromGaugeBar(4.6), Air.FromGaugeBar(2.5), pipe.Pressure);
        Assert.True(valve.Applied);
        for (var i = 0; i < 20000; i++)
            valve.Step(pipe, 0.002);
        Assert.Equal(3.8 * 1.0 / 1.5, valve.Cylinder.GaugeBar, 1);
    }

    [Fact]
    public void EnginesShareMainReservoirsThroughPipedCars()
    {
        var g = Rig.Geometry;
        var a = new LocomotiveBrake(new LocomotiveBrakeSpec { MainReservoirVolume = 1.0 });
        var b = new LocomotiveBrake(new LocomotiveBrakeSpec { MainReservoirVolume = 1.0 });
        var cars = new List<CarBrake> { new(20, g, locomotive: a), new(10, g), new(20, g, locomotive: b) };
        foreach (var c in cars)
            c.ChargeTo(Air.FromGaugeBar(5));
        a.MainReservoir.Pressure = Air.FromAbsoluteBar(9);
        b.MainReservoir.Pressure = Air.FromAbsoluteBar(6);
        var line = new BrakeLine(cars, g, new[] { true, true, true });
        for (var i = 0; i < 60 * 30; i++)
            line.Advance(1 / 60.0);
        Assert.InRange(Math.Abs(a.MainReservoir.Pressure - b.MainReservoir.Pressure), 0, 0.01 * Air.Bar);

        // A car without the pipe between them keeps them apart.
        b.MainReservoir.Pressure = Air.FromAbsoluteBar(6);
        var apart = new BrakeLine(cars, g, new[] { true, false, true });
        var before = a.MainReservoir.Pressure;
        apart.Advance(1);
        Assert.InRange(Math.Abs(before - a.MainReservoir.Pressure), 0, 1);
    }

    [Fact]
    public void AResizedReservoirKeepsItsPressure()
    {
        var c = new Chamber(1.0, Air.FromAbsoluteBar(8));
        c.Resize(1.491);
        Assert.Equal(8, Air.ToAbsoluteBar(c.Pressure), 9);
        Assert.Equal(1.491, c.Volume);
    }
}

public class ReviewRegressionTests
{
    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    public void ALightApplicationHolds(int cars)
    {
        // The smallest self-lap step: the pipe's reflections mustn't release the rear car mid-application
        // (they did when the game setting thinned the pipe's friction; review, 2026-10-24).
        var rig = new Rig(cars);
        rig.Service(0.225);
        var applied = false;
        var released = rig.RunUntil(() =>
        {
            applied |= rig.Rear.Distributor!.Applied;
            return applied && !rig.Rear.Distributor!.Applied;
        }, 20);
        Assert.True(applied, "the rear car never applied");
        Assert.True(double.IsNaN(released), $"the rear car released {released:F1} s into a held application");
    }

    [Fact]
    public void ADeadEngineBrakesFromTheTrainsAir()
    {
        var g = Rig.Geometry;
        var live = new LocomotiveBrake(new LocomotiveBrakeSpec { MainReservoirVolume = 2.0 }, new DriverBrakeValve());
        var dead = new LocomotiveBrake(new LocomotiveBrakeSpec { MainReservoirVolume = 1.0 }, new DriverBrakeValve { CutIn = false });
        var cars = new List<CarBrake> { new(20, g, locomotive: live), new(20, g, locomotive: dead), new(15, g, new UicDistributor()) };
        foreach (var c in cars)
            c.ChargeTo(Air.FromGaugeBar(5));
        live.MainReservoir.Pressure = Air.FromAbsoluteBar(9);
        dead.MainReservoir.Pressure = Air.Atmosphere;
        var line = new BrakeLine(cars, g);
        // A 1,000 L reservoir through the 3.2 mm choke takes about ten minutes, as a real dead engine does.
        for (var i = 0; i < 60 * 900; i++)
        {
            live.CompressorFlow = live.MainReservoir.Pressure < Air.FromAbsoluteBar(9) ? Rig.FreeAir(100) : 0;
            line.Advance(1 / 60.0);
        }
        Assert.InRange(dead.MainReservoir.GaugeBar, 4.6, 5.0);
        live.DriverValve!.Position = DriverValvePosition.Service;
        live.DriverValve.ServiceReduction = 1.5 * Air.Bar;
        for (var i = 0; i < 60 * 30; i++)
            line.Advance(1 / 60.0);
        Assert.InRange(dead.Cylinder.GaugeBar, 3.5, 3.8);
    }

    [Fact]
    public void ACylinderTheGameForcedOnStaysOnUntilTheTrainIsCharged()
    {
        // The game's brakes-on-spawn: every cylinder at 4.5 bar absolute and the control reservoir at 6, on a cold train.
        var engine = new LocomotiveBrake(new LocomotiveBrakeSpec(), new DriverBrakeValve());
        var car = new CarBrake(20, Rig.Geometry, locomotive: engine);
        car.PipeSection.Pressure = Air.Atmosphere;
        engine.MainReservoir.Pressure = Air.Atmosphere;
        // The adapter starts the distributor's pilot where the engine's cylinders stand.
        engine.Cylinder.Pressure = Air.FromAbsoluteBar(4.5);
        engine.Distributor.Restore(Air.FromAbsoluteBar(6), Air.Atmosphere, engine.Cylinder.Pressure, Air.Atmosphere);
        var line = new BrakeLine(new[] { car }, Rig.Geometry);
        for (var i = 0; i < 60 * 10; i++)
            line.Advance(1 / 60.0);
        Assert.Equal(3.5, engine.Cylinder.GaugeBar, 2);
    }
}

public class BailOffTests
{
    [Fact]
    public void ABailReleasesTheEngineHoldsAndClearsOnRelease()
    {
        var rig = new Rig(5, BrakingMode.Passenger);
        rig.Service(0.8);
        rig.Run(20);
        var applied = rig.Engine.Cylinder.GaugeBar;
        Assert.InRange(applied, 1.8, 2.2);
        Assert.True(rig.Engine.BailOff() > 0);
        // The engine's cylinders release through the relay's exhaust in about 4 s.
        rig.Run(40);
        Assert.InRange(rig.Engine.Cylinder.GaugeBar, 0, 0.05);
        Assert.InRange(rig.Rear.CylinderGauge / Air.Bar, 1.8, 2.2);

        // A further reduction applies the engine again by that much.
        rig.Service(1.2);
        rig.Run(20);
        Assert.InRange(rig.Engine.Cylinder.GaugeBar, 0.9, 1.1);

        // A release clears the bail, so the next application applies the engine in full.
        rig.Valve.Position = DriverValvePosition.Running;
        rig.Run(60);
        Assert.Equal(0, rig.Engine.BailedOff);
        rig.Service(0.8);
        rig.Run(20);
        Assert.InRange(rig.Engine.Cylinder.GaugeBar, 1.8, 2.2);
    }

    [Fact]
    public void ABailShrinksWithAPartialRelease()
    {
        // Realistic Firing's rule: a partial release takes the bail down with it, so applying again applies the engine.
        var rig = new Rig(5, BrakingMode.Passenger);
        rig.Service(1.2);
        rig.Run(20);
        rig.Engine.BailOff();
        rig.Service(0.6);
        rig.Run(20);
        rig.Service(1.2);
        rig.Run(20);
        Assert.InRange(rig.Engine.Cylinder.GaugeBar, 1.3, 1.7);
    }

    [Fact]
    public void ALineDoesntSleepThroughAZeroLengthFrame()
    {
        var rig = new Rig(3);
        rig.Run(120);
        rig.Line.Advance(0);
        Assert.True(double.IsPositiveInfinity(rig.Line.ChangeRate));
    }

    [Fact]
    public void TheIndependentReleasesQuicklyAndTheAutomaticAtThePassengerTime()
    {
        var rig = new Rig(0);
        rig.Engine.Independent = 1;
        rig.RunUntil(() => rig.Engine.Cylinder.GaugeBar > 3.79, 30);
        rig.Engine.Independent = 0;
        var independent = rig.RunUntil(() => rig.Engine.Cylinder.GaugeBar < 0.4, 60);
        Assert.InRange(independent, 3, 5);

        // The automatic brake follows the distributor's pilot: UIC 543's Passenger release, 15–20 s from full to 0.4 bar.
        rig.Service(1.5);
        rig.RunUntil(() => rig.Engine.Cylinder.GaugeBar > 3.6, 30);
        rig.Run(10);
        rig.Valve.Position = DriverValvePosition.Running;
        var automatic = rig.RunUntil(() => rig.Engine.Cylinder.GaugeBar < 0.4, 60);
        Assert.InRange(automatic, 15, 20);
    }

    [Fact]
    public void ABailOverAPartialReleaseDoesntApplyTheEngineAgain()
    {
        // The pilot comes down slower than the relay: the bail must come down with it, not with the demand.
        var rig = new Rig(5, BrakingMode.Passenger);
        rig.Service(1.5);
        rig.Run(20);
        rig.Engine.BailOff();
        rig.Run(10);
        rig.Service(0.6);
        var reapplied = rig.RunUntil(() => rig.Engine.Cylinder.GaugeBar > 0.1, 40);
        Assert.True(double.IsNaN(reapplied), $"the engine applied again {reapplied:F1} s into the partial release");
        rig.Valve.Position = DriverValvePosition.Running;
        reapplied = rig.RunUntil(() => rig.Engine.Cylinder.GaugeBar > 0.1, 60);
        Assert.True(double.IsNaN(reapplied), $"the engine applied again {reapplied:F1} s into the release");
    }

    [Fact]
    public void NothingToBailWhenReleased()
    {
        var rig = new Rig(1);
        rig.Run(5);
        Assert.Equal(0, rig.Engine.BailOff());
    }
}
