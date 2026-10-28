using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

/// <summary>An American car's AB-type valve against a pipe held or ramped at set pressures.</summary>
public class AarControlValveTests
{
    private const double Dt = 0.002;

    private static (AarControlValve valve, Chamber pipe) Charged()
    {
        var valve = new AarControlValve();
        var pipe = new Chamber(1000, Air.FromGaugeBar(5));
        valve.ChargeTo(pipe.Pressure);
        return (valve, pipe);
    }

    private static void Hold(AarControlValve valve, Chamber pipe, double bar, double seconds)
    {
        for (var t = 0.0; t < seconds; t += Dt)
        {
            pipe.Pressure = Air.FromGaugeBar(bar);
            valve.Step(pipe, Dt);
        }
    }

    /// <summary>Lowers the pipe at a service rate, 0.3 bar/s, to <paramref name="bar"/>, then holds it.</summary>
    private static void Reduce(AarControlValve valve, Chamber pipe, double bar, double seconds)
    {
        var p = pipe.GaugeBar;
        for (var t = 0.0; t < seconds; t += Dt)
        {
            p = Math.Max(bar, p - 0.3 * Dt);
            pipe.Pressure = Air.FromGaugeBar(p);
            valve.Step(pipe, Dt);
        }
    }

    [Fact]
    public void FullServiceLevelsTheReservoirAndCylinder()
    {
        var (valve, pipe) = Charged();
        Reduce(valve, pipe, 3.5, 40);
        // From a 90 psi pipe the AB levels at 64 psi, about 71 %; at the game's 5 bar, about 3.6.
        Assert.InRange(valve.Cylinder.GaugeBar, 3.5, 3.8);
        Assert.Equal(valve.AuxiliaryReservoir.GaugeBar, valve.Cylinder.GaugeBar, 1);
        Assert.False(valve.Emergency);
        Assert.Equal(5, valve.EmergencyReservoir.GaugeBar, 2);
    }

    [Fact]
    public void AnEmergencyAddsTheEmergencyReservoir()
    {
        var (valve, pipe) = Charged();
        Hold(valve, pipe, 0, 10);
        Assert.True(valve.Emergency);
        // About a fifth more than full service.
        Assert.InRange(valve.Cylinder.GaugeBar, 4.15, 4.4);
    }

    [Fact]
    public void AServiceRateMakesNoEmergency()
    {
        var (valve, pipe) = Charged();
        Reduce(valve, pipe, 3.5, 20);
        Assert.True(valve.Applied);
        Assert.False(valve.Emergency);
    }

    [Fact]
    public void ASlowLeakDoesntApply()
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
    public void AnyReleaseReleasesInFull()
    {
        var (valve, pipe) = Charged();
        Reduce(valve, pipe, 3.5, 40);
        // Up only a little: unlike the European brake, the whole application goes.
        Hold(valve, pipe, 3.9, 60);
        Assert.False(valve.Applied);
        Assert.Equal(0, valve.Cylinder.GaugeBar, 2);
    }

    [Fact]
    public void TheHighPressureRetainerHoldsTwentyPsi()
    {
        var (valve, pipe) = Charged();
        valve.Retainer = Retainer.HighPressure;
        Reduce(valve, pipe, 3.5, 40);
        Hold(valve, pipe, 5, 60);
        Assert.False(valve.Applied);
        Assert.Equal(1.4, valve.Cylinder.GaugeBar, 2);
    }

    [Fact]
    public void TheVentValveClosesAfterAboutAMinute()
    {
        var (valve, pipe) = Charged();
        var t = 0.0;
        Hold(valve, pipe, 0, 1);
        while (valve.QuickActionChamber.Pressure > Air.Atmosphere + valve.Spec.VentHold && t < 300)
        {
            Hold(valve, pipe, 0, 0.5);
            t += 0.5;
        }
        Assert.InRange(t, 40, 90);
        // Recharged, the car releases.
        Hold(valve, pipe, 5, 120);
        Assert.False(valve.Applied);
        Assert.Equal(0, valve.Cylinder.GaugeBar, 2);
    }

    [Fact]
    public void TheBleedRodReleasesACarOffAir()
    {
        var (valve, pipe) = Charged();
        Hold(valve, pipe, 0, 90);
        var emergency = valve.EmergencyReservoir.GaugeBar;
        var auxiliary = valve.AuxiliaryReservoir.GaugeBar;
        valve.PullRelease();
        Hold(valve, pipe, 0, 60);
        Assert.True(valve.CylinderReleaseOpen);
        Assert.Equal(0, valve.Cylinder.GaugeBar, 2);
        Assert.Equal(emergency, valve.EmergencyReservoir.GaugeBar, 6);
        Assert.Equal(auxiliary, valve.AuxiliaryReservoir.GaugeBar, 6);
    }

