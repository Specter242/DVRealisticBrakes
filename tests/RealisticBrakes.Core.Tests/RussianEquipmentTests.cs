using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

 public class RussianEquipmentTests
{
    private static double Bar(Chamber c) => Air.ToGaugeBar(c.Pressure);

    private static Rig Engine(int cars = 0, double charging = 5.0) =>
        new(cars, engine: LocomotiveBrakeSpec.Russian254(), driver: DriverBrakeValveSpec.Russian394(charging * Air.Bar));

    [Fact]
    public void InTrainTheEqualizingReservoirChargesFromNothingInThirtyToFortySeconds()
    {
        var rig = Engine();
        rig.Valve.EqualizingReservoir.Pressure = Air.Atmosphere;
        rig.Valve.Position = DriverValvePosition.Running;
        Assert.InRange(rig.RunUntil(() => Bar(rig.Valve.EqualizingReservoir) >= 4.99, 120), 30, 40);
    }

    [Fact]
    public void InReleaseTheEqualizingReservoirChargesInFourSecondsAtMost()
    {
        var rig = Engine();
        rig.Valve.EqualizingReservoir.Pressure = Air.Atmosphere;
        rig.Valve.Position = DriverValvePosition.Fill;
        Assert.InRange(rig.RunUntil(() => Bar(rig.Valve.EqualizingReservoir) >= 4.99, 30), 0, 4);
    }

    [Fact]
    public void ReleaseHasNoLimiterAndTrainLetsTheOverchargeDownByAFifthOfABarInEightyToAHundredAndTwentySeconds()
    {
        var rig = Engine(5);
        rig.Run(10);
        rig.Valve.Position = DriverValvePosition.Fill;
        rig.RunUntil(() => Bar(rig.Valve.EqualizingReservoir) >= 5.4, 30);
        Assert.True(Bar(rig.Valve.EqualizingReservoir) >= 5.4);
        rig.Valve.Position = DriverValvePosition.Running;
        var from = Bar(rig.Valve.EqualizingReservoir);
        Assert.InRange(rig.RunUntil(() => Bar(rig.Valve.EqualizingReservoir) <= from - 0.2, 300), 80, 120);
    }

    [Fact]
    public void TrainHoldsTheChargingPressureTheDriverSets()
    {
        foreach (var charging in new[] { 4.8, 5.5, 6.5 })
        {
            var rig = Engine(10, charging);
            rig.Run(60);
            Assert.InRange(Bar(rig.Cars[0].PipeSection), charging - 0.1, charging + 0.1);
            Assert.InRange(Bar(rig.Valve.EqualizingReservoir), charging - 0.1, charging + 0.1);
        }
    }

    [Fact]
    public void TheChargingPressureStaysBetweenFourPointEightAndSixPointFiveBar()
    {
        Assert.Equal(4.8 * Air.Bar, DriverBrakeValveSpec.Russian394(3.0 * Air.Bar).Running, 6);
        Assert.Equal(6.5 * Air.Bar, DriverBrakeValveSpec.Russian394(8.0 * Air.Bar).Running, 6);
    }

    [Fact]
    public void ServiceTakesTheEqualizingReservoirDownABarInFourToFiveSeconds()
    {
        var rig = Engine();
        rig.Run(5);
        rig.Valve.Position = DriverValvePosition.Apply;
        Assert.InRange(rig.RunUntil(() => Bar(rig.Valve.EqualizingReservoir) <= 4.0, 30), 4, 5);
    }

    [Fact]
    public void SlowServiceTakesItDownHalfABarInFifteenToTwentySeconds()
    {
        var rig = Engine();
        rig.Run(5);
        rig.Valve.Position = DriverValvePosition.SlowApply;
        Assert.InRange(rig.RunUntil(() => Bar(rig.Valve.EqualizingReservoir) <= 4.5, 60), 15, 20);
    }

    [Fact]
    public void EmergencyEmptiesThePipeAtTheEngineFromFiveToOneBarInThreeSecondsAtMost()
    {
        var rig = Engine(50);
        rig.Run(30);
        rig.Valve.Position = DriverValvePosition.Emergency;
        Assert.InRange(rig.RunUntil(() => Bar(rig.Cars[0].PipeSection) <= 1.0, 30), 0, 3);
    }

