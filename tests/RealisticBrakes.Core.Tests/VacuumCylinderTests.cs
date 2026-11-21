using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

 public class VacuumCylinderTests
{
    private const double Step = 0.004;

    private static double Inches(Chamber c) => Vacuum.ToInches(c.Pressure);

    /// <summary>Runs <paramref name="car"/> for <paramref name="seconds"/> against a pipe held at <paramref name="inches"/>.</summary>
    private static void Hold(VacuumCylinder car, double inches, double seconds)
    {
        var pipe = new Chamber(1.0, Vacuum.FromInches(inches));
        for (var t = 0.0; t < seconds; t += Step)
        {
            pipe.Pressure = Vacuum.FromInches(inches);
            car.Step(pipe, Step);
        }
    }

    private static VacuumCylinder Charged(VacuumCylinderSpec? spec = null)
    {
        var car = new VacuumCylinder(spec);
        car.ChargeTo(Vacuum.FromInches(21));
        return car;
    }

    [Fact]
    public void InchesOfVacuumReadFromTheModelsAtmosphere()
    {
        Assert.Equal(0.2889, Air.ToAbsoluteBar(Vacuum.FromInches(21)), 3);
        Assert.Equal(21, Vacuum.ToInches(Vacuum.FromInches(21)), 9);
    }

    [Fact]
    public void TheStockFieldsReadAVacuumTrainAsAnAirOne()
    {
        Assert.Equal(6, Air.ToAbsoluteBar(Vacuum.AirEquivalent(Vacuum.FromInches(21))), 6);
        Assert.Equal(1, Air.ToAbsoluteBar(Vacuum.AirEquivalent(Air.Atmosphere)), 6);
        Assert.Equal(3.5, Air.ToAbsoluteBar(Vacuum.AirEquivalent(Vacuum.FromInches(10.5))), 6);
        Assert.Equal(12.0, Vacuum.ToInches(Vacuum.FromAirEquivalent(Vacuum.AirEquivalent(Vacuum.FromInches(12)))), 6);
        // A European train's 6 bar is 21 in; a pipe vented below the atmosphere reads as none.
        Assert.Equal(21, Vacuum.ToInches(Vacuum.FromAirEquivalent(Air.FromAbsoluteBar(6))), 6);
        Assert.Equal(0, Vacuum.ToInches(Vacuum.FromAirEquivalent(Air.FromAbsoluteBar(0.5))), 6);
    }

    [Fact]
    public void ACoachsTwoCylindersBrakeAsOne()
    {
        var coach = new VacuumCylinder(VacuumCylinderSpec.Coach());
        coach.ChargeTo(Vacuum.FromInches(21));
        Hold(coach, 0, 10);
        Assert.InRange(coach.ForceShare, 0.97, 1.03);
        // Two 18 in cylinders pull twice one's.
        var one = Charged();
        Hold(one, 0, 10);
        Assert.InRange(coach.Force / one.Force, 1.95, 2.05);
    }

    [Fact]
    public void CreatingVacuumEvacuatesBothSidesAndTheBrakeStaysOff()
    {
        var car = new VacuumCylinder();
        car.ChargeTo(Air.Atmosphere);
        Hold(car, 21, 60);
        // The ball valve leaves the chamber its lift behind the pipe: 18 mm of mercury (Mohan 1964).
        Assert.InRange(Inches(car.Chamber), 20.2, 20.35);
        Assert.False(car.Applied);
        Assert.Equal(0, car.Force);
        Assert.Equal(0, car.Cylinder.GaugeBar, 6);
    }

    [Fact]
    public void DestroyingTheVacuumGivesTheFullForceAndTheStrokeCompressesTheChamber()
    {
        var car = Charged();
        Hold(car, 0, 10);
        Assert.Equal(car.Spec.Stroke, car.Travel, 6);
        // Roberts 1966: 20 in falls to 17½ in at a 4½ in stroke; the 5 in stroke here, from 20.3 in.
        Assert.InRange(Inches(car.Chamber), 17.3, 17.9);
        Assert.InRange(car.ForceShare, 0.97, 1.03);
        // An 18 in combined cylinder pulls about 2,000 to 2,400 lb (Marsh 1952, Fig. 2; Arnold 1934, Fig. 7).
        Assert.InRange(car.Force / 4.448, 1900, 2400);
        // The game's braking factor reads it as full.
        Assert.InRange(BrakeRigging.StockBrakingFactor(car.Cylinder.GaugeBar * Air.Bar), 0.97, 1.0);
    }