    [Fact]
    public void ABledCarIgnoresAFurtherReductionUntilRecharged()
    {
        var (valve, pipe) = Charged();
        Reduce(valve, pipe, 4.3, 30);
        Assert.True(valve.Applied);
        valve.PullRelease();
        Hold(valve, pipe, 4.3, 30);
        Assert.Equal(0, valve.Cylinder.GaugeBar, 2);
        Reduce(valve, pipe, 3.6, 30);
        Assert.Equal(0, valve.Cylinder.GaugeBar, 2);
        // Recharged, the valve releases and the release valve resets; the next application applies.
        Hold(valve, pipe, 5, 120);
        Assert.False(valve.Applied);
        Assert.False(valve.CylinderReleaseOpen);
        Reduce(valve, pipe, 4.3, 30);
        Assert.True(valve.Cylinder.GaugeBar > 1);
    }

    [Fact]
    public void AHeldRodDrainsBothReservoirs()
    {
        var (valve, pipe) = Charged();
        Hold(valve, pipe, 0, 30);
        valve.PullRelease();
        valve.ReleaseHeld = true;
        Hold(valve, pipe, 0, 180);
        valve.ReleaseHeld = false;
        Assert.Equal(0, valve.Cylinder.GaugeBar, 2);
        Assert.True(valve.AuxiliaryReservoir.GaugeBar < 0.05);
        Assert.True(valve.EmergencyReservoir.GaugeBar < 0.05);
    }

    [Fact]
    public void TheEmptySettingGivesItsCylinderAtFullServiceAndHoldsAnEmergencyToIt()
    {
        // A hopper's empty setting: the game's braking factor of 24/86, 1.34 bar.
        var target = new BrakeRigging(24000, 86000).EmptyCylinder;
        var (empty, pipe) = Charged();
        empty.SetEmptyCylinder(target);
        empty.Load = LoadSetting.Empty;
        Reduce(empty, pipe, 3.5, 40);
        Assert.InRange(empty.Cylinder.GaugeBar, target / Air.Bar * 0.92, target / Air.Bar * 1.08);

        // An emergency on empty stays at the same share of the loaded emergency (the review found 3.2 bar, sliding the wheels).
        var (emergency, pipe2) = Charged();
        emergency.SetEmptyCylinder(target);
        emergency.Load = LoadSetting.Empty;
        Hold(emergency, pipe2, 0, 10);
        Assert.True(emergency.Emergency);
        Assert.InRange(emergency.Cylinder.GaugeBar, 1.2, 1.9);
    }

    [Fact]
    public void ACarBledSoonAfterAnEmergencyStaysReleased()
    {
        var (valve, pipe) = Charged();
        Hold(valve, pipe, 0, 5);
        Assert.True(valve.Emergency);
        valve.PullRelease();
        Hold(valve, pipe, 0, 60);
        Assert.Equal(0, valve.Cylinder.GaugeBar, 2);
        // Recharged, it releases as any car does.
        Hold(valve, pipe, 5, 180);
        Assert.False(valve.Applied);
        Assert.False(valve.CylinderReleaseOpen);
        Assert.Equal(0, valve.Cylinder.GaugeBar, 2);
    }
}

/// <summary>Trains of American cars.</summary>
public class AarTrainTests
{
    [Fact]
    public void AnEmergencyRunsDownTheTrainAtAbout900FeetASecond()
    {
        var rig = new Rig(50, american: true);
        rig.Valve.Position = DriverValvePosition.Emergency;
        var t = rig.RunUntil(() => rig.Rear.ControlValve!.Applied, 30);
        // Each car's vent valve carries it on: WABCO gives about 900 ft/s (275 m/s).
        Assert.InRange(rig.Length / t, 240, 310);
        rig.Run(5);
        Assert.All(rig.Cars.Skip(1), c => Assert.True(c.ControlValve!.Emergency));
    }

    [Theory]
    [InlineData(40, 9.1, 0.5)]
    [InlineData(40, 9.1, 1.5)]
    [InlineData(60, 10.9, 0.5)]
    [InlineData(60, 10.9, 1.5)]
    public void ServiceOnATrainOfShortCarsMakesNoEmergency(int cars, double length, double reduction)
    {
        // The stock short flatcar (9.1 m) and short tank (10.9 m): each car's quick service dips a short
        // pipe section, and with a 3 mm port the dips added up to an emergency (review, 2026-10-24).
        var rig = new Rig(cars, american: true, carLength: length);
        rig.Service(reduction);
        rig.Run(30);
        Assert.All(rig.Cars.Skip(1), c => Assert.False(c.ControlValve!.Emergency));
        Assert.All(rig.Cars.Skip(1), c => Assert.True(c.ControlValve!.Applied));
    }

