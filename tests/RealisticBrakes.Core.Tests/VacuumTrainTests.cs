using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

 public class VacuumTrainTests
{
    private const double FourWheelWagon = 1500 * 0.3048 / 70;

    private static double Inches(Chamber c) => Vacuum.ToInches(c.Pressure);

    /// <summary>Seconds until the rear car's pipe is at <paramref name="inches"/> or under.</summary>
    private static double RearDownTo(VacuumRig rig, double inches, double limit) =>
        rig.RunUntil(() => Inches(rig.Rear.PipeSection) <= inches, limit);

    [Fact]
    public void TheSmallEjectorHoldsWhatItHeldAgainstTheLeakHoles()
    {
        // Gresham 1925, Fig. 3: the 20 mm Dreadnought held about 22 in against a ¼ in hole. The relief valve set out of the way.
        var spec = VacuumEngineSpec.Steam();
        spec.ReliefSetting = Vacuum.FromInches(29);
        var rig = new VacuumRig(0, FourWheelWagon, engine: spec);
        rig.Cars[0].PipeLeak = new Orifice(0.25 * 25.4, 0.6);
        rig.Run(60);
        Assert.InRange(Inches(rig.Cars[0].PipeSection), 21.7, 22.3);
    }

    [Fact]
    public void TheEjectorsEmptyWickhamsTankInHisTimes()
    {
        var spec = VacuumEngineSpec.Steam();
        double Time(params VacuumPump[] pumps)
        {
            var tank = new Chamber(300000 * Math.Pow(0.0254, 3));
            var t = 0.0;
            while (Inches(tank) < 20 && t < 1000)
            {
                foreach (var pump in pumps)
                    pump.Draw(tank, 0.01);
                t += 0.01;
            }
            return t;
        }
        Assert.InRange(Time(spec.LargeEjector), 150, 180);
        Assert.InRange(Time(spec.LargeEjector, spec.SmallEjector), 95, 115);
    }

    [Fact]
    public void TheReliefValveHoldsTwentyOneInches()
    {
        var rig = new VacuumRig(10, FourWheelWagon, charged: false);
        rig.Engine.Position = VacuumValvePosition.Release;
        rig.Run(300);
        // A diaphragm relief valve holds within ¼ in (Hunter 1962).
        Assert.InRange(Inches(rig.Cars[0].PipeSection), 20.7, 21.3);
        Assert.InRange(Inches(rig.Rear.PipeSection), 20.5, 21.3);
        Assert.All(rig.Cars.Skip(1), c => Assert.False(c.VacuumCylinder!.Applied));
    }

    [Fact]
    public void WithDirectAdmissionALongTrainsRearIsOnInAboutHalfAMinute()
    {
        // Roberts 1966, Fig. 2: 66 wagons (1,390 ft) with DA: the rear begins to fall at about 5 s and is at 0 by about 33 s.
        var rig = new VacuumRig(66, 1390 * 0.3048 / 66);
        rig.Engine.Position = VacuumValvePosition.Emergency;
        Assert.InRange(RearDownTo(rig, 20.5, 60), 1.5, 8);
        rig = new VacuumRig(66, 1390 * 0.3048 / 66);
        rig.Engine.Position = VacuumValvePosition.Emergency;
        Assert.InRange(RearDownTo(rig, 0.5, 120), 20, 40);
        rig.Run(10);
        Assert.InRange(rig.Rear.VacuumCylinder!.ForceShare, 0.95, 1.05);
    }

    [Fact]
    public void WithoutDirectAdmissionTheRearLagsFarBehind()
    {
        // Roberts 1966, Fig. 1: 70 wagons (1,500 ft) without DA: the rear's pipe is at about 10 in at about 45 s, and "for about
        // the first 20 seconds of the application the rear of the train is virtually unbraked".
        var rig = new VacuumRig(70, FourWheelWagon, directAdmission: false);
        rig.Engine.Position = VacuumValvePosition.Emergency;
        rig.Run(20);
        Assert.True(rig.Rear.VacuumCylinder!.ForceShare < 0.05, $"{rig.Rear.VacuumCylinder.ForceShare}");
        rig = new VacuumRig(70, FourWheelWagon, directAdmission: false);
        rig.Engine.Position = VacuumValvePosition.Emergency;
        Assert.InRange(RearDownTo(rig, 10, 120), 35, 60);
    }