    [Fact]
    public void LapWithoutFeedLetsLeaksCreepAndLapWithFeedHoldsThePipeToTheReservoir()
    {
        // Leaks of about 0.25 bar a minute on a 100-car train.
        Rig Leaky()
        {
            var rig = Engine(100);
            rig.Run(60);
            foreach (var car in rig.Cars.Skip(1))
                car.PipeLeak = new Orifice(0.4);
            return rig;
        }
        var without = Leaky();
        without.Valve.Position = DriverValvePosition.LapWithoutFeed;
        var start = Bar(without.Cars[0].PipeSection);
        without.Run(60);
        Assert.InRange(start - Bar(without.Cars[0].PipeSection), 0.2, 0.35);
        Assert.InRange(Bar(without.Valve.EqualizingReservoir), 4.95, 5.05);

        var with = Leaky();
        with.Valve.Position = DriverValvePosition.Lap;
        with.Run(60);
        Assert.InRange(Bar(with.Cars[0].PipeSection), 4.9, 5.05);
    }

    [Fact]
    public void TrainMakesGoodLeaksOfAFifthOfABarAMinuteOnAHundredCarTrain()
    {
        var rig = Engine(100);
        rig.Run(60);
        foreach (var car in rig.Cars.Skip(1))
            car.PipeLeak = new Orifice(0.4);
        rig.Run(120);
        Assert.InRange(Bar(rig.Cars[0].PipeSection), 4.9, 5.1);
    }

    [Fact]
    public void TheHandlesStockTravelGivesIIToVIWithVA()
    {
        // Six notches at 0, 0.2, 0.4, 0.6, 0.8, and 1; I lies before them. Along the quadrant, release to emergency:
        // I, II, III, IV, VA, V, VI, so that from IV into braking the driver passes VA, then V, then VI (KirVeliky, Nexus,
        // 2026-11-21).
        Assert.Equal(DriverValvePosition.Running, DriverBrakeValveSpec.Matrosov394At(0));
        Assert.Equal(DriverValvePosition.LapWithoutFeed, DriverBrakeValveSpec.Matrosov394At(0.2));
        Assert.Equal(DriverValvePosition.Lap, DriverBrakeValveSpec.Matrosov394At(0.4));
        Assert.Equal(DriverValvePosition.SlowApply, DriverBrakeValveSpec.Matrosov394At(0.6));
        Assert.Equal(DriverValvePosition.Apply, DriverBrakeValveSpec.Matrosov394At(0.8));
        Assert.Equal(DriverValvePosition.Emergency, DriverBrakeValveSpec.Matrosov394At(1));
        Assert.True(DriverBrakeValveSpec.Russian394().Matrosov394);
        Assert.False(DriverBrakeValveSpec.Uic().Matrosov394);
    }

    [Fact]
    public void TheIndependentsSixNotchesReadAsItsTravel()
    {
        // I (bail-off) at 0 and II at 0.2 are released; III to VI at 0.4 to 1.
        Assert.True(LocomotiveBrakeSpec.Matrosov254InBailOff(0));
        Assert.False(LocomotiveBrakeSpec.Matrosov254InBailOff(0.2));
        Assert.Equal(0, LocomotiveBrakeSpec.Matrosov254Travel(0));
        Assert.Equal(0, LocomotiveBrakeSpec.Matrosov254Travel(0.2));
        Assert.Equal(0.25, LocomotiveBrakeSpec.Matrosov254Travel(0.4), 6);
        Assert.Equal(0.5, LocomotiveBrakeSpec.Matrosov254Travel(0.6), 6);
        Assert.Equal(0.75, LocomotiveBrakeSpec.Matrosov254Travel(0.8), 6);
        Assert.Equal(1, LocomotiveBrakeSpec.Matrosov254Travel(1), 6);
    }

    [Fact]
    public void TheCabTextNamesThePositionsByTheirNumbers()
    {
        var engine = new LocomotiveBrake(LocomotiveBrakeSpec.Russian254(), new DriverBrakeValve(DriverBrakeValveSpec.Russian394(5.5 * Air.Bar)));
        engine.DriverValve!.ChargeTo(Air.FromGaugeBar(5.5));
        var cases = new (DriverValvePosition Position, string Starts)[]
        {
            (DriverValvePosition.Fill, "Train brake: I, release and charging"),
            (DriverValvePosition.Running, "Train brake: II, train (charging pressure 5.5 bar"),
            (DriverValvePosition.LapWithoutFeed, "Train brake: III, lap without feed"),
            (DriverValvePosition.Lap, "Train brake: IV, lap with feed"),
            (DriverValvePosition.SlowApply, "Train brake: VA, slow service"),
            (DriverValvePosition.Apply, "Train brake: V, service"),
            (DriverValvePosition.Emergency, "Train brake: VI, emergency"),
        };
        foreach (var (position, starts) in cases)
        {
            engine.DriverValve.Position = position;
            var text = CabValveText.TrainBrake(engine, selfLapping: false);
            Assert.StartsWith(starts, text);
        }
    }