    [Fact]
    public void ApplicationAndReleaseAreBothGraduated()
    {
        var car = Charged();
        Hold(car, 10, 10);
        var half = car.ForceShare;
        Assert.InRange(half, 0.3, 0.55);
        Hold(car, 5, 10);
        var more = car.ForceShare;
        Assert.True(more > half + 0.1, $"{more} after {half}");
        Hold(car, 13, 10);
        var less = car.ForceShare;
        Assert.InRange(less, 0.05, half - 0.05);
        Assert.True(car.Applied);
        Hold(car, 21, 30);
        Assert.False(car.Applied);
        Assert.Equal(0, car.ForceShare);
    }

    [Fact]
    public void RepeatedApplicationsDontWearTheBrakeDown()
    {
        var car = Charged();
        for (var i = 0; i < 5; i++)
        {
            Hold(car, 0, 10);
            Hold(car, 21, 30);
        }
        Hold(car, 0, 10);
        Assert.InRange(car.ForceShare, 0.97, 1.03);
    }

    [Fact]
    public void AHeldCordReleasesABogieWagonInAboutTenSeconds()
    {
        var car = Charged(VacuumCylinderSpec.Bogie());
        Hold(car, 0, 10);
        Assert.True(car.ForceShare > 0.95);
        car.ReleaseHeld = true;
        var pipe = new Chamber(1.0);
        var t = 0.0;
        while (car.Travel > 0 && t < 60)
        {
            car.Step(pipe, Step);
            t += Step;
        }
        // Gresham 1928: one person released a 21 in bogie wagon in 22 s, then in 10 s with an improved valve.
        Assert.InRange(t, 8, 12);
    }

    [Fact]
    public void AShortPullReleasesTheCarAndItStaysReleased()
    {
        var car = Charged(VacuumCylinderSpec.Bogie());
        Hold(car, 0, 10);
        car.PullRelease();
        Hold(car, 0, 30);
        Assert.False(car.Applied);
        Assert.Equal(0, car.ForceShare);
        Hold(car, 21, 60);
        Hold(car, 0, 10);
        Assert.InRange(car.ForceShare, 0.97, 1.03);
    }

    [Fact]
    public void ACarChargedHigherThanItsEngineDragsUntilItsCordIsPulled()
    {
        // LMS 1946: "the engine previously attached ... created more than 21 in": the brakes stay on until each release valve is pulled.
        var car = new VacuumCylinder();
        car.ChargeTo(Vacuum.FromInches(25));
        Hold(car, 21, 30);
        // The piston rides at its stroke and the blocks drag: about a twentieth of the force.
        Assert.True(car.Applied);
        Assert.InRange(car.ForceShare, 0.02, 0.1);
        car.PullRelease();
        Hold(car, 21, 30);
        Assert.False(car.Applied);
        Assert.Equal(0, car.ForceShare);
    }

    [Fact]
    public void TheDirectAdmissionValveFillsTheCylinderFromTheAirNotThePipe()
    {
        double Climb(bool fitted)
        {
            var car = Charged(new VacuumCylinderSpec { DirectAdmission = fitted });
            var pipe = new Chamber(0.03, Vacuum.FromInches(10));
            for (var t = 0.0; t < 5; t += Step)
                car.Step(pipe, Step);
            Assert.True(car.Applied);
            return Inches(pipe) - 10;
        }
        var with = Climb(true);
        var without = Climb(false);
        // With DA only the last of its 1 to 2 in lag comes from the pipe (Sheffield 1979).
        Assert.InRange(with, 0, 1.0);
        Assert.True(without > 3 * with + 1, $"{without} without, {with} with");
    }