    [Fact]
    public void TheTwoInchPipeHoldsBackALongTrainsRear()
    {
        // Hunter 1962: 2,000 ft without DA, the rear at 5 in after 75 s on a 2 in pipe.
        var rig = new VacuumRig(66, 2000 * 0.3048 / 66, directAdmission: false);
        rig.Engine.Position = VacuumValvePosition.Emergency;
        Assert.InRange(RearDownTo(rig, 5, 150), 60, 90);
    }

    [Fact]
    public void ALongTrainTakesMinutesToRelease()
    {
        var rig = new VacuumRig(70, FourWheelWagon);
        rig.Engine.Position = VacuumValvePosition.Emergency;
        rig.Run(150);
        rig.Engine.Position = VacuumValvePosition.Release;
        var off = rig.RunUntil(() => !rig.Rear.VacuumCylinder!.Applied, 600);
        Assert.InRange(off, 60, 180);
        var t = off + rig.RunUntil(() => Inches(rig.Rear.PipeSection) >= 18, 600);
        Assert.InRange(t, 100, 200);
    }

    [Fact]
    public void CreatingVacuumOnAColdTrainTakesMinutes()
    {
        // Arnold 1934, Fig. 1: 50 vehicles of 18 in cylinders (1,500 ft) to about 20 in at the rear: 165 s on two 15 mm cones,
        // 94 s on 30, 15, and 15 mm. The model's 30 and 20 mm Dreadnought, from Gresham's and Wickham's figures, takes longer.
        var rig = new VacuumRig(50, 1500 * 0.3048 / 50, charged: false);
        rig.Engine.Position = VacuumValvePosition.Release;
        var t = rig.RunUntil(() => Inches(rig.Rear.PipeSection) >= 20, 600);
        Assert.InRange(t, 165, 330);
        rig.Run(120);
        Assert.InRange(Inches(rig.Rear.VacuumCylinder!.Chamber), 19.5, 20.5);
    }

    [Fact]
    public void ALeakInLapCreepsTheBrakeOnAndRunningHoldsItOff()
    {
        VacuumRig Leaky(VacuumValvePosition position)
        {
            var rig = new VacuumRig(20, FourWheelWagon);
            rig.Rear.PipeLeak = new Orifice(0.125 * 25.4, 0.6);
            rig.Engine.Position = position;
            rig.Run(120);
            return rig;
        }
        var lapped = Leaky(VacuumValvePosition.Lap);
        Assert.True(lapped.Rear.VacuumCylinder!.ForceShare > 0.1, $"{lapped.Rear.VacuumCylinder.ForceShare}");
        var running = Leaky(VacuumValvePosition.Running);
        Assert.False(running.Rear.VacuumCylinder!.Applied);
    }

    [Fact]
    public void AHoseOffItsDummyAppliesTheTrain()
    {
        var rig = new VacuumRig(10, FourWheelWagon);
        rig.Line.Pipe.RearOpen = true;
        rig.Run(20);
        Assert.All(rig.Cars.Skip(1), c => Assert.True(c.VacuumCylinder!.ForceShare > 0.9, $"{c.VacuumCylinder.ForceShare}"));
    }

    [Fact]
    public void LapHoldsAGraduatedApplication()
    {
        // Lapped with the engine's end let down to 10 in, the pipe evens out along the train, and every car holds alike.
        var rig = new VacuumRig(10, FourWheelWagon);
        rig.Engine.Position = VacuumValvePosition.Apply;
        rig.RunUntil(() => Inches(rig.Cars[0].PipeSection) <= 10, 30);
        rig.Engine.Position = VacuumValvePosition.Lap;
        rig.Run(30);
        var share = rig.Rear.VacuumCylinder!.ForceShare;
        Assert.InRange(share, 0.1, 0.6);
        Assert.InRange(rig.Cars[1].VacuumCylinder!.ForceShare, share - 0.03, share + 0.03);
        rig.Run(60);
        Assert.InRange(rig.Rear.VacuumCylinder.ForceShare, share - 0.02, share + 0.02);
    }