    [Fact]
    public void TheIndependentsPositionsGiveTheirPressures()
    {
        // II at 0, then III, IV, V, and VI at even steps of the handle's travel.
        var spec = LocomotiveBrakeSpec.Russian254();
        Assert.Equal(0, spec.IndependentFor(0));
        Assert.InRange(spec.IndependentFor(0.25) / Air.Bar, 1.0, 1.3);
        Assert.InRange(spec.IndependentFor(0.5) / Air.Bar, 1.7, 2.0);
        Assert.InRange(spec.IndependentFor(0.75) / Air.Bar, 2.7, 3.0);
        Assert.InRange(spec.IndependentFor(1) / Air.Bar, 3.8, 4.0);
    }

    [Fact]
    public void TheCabTextGivesTheIndependentsStepNotAStraightLine()
    {
        // In game, 2026-11-21 (job 3202): III's 1.15 bar read "1.0 bar (of 3.9)", the maximum times the travel.
        var engine = new LocomotiveBrake(LocomotiveBrakeSpec.Russian254(), new DriverBrakeValve(DriverBrakeValveSpec.Russian394()));
        foreach (var travel in new[] { 0.25, 0.5, 0.75, 1.0 })
        {
            engine.Independent = travel;
            var bar = (engine.Spec.IndependentFor(travel) / Air.Bar).ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            Assert.StartsWith($"Independent brake: {bar} bar (of 3.9)", CabValveText.Independent(engine, clickToBail: true));
        }
    }

    [Fact]
    public void TheIndependentAppliesInFourSecondsAndReleasesInThirteenAtMost()
    {
        var rig = Engine();
        rig.Run(5);
        rig.Engine.Independent = 1;
        Assert.InRange(rig.RunUntil(() => rig.Engine.Cylinder.Pressure - Air.Atmosphere >= 3.5 * Air.Bar, 30), 0, 4);
        rig.Run(10);
        rig.Engine.Independent = 0;
        Assert.InRange(rig.RunUntil(() => rig.Engine.Cylinder.Pressure - Air.Atmosphere <= 0.5 * Air.Bar, 60), 0, 13);
    }

    [Fact]
    public void TheHigherOfTheTrainBrakeAndTheIndependentWins()
    {
        var rig = Engine(5);
        rig.Run(30);
        rig.Valve.Position = DriverValvePosition.Apply;
        rig.RunUntil(() => Bar(rig.Valve.EqualizingReservoir) <= 4.4, 30);
        rig.Valve.Position = DriverValvePosition.Lap;
        rig.Run(30);
        var automatic = rig.Engine.Cylinder.Pressure - Air.Atmosphere;
        Assert.True(automatic > 0.8 * Air.Bar, $"{automatic / Air.Bar:F2}");
        // V, over the train brake's application: the independent wins.
        rig.Engine.Independent = 0.75;
        rig.Run(10);
        Assert.InRange(rig.Engine.Cylinder.Pressure - Air.Atmosphere, 2.7 * Air.Bar, 3.0 * Air.Bar);
        // Back to II: the train brake's application again.
        rig.Engine.Independent = 0;
        rig.Run(15);
        Assert.InRange(rig.Engine.Cylinder.Pressure - Air.Atmosphere, automatic - 0.1 * Air.Bar, automatic + 0.1 * Air.Bar);
    }

    [Fact]
    public void BailingOffReleasesTheEngineUntilTheNextReduction()
    {
        var rig = Engine(5);
        rig.Run(30);
        rig.Valve.Position = DriverValvePosition.Apply;
        rig.RunUntil(() => Bar(rig.Valve.EqualizingReservoir) <= 4.4, 30);
        rig.Valve.Position = DriverValvePosition.Lap;
        rig.Run(30);
        rig.Engine.BailOff();
        rig.Run(10);
        Assert.InRange(rig.Engine.Cylinder.Pressure - Air.Atmosphere, 0, 0.05 * Air.Bar);
        Assert.True(rig.Rear.CylinderGauge > 0.5 * Air.Bar);
    }
}