    [Theory]
    [InlineData(21)]
    [InlineData(15)]
    [InlineData(10)]
    [InlineData(5)]
    [InlineData(0)]
    public void ARestoredCarStandsAsItStood(double inches)
    {
        var car = Charged();
        Hold(car, inches, 60);
        var again = new VacuumCylinder(new VacuumCylinderSpec());
        again.Restore(car.Chamber.Pressure, car.Underside.Pressure);
        Assert.Equal(Inches(car.Chamber), Inches(again.Chamber), 3);
        Assert.Equal(Inches(car.Underside), Inches(again.Underside), 3);
        Assert.Equal(car.Travel, again.Travel, 4);
        Assert.Equal(car.ForceShare, again.ForceShare, 3);
    }

    [Fact]
    public void VacuumReadInchesReadsChargedAndAppliedCarPartsWithoutRegardToCase()
    {
        var pipe = PipeGeometry.VacuumTrainPipe();
        var brake = new CarBrake(15, pipe, new VacuumCylinder(VacuumCylinderSpec.Bogie()));
        brake.ChargeTo(Vacuum.FromInches(21));

        Assert.Equal(21, Vacuum.ReadInches(brake, "BrakePipe"), 6);
        Assert.Equal(21, Vacuum.ReadInches(brake, "brakepipe"), 6);
        var chamber = Vacuum.ReadInches(brake, "VacuumChamber");
        Assert.InRange(chamber, 20.2, 20.35);
        Assert.Equal(chamber, Vacuum.ReadInches(brake, "CHAMBER"), 6);
        Assert.Equal(chamber, Vacuum.ReadInches(brake, "auxiliaryreservoir"), 6);
        Assert.Equal(21, Vacuum.ReadInches(brake, "Underside"), 6);
        Assert.Equal(0, Vacuum.ReadInches(brake, "BrakeCylinder"), 6);

        Hold(brake.VacuumCylinder!, 0, 10);
        brake.PipeSection.Pressure = Air.Atmosphere;
        Assert.Equal(0, Vacuum.ReadInches(brake, "BrakePipe"), 6);
        var appliedChamber = Vacuum.ReadInches(brake, "VacuumChamber");
        var appliedUnderside = Vacuum.ReadInches(brake, "underside");
        Assert.InRange(appliedChamber, 17.3, 18.0);
        // With DA fitted, the underside closes at DirectAdmissionLag (0.25 inHg) ahead of the pipe.
        Assert.InRange(appliedUnderside, 0, 0.3);
        Assert.Equal(appliedChamber - appliedUnderside, Vacuum.ReadInches(brake, "brakecylinder"), 6);
        Assert.InRange(Vacuum.ReadInches(brake, "brakecylinder"), 17.0, 18.0);
    }

    [Fact]
    public void VacuumReadInchesReturnsNaNForMissingPartsAndEnginesWithoutVacuumCylinder()
    {
        var pipe = PipeGeometry.VacuumTrainPipe();
        var engine = new CarBrake(20, pipe, vacuumEngine: new VacuumEngine(VacuumEngineSpec.Steam()));
        engine.ChargeTo(Vacuum.FromInches(21));
        Assert.Equal(21, Vacuum.ReadInches(engine, "BrakePipe"), 6);
        Assert.True(double.IsNaN(Vacuum.ReadInches(engine, "VacuumChamber")));
        Assert.True(double.IsNaN(Vacuum.ReadInches(engine, "Underside")));
        Assert.True(double.IsNaN(Vacuum.ReadInches(engine, "BrakeCylinder")));

        var bare = new CarBrake(12, pipe);
        bare.ChargeTo(Vacuum.FromInches(21));
        Assert.Equal(21, Vacuum.ReadInches(bare, "BrakePipe"), 6);
        Assert.True(double.IsNaN(Vacuum.ReadInches(bare, "Chamber")));

        Assert.True(double.IsNaN(Vacuum.ReadInches(null!, "BrakePipe")));
        Assert.True(double.IsNaN(Vacuum.ReadInches(engine, null!)));
        Assert.True(double.IsNaN(Vacuum.ReadInches(engine, "")));
        Assert.True(double.IsNaN(Vacuum.ReadInches(engine, "MainReservoir")));
    }