    [Fact]
    public void ASelfLappingValveHoldsItsTarget()
    {
        var rig = new VacuumRig(10, FourWheelWagon, engine: VacuumEngineSpec.Diesel());
        rig.Engine.Position = VacuumValvePosition.Service;
        rig.Engine.Target = Vacuum.FromInches(12);
        rig.Run(60);
        Assert.InRange(Inches(rig.Cars[0].PipeSection), 11.6, 12.4);
        rig.Engine.Target = Vacuum.FromInches(16);
        rig.Run(90);
        Assert.InRange(Inches(rig.Cars[0].PipeSection), 15.6, 16.4);
    }

    [Fact]
    public void TheSteamBrakeFollowsThePipeAndTheIndependent()
    {
        var rig = new VacuumRig(5, FourWheelWagon);
        rig.Run(10);
        Assert.Equal(0, rig.Engine.BrakeShare, 3);
        rig.Engine.Position = VacuumValvePosition.Emergency;
        rig.Run(15);
        Assert.InRange(rig.Engine.SteamBrakePressure / 6894.757, 170, 181);
        Assert.InRange(BrakeRigging.StockBrakingFactor(rig.Cars[0].CylinderGauge), 0.95, 1.0);
        rig.Engine.Position = VacuumValvePosition.Release;
        rig.Run(120);
        Assert.True(rig.Engine.BrakeShare < 0.03, $"{rig.Engine.BrakeShare}");
        rig.Engine.Independent = 0.5;
        rig.Run(10);
        Assert.InRange(rig.Engine.BrakeShare, 0.48, 0.52);
        Assert.InRange(rig.Engine.SteamFlow * 3600 / 0.45359237, 1040, 1060);
    }

    [Fact]
    public void LowSteamWeakensTheEjectorsAndTheirSteam()
    {
        // Half the boiler's rated working: half the steam, and the pipe drawn down more slowly.
        double Time(double supply, out double steam)
        {
            var rig = new VacuumRig(10, FourWheelWagon, charged: false);
            rig.Engine.Supply = supply;
            rig.Engine.Position = VacuumValvePosition.Release;
            var t = rig.RunUntil(() => Inches(rig.Rear.PipeSection) >= 18, 600);
            steam = rig.Engine.SteamFlow;
            return t;
        }
        var full = Time(1, out var fullSteam);
        var half = Time(0.5, out var halfSteam);
        Assert.InRange(half / full, 1.6, 2.3);
        Assert.InRange(halfSteam / fullSteam, 0.45, 0.55);
    }

    [Fact]
    public void FillingTheSteamBrakeTakesALittleSteam()
    {
        var rig = new VacuumRig(0, FourWheelWagon);
        rig.Engine.PumpOn = false;
        rig.Engine.Independent = 1;
        var steam = 0.0;
        rig.RunUntil(() => { steam += rig.Engine.SteamFlow * VacuumRig.Frame; return false; }, 10);
        // 30 L of cylinders at about 180 psi: about a fifth of a kilogram.
        Assert.InRange(steam, 0.15, 0.25);
    }

    [Fact]
    public void WithItsSteamValveShutTheSmallEjectorDrawsNothing()
    {
        var rig = new VacuumRig(5, FourWheelWagon, charged: false);
        rig.Engine.PumpOn = false;
        rig.Run(30);
        Assert.True(Inches(rig.Cars[0].PipeSection) < 0.1, $"{Inches(rig.Cars[0].PipeSection)} in");
        Assert.Equal(0, rig.Engine.SteamFlow, 6);
    }