    [Fact]
    public void TheAmericanValvesFirstNotchHoldsALongTrain()
    {
        // The game's first self-lap notch asks 0.225 bar; the 26-L makes its 0.45 bar minimum, and every car stays on.
        var engine = new LocomotiveBrake(new LocomotiveBrakeSpec { MainReservoirVolume = 2.0 }, new DriverBrakeValve(DriverBrakeValveSpec.American()));
        var cars = new List<CarBrake> { new(20, Rig.Geometry, locomotive: engine) };
        for (var i = 0; i < 35; i++)
            cars.Add(new CarBrake(14, Rig.Geometry, new AarControlValve()));
        foreach (var c in cars)
            c.ChargeTo(Air.FromGaugeBar(5));
        engine.MainReservoir.Pressure = Air.FromAbsoluteBar(9);
        var line = new BrakeLine(cars, Rig.Geometry);
        engine.DriverValve!.Position = DriverValvePosition.Service;
        engine.DriverValve.ServiceReduction = 0.225 * Air.Bar;
        for (var i = 0; i < 60 * 25; i++)
            line.Advance(1 / 60.0);
        Assert.All(cars.Skip(1), c => Assert.True(c.ControlValve!.Applied));
    }

    [Fact]
    public void FullServiceOnALongTrainMakesNoEmergency()
    {
        var rig = new Rig(50, american: true);
        rig.Service(1.5);
        rig.Run(60);
        Assert.All(rig.Cars.Skip(1), c => Assert.False(c.ControlValve!.Emergency));
        Assert.InRange(rig.Rear.CylinderGauge / Air.Bar, 3.3, 3.8);
    }

    [Fact]
    public void ReapplyingBeforeTheTrainRechargesBrakesWeaker()
    {
        var rig = new Rig(30, american: true);
        rig.Service(1.5);
        rig.Run(60);
        var first = rig.Rear.CylinderGauge / Air.Bar;
        rig.Valve.Position = DriverValvePosition.Running;
        // Released, but the reservoirs are still short when the driver applies again.
        rig.RunUntil(() => rig.Rear.CylinderGauge < 0.1 * Air.Bar, 120);
        rig.Run(5);
        rig.Service(1.5);
        rig.Run(60);
        var second = rig.Rear.CylinderGauge / Air.Bar;
        Assert.True(second < first * 0.9, $"second application {second:F2} bar against the first's {first:F2}");
    }

    [Fact]
    public void ALongTrainReleasesQuicklyButRechargesSlowly()
    {
        var rig = new Rig(50, american: true);
        rig.Service(1.5);
        rig.Run(60);
        rig.Valve.Position = DriverValvePosition.Running;
        var released = rig.RunUntil(() => rig.Rear.CylinderGauge < 0.4 * Air.Bar, 300);
        var recharged = rig.RunUntil(() => rig.Cars.Skip(1).All(c =>
            c.ControlValve!.AuxiliaryReservoir.GaugeBar > 4.9 && c.ControlValve.EmergencyReservoir.GaugeBar > 4.9), 900);
        Assert.InRange(released, 5, 60);
        Assert.InRange(recharged, 120, 600);
    }
}

public class AmericanDriverValveTests
{
    [Fact]
    public void ReleaseHeldTooLongOverchargesTheCarsAndRunningAppliesThem()
    {
        var engine = new LocomotiveBrake(new LocomotiveBrakeSpec { MainReservoirVolume = 2.0 }, new DriverBrakeValve(DriverBrakeValveSpec.American()));
        var g = Rig.Geometry;
        var cars = new List<CarBrake> { new(20, g, locomotive: engine) };
        for (var i = 0; i < 5; i++)
            cars.Add(new CarBrake(15, g, new AarControlValve()));
        foreach (var c in cars)
            c.ChargeTo(Air.FromGaugeBar(5));
        engine.MainReservoir.Pressure = Air.FromAbsoluteBar(9);
        var line = new BrakeLine(cars, g);
        engine.DriverValve!.Position = DriverValvePosition.Fill;
        for (var i = 0; i < 60 * 90; i++)
        {
            engine.CompressorFlow = engine.MainReservoir.Pressure < Air.FromAbsoluteBar(9) ? Rig.FreeAir(100) : 0;
            line.Advance(1 / 60.0);
        }
        Assert.True(cars[^1].ControlValve!.AuxiliaryReservoir.GaugeBar > 5.5, "the reservoirs should have charged over running");
        // No overcharge to let down on an American valve: running drops the pipe at once.
        Assert.Equal(0, engine.DriverValve.OverchargeLeft);
        engine.DriverValve.Position = DriverValvePosition.Running;
        for (var i = 0; i < 60 * 10; i++)
            line.Advance(1 / 60.0);
        Assert.Contains(cars.Skip(1), c => c.ControlValve!.Applied);
    }
}