    [Fact]
    public void OnlyVacuumDieselsCanBailOffOnVacuumEquipment()
    {
        var pipe = PipeGeometry.VacuumTrainPipe();
        var diesel = new CarBrake(20, pipe, vacuumEngine: new VacuumEngine(VacuumEngineSpec.Diesel()));
        var steam = new CarBrake(20, pipe, vacuumEngine: new VacuumEngine(VacuumEngineSpec.Steam()));
        var wagon = new CarBrake(15, pipe, new VacuumCylinder(VacuumCylinderSpec.Bogie()));
        var bare = new CarBrake(12, pipe);

        Assert.True(diesel.CanBail);
        Assert.False(steam.CanBail);
        Assert.False(wagon.CanBail);
        Assert.False(bare.CanBail);
    }

    [Fact]
    public void AnIsolatedCylinderIgnoresThePipeAndGivesNoForce()
    {
        var car = Charged();
        var chamberBefore = Inches(car.Chamber);
        car.Isolated = true;
        Hold(car, 0, 10);
        Assert.Equal(0, car.Force);
        Assert.Equal(0, car.ForceShare);
        Assert.Equal(0, car.Cylinder.GaugeBar, 6);
        Assert.Equal(0, car.PipeDraw);
        Assert.Equal(chamberBefore, Inches(car.Chamber), 6);
    }

    [Fact]
    public void IsolatingAndCuttingInWorksAcrossAppliedAndReleasedStates()
    {
        // Isolate while applied: force drops to zero immediately.
        var car = Charged();
        Hold(car, 0, 10);
        Assert.InRange(car.ForceShare, 0.97, 1.03);
        car.Isolated = true;
        Hold(car, 0, 1);
        Assert.Equal(0, car.Force);
        Assert.Equal(0, car.ForceShare);

        // Cut back in against an atmospheric pipe: brake force returns.
        car.Isolated = false;
        Hold(car, 0, 5);
        Assert.InRange(car.ForceShare, 0.97, 1.03);

        // Cut back in against a charged 21 in pipe: releases cleanly.
        car.Isolated = true;
        Hold(car, 21, 2);
        car.Isolated = false;
        Hold(car, 21, 30);
        Assert.False(car.Applied);
        Assert.Equal(0, car.ForceShare);

        // Isolated while charged, pipe drops to 10 in, then cut in: applies to graduated force cleanly.
        var cutIn = Charged();
        cutIn.Isolated = true;
        Hold(cutIn, 10, 5);
        Assert.Equal(0, cutIn.ForceShare);
        cutIn.Isolated = false;
        Hold(cutIn, 10, 10);
        Assert.True(cutIn.Applied);
        Assert.InRange(cutIn.ForceShare, 0.3, 0.55);
    }

    [Fact]
    public void RestoreHandlesReleasedAppliedEmptyAndIsolatedStates()
    {
        // Released: underside within PistonWeight of chamber leaves piston down and force 0.
        var released = new VacuumCylinder();
        released.Restore(Vacuum.FromInches(20.3), Vacuum.FromInches(21.0));
        Assert.False(released.Applied);
        Assert.Equal(0, released.Travel, 6);
        Assert.Equal(0, released.Force);
        Assert.Equal(0, released.ForceShare);

        // Applied on empty load: scales ForceShare by empty share.
        var empty = new VacuumCylinder();
        empty.SetEmptyCylinder(new BrakeRigging(24000, 86000).EmptyCylinder);
        empty.Load = LoadSetting.Empty;
        empty.Restore(Vacuum.FromInches(17.6), Air.Atmosphere);
        Assert.True(empty.Applied);
        Assert.Equal(empty.Spec.Stroke, empty.Travel, 6);
        Assert.InRange(empty.ForceShare, 0.25, 0.45);

        // Isolated: Restore still yields zero force while isolated.
        var isolated = new VacuumCylinder { Isolated = true };
        isolated.Restore(Vacuum.FromInches(17.6), Air.Atmosphere);
        Assert.Equal(0, isolated.Force);
        Assert.Equal(0, isolated.ForceShare);
    }
}