    [Fact]
    public void TheLargeEjectorWorksFromTheHandleWithTheSmallShut()
    {
        double Time(bool small, out double steam)
        {
            var rig = new VacuumRig(5, FourWheelWagon, charged: false);
            rig.Engine.PumpOn = small;
            rig.Engine.Position = VacuumValvePosition.Release;
            var t = rig.RunUntil(() => Inches(rig.Rear.PipeSection) >= 18, 600);
            steam = rig.Engine.SteamFlow * 3600 / 0.45359237;
            return t;
        }
        var alone = Time(false, out var aloneSteam);
        var both = Time(true, out var bothSteam);
        Assert.False(double.IsNaN(alone), "the large ejector alone never reached 18 in");
        Assert.True(alone > both, $"alone {alone} s, both {both} s");
        // Its own 750 lb an hour, and the small's 300 with it.
        Assert.InRange(aloneSteam, 740, 760);
        Assert.InRange(bothSteam, 1040, 1060);
    }

    [Fact]
    public void ADieselsExhaustersStopWithItsEngineEvenInRelease()
    {
        var rig = new VacuumRig(5, FourWheelWagon, charged: false, engine: VacuumEngineSpec.Diesel());
        rig.Engine.PumpOn = false;
        rig.Engine.Position = VacuumValvePosition.Release;
        rig.Run(30);
        Assert.True(Inches(rig.Cars[0].PipeSection) < 0.1, $"{Inches(rig.Cars[0].PipeSection)} in");
    }

    [Fact]
    public void TheLineStaysAwakeUntilTheSteamBrakeHasSettled()
    {
        // A settled line sleeps; the steam brake moves on its own time, with the pipe standing still (audit 2026-11-09).
        const double settled = 1.0;
        var rig = new VacuumRig(0, FourWheelWagon);
        rig.Run(20);
        rig.Engine.Independent = 1;
        var slept = rig.RunUntil(() => rig.Line.ChangeRate < settled, 60);
        Assert.False(double.IsNaN(slept), "the line never settled");
        Assert.InRange(rig.Engine.BrakeShare, 0.999, 1.0);
        rig.Engine.Independent = 0;
        slept = rig.RunUntil(() => rig.Line.ChangeRate < settled, 60);
        Assert.False(double.IsNaN(slept), "the line never settled on release");
        Assert.InRange(rig.Engine.BrakeShare, 0, 0.001);
    }

    [Fact]
    public void ADieselsOwnBrakeFollowsThePipeInProportion()
    {
        // Marsh 1952: the pipe let down to 15 in puts the engine's brake on at about a quarter.
        var rig = new VacuumRig(5, FourWheelWagon, engine: VacuumEngineSpec.Diesel());
        rig.Engine.MainReservoir.Pressure = Air.FromAbsoluteBar(9);
        rig.Engine.Position = VacuumValvePosition.Service;
        rig.Engine.Target = Vacuum.FromInches(15);
        rig.Run(30);
        Assert.InRange(rig.Engine.BrakeShare, 0.24, 0.32);
    }

    [Fact]
    public void RunningHoldsTheReliefValvesBleedApartFromTheValvesOwnAir()
    {
        var rig = new VacuumRig(5, FourWheelWagon);
        rig.Run(30);
        Assert.True(rig.Engine.AdmitFlow > 0, "the relief valve isn't bleeding");
        Assert.Equal(0, rig.Engine.ValveAdmitFlow);
        rig.Engine.Position = VacuumValvePosition.Apply;
        rig.Run(0.5);
        Assert.True(rig.Engine.ValveAdmitFlow > 0);
    }

    [Fact]
    public void AVacuumTrainSettlesAfterApplicationAndReleaseAndWakesOnInputChange()
    {
        // Mirrors BrakeSets.Tick's sleep gate (BrakeSets.SettledRate = 1.0 Pa/s): once ChangeRate < SettledRate
        // with no changed input, Line.Advance is skipped until a control input changes and wakes the line.
        const double settledRate = 1.0;
        var rig = new VacuumRig(10, FourWheelWagon);

        bool TickWithSleepGate(bool inputChanged)
        {
            if (!inputChanged && rig.Line.ChangeRate < settledRate)
                return false;
            rig.Line.Advance(VacuumRig.Frame);
            return true;
        }

        void AssertSleepsWhenUnchanged()
        {
            var before = rig.Cars.SelectMany(c => c.Chambers).Select(c => c.Pressure).ToArray();
            for (var i = 0; i < 60; i++)
                Assert.False(TickWithSleepGate(inputChanged: false));
            Assert.Equal(before, rig.Cars.SelectMany(c => c.Chambers).Select(c => c.Pressure).ToArray());
        }

        rig.Engine.Position = VacuumValvePosition.Apply;
        rig.RunUntil(() => Inches(rig.Cars[0].PipeSection) <= 10, 30);
        rig.Engine.Position = VacuumValvePosition.Lap;
        var settledApplied = rig.RunUntil(() => rig.Line.ChangeRate < settledRate, 180);
        Assert.False(double.IsNaN(settledApplied), $"applied vacuum train still changing at {rig.Line.ChangeRate:F2} Pa/s");
        AssertSleepsWhenUnchanged();

        var vacuumBeforeRelease = Inches(rig.Cars[0].PipeSection);
        rig.Engine.Position = VacuumValvePosition.Release;
        Assert.True(TickWithSleepGate(inputChanged: true));
        Assert.True(rig.Line.ChangeRate > settledRate, $"expected wake on release, got {rig.Line.ChangeRate:F2} Pa/s");
        rig.Run(1);
        Assert.True(Inches(rig.Cars[0].PipeSection) > vacuumBeforeRelease + 0.5);

        rig.Run(60);
        rig.Engine.Position = VacuumValvePosition.Running;
        var settledReleased = rig.RunUntil(() => rig.Line.ChangeRate < settledRate, 300);
        Assert.False(double.IsNaN(settledReleased), $"released vacuum train still changing at {rig.Line.ChangeRate:F2} Pa/s");
        AssertSleepsWhenUnchanged();

        var vacuumBeforeApply = Inches(rig.Cars[0].PipeSection);
        rig.Engine.Position = VacuumValvePosition.Apply;
        Assert.True(TickWithSleepGate(inputChanged: true));
        Assert.True(rig.Line.ChangeRate > settledRate, $"expected wake on apply, got {rig.Line.ChangeRate:F2} Pa/s");
        rig.Run(1);
        Assert.True(Inches(rig.Cars[0].PipeSection) < vacuumBeforeApply - 0.5);
    }

    [Fact]
    public void AnIsolatedCarInAVacuumTrainPassesThePipeThroughAndBrakesWhenCutIn()
    {
        var rig = new VacuumRig(10, FourWheelWagon);
        var middle = rig.Cars[5];
        middle.VacuumCylinder!.Isolated = true;

        rig.Engine.Position = VacuumValvePosition.Emergency;
        rig.Run(25);
        // The isolated car stays unbraked while the pipe passes through to brake the rear car.
        Assert.Equal(0, middle.VacuumCylinder.Force);
        Assert.Equal(0, middle.VacuumCylinder.ForceShare);
        Assert.InRange(rig.Rear.VacuumCylinder!.ForceShare, 0.95, 1.05);

        // Cut back in while the pipe is down: the car applies with the train, then releases with it.
        middle.VacuumCylinder.Isolated = false;
        rig.Run(10);
        Assert.InRange(middle.VacuumCylinder.ForceShare, 0.95, 1.05);

        rig.Engine.Position = VacuumValvePosition.Release;
        var off = rig.RunUntil(() => rig.Cars.Skip(1).All(c => !c.VacuumCylinder!.Applied), 180);
        Assert.False(double.IsNaN(off), "train never released after cut-in");
    }

    [Fact]
    public void VacuumEngineRestoreOwnBrakeRestoresSteamAndDieselAndClampsRange()
    {
        var steam = new VacuumEngine(VacuumEngineSpec.Steam());
        steam.RestoreOwnBrake(0.6);
        Assert.Equal(0.6, steam.BrakeShare, 6);
        Assert.Equal(0.6 * steam.Spec.SteamBrakeMaximum, steam.SteamBrakePressure, 3);
        Assert.InRange(BrakeRigging.StockBrakingFactor(steam.Cylinder.GaugeBar * Air.Bar), 0.59, 0.61);

        steam.RestoreOwnBrake(-0.5);
        Assert.Equal(0, steam.BrakeShare);
        Assert.Equal(0, steam.SteamBrakePressure);
        Assert.Equal(Air.Atmosphere, steam.Cylinder.Pressure, 6);

        steam.RestoreOwnBrake(1.5);
        Assert.Equal(1, steam.BrakeShare);
        Assert.Equal(steam.Spec.SteamBrakeMaximum, steam.SteamBrakePressure, 3);

        var diesel = new VacuumEngine(VacuumEngineSpec.Diesel());
        diesel.RestoreOwnBrake(0.5);
        Assert.Equal(0.5, diesel.BrakeShare, 6);
        Assert.Equal(0.5 * diesel.Spec.AirBrakeMaximum, diesel.Cylinder.GaugeBar * Air.Bar, 3);

        diesel.RestoreOwnBrake(-1);
        Assert.Equal(0, diesel.BrakeShare);
        Assert.Equal(Air.Atmosphere, diesel.Cylinder.Pressure, 6);

        diesel.RestoreOwnBrake(2);
        Assert.Equal(1, diesel.BrakeShare);
        Assert.Equal(diesel.Spec.AirBrakeMaximum, diesel.Cylinder.GaugeBar * Air.Bar, 3);
    }

    [Fact]
    public void CoupledVacuumDieselsShareMainReservoirsThroughPipedCars()
    {
        var g = VacuumRig.Geometry;
        var a = new VacuumEngine(VacuumEngineSpec.Diesel());
        var b = new VacuumEngine(VacuumEngineSpec.Diesel());
        var cars = new List<CarBrake> { new(20, g, vacuumEngine: a), new(10, g), new(20, g, vacuumEngine: b) };
        foreach (var c in cars)
            c.ChargeTo(Vacuum.FromInches(21));
        a.MainReservoir.Pressure = Air.FromAbsoluteBar(9);
        b.MainReservoir.Pressure = Air.FromAbsoluteBar(6);
        var line = new BrakeLine(cars, g, new[] { true, true, true });
        for (var i = 0; i < 60 * 30; i++)
            line.Advance(VacuumRig.Frame);
        Assert.InRange(Math.Abs(a.MainReservoir.Pressure - b.MainReservoir.Pressure), 0, 0.01 * Air.Bar);

        // An unpiped car between them keeps their main reservoirs separate.
        b.MainReservoir.Pressure = Air.FromAbsoluteBar(6);
        var apart = new BrakeLine(cars, g, new[] { true, false, true });
        var before = a.MainReservoir.Pressure;
        apart.Advance(1);
        Assert.InRange(Math.Abs(before - a.MainReservoir.Pressure), 0, 1);

        var steam = new VacuumEngine(VacuumEngineSpec.Steam());
        var mixed = new List<CarBrake> { new(20, g, vacuumEngine: a), new(20, g, vacuumEngine: steam) };
        foreach (var c in mixed)
            c.ChargeTo(Vacuum.FromInches(21));
        a.MainReservoir.Pressure = Air.FromAbsoluteBar(9);
        steam.MainReservoir.Pressure = Air.Atmosphere;
        var mixedLine = new BrakeLine(mixed, g, new[] { true, true });
        mixedLine.Advance(5);
        Assert.Equal(Air.FromAbsoluteBar(9), a.MainReservoir.Pressure, 3);
        Assert.Equal(Air.Atmosphere, steam.MainReservoir.Pressure, 3);
    }
}

