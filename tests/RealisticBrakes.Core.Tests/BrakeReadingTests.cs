using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

/// <summary>
/// The readings the public API gives other mods (<see cref="CarBrake.GaugePressure"/>, Api.Pressure): each part's
/// pressure in gauge bar, NaN where the car has no such part (Fuggschen, 2026-11-07: "a real API would be nice with
/// predictable values").
/// </summary>
public class BrakeReadingTests
{
    private static double Bar(CarBrake car, BrakePart part) => car.GaugePressure(part) / Air.Bar;

    [Fact]
    public void AChargedEuropeanTrainReadsItsPressures()
    {
        var rig = new Rig(3);
        rig.Run(30);
        var engine = rig.Cars[0];
        var car = rig.Front;
        Assert.InRange(Bar(car, BrakePart.BrakePipe), 4.95, 5.05);
        Assert.InRange(Bar(car, BrakePart.AuxiliaryReservoir), 4.95, 5.05);
        Assert.InRange(Bar(car, BrakePart.ControlReservoir), 4.95, 5.05);
        Assert.InRange(Bar(car, BrakePart.BrakeCylinder), 0, 0.01);
        Assert.True(double.IsNaN(car.GaugePressure(BrakePart.EmergencyReservoir)), "a European car has no emergency reservoir");
        Assert.True(double.IsNaN(car.GaugePressure(BrakePart.MainReservoir)), "a car has no main reservoir");
        Assert.True(double.IsNaN(car.GaugePressure(BrakePart.EqualizingReservoir)), "a car has no equalizing reservoir");

        Assert.InRange(Bar(engine, BrakePart.MainReservoir), 7, 8.05);
        Assert.InRange(Bar(engine, BrakePart.EqualizingReservoir), 4.95, 5.05);
        Assert.InRange(Bar(engine, BrakePart.BrakePipe), 4.95, 5.05);
        Assert.InRange(Bar(engine, BrakePart.ControlReservoir), 4.95, 5.05);
        Assert.InRange(Bar(engine, BrakePart.AuxiliaryReservoir), 4.95, 5.05);
        Assert.True(double.IsNaN(engine.GaugePressure(BrakePart.EmergencyReservoir)));
    }

    [Fact]
    public void AnApplicationReadsInThePipeTheEqualizingReservoirAndTheCylinders()
    {
        var rig = new Rig(3);
        rig.Run(30);
        rig.Service(1.0);
        rig.Run(40);
        Assert.InRange(Bar(rig.Cars[0], BrakePart.EqualizingReservoir), 3.9, 4.1);
        Assert.InRange(Bar(rig.Front, BrakePart.BrakePipe), 3.9, 4.1);
        Assert.True(Bar(rig.Front, BrakePart.BrakeCylinder) > 1.5);
        Assert.True(Bar(rig.Cars[0], BrakePart.BrakeCylinder) > 1.5);
    }

    [Fact]
    public void AnAmericanCarReadsItsEmergencyReservoirAndNoControlReservoir()
    {
        var rig = new Rig(3, american: true);
        rig.Run(30);
        Assert.InRange(Bar(rig.Front, BrakePart.EmergencyReservoir), 4.9, 5.05);
        Assert.InRange(Bar(rig.Front, BrakePart.AuxiliaryReservoir), 4.9, 5.05);
        Assert.True(double.IsNaN(rig.Front.GaugePressure(BrakePart.ControlReservoir)), "an AB valve has no control reservoir");
    }

    [Theory]
    [InlineData("BrakePipe", BrakePart.BrakePipe)]
    [InlineData("brakecylinder", BrakePart.BrakeCylinder)]
    [InlineData("MAINRESERVOIR", BrakePart.MainReservoir)]
    public void APartIsNamedWithoutRegardToCase(string name, BrakePart expected)
    {
        Assert.True(BrakeParts.TryParse(name, out var part));
        Assert.Equal(expected, part);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Pipe")]
    [InlineData("3")]
    public void AnUnknownNameIsNoPart(string name) => Assert.False(BrakeParts.TryParse(name, out _));
}

/// <summary>
/// The car's settings the public API reads (Fuggschen, 2026-11-07: "What about Brake Mode and Cutout?"): a European
/// distributor's G/P mode, the car's isolating cock, and an engine's brake valve cut-out.
/// </summary>
public class BrakeSettingReadingTests
{
    [Fact]
    public void AEuropeanCarReadsItsModeAndItsCock()
    {
        var rig = new Rig(2, BrakingMode.Goods);
        var car = rig.Front;
        Assert.Equal(BrakingMode.Goods, car.BrakingMode);
        car.Distributor!.Mode = BrakingMode.Passenger;
        Assert.Equal(BrakingMode.Passenger, car.BrakingMode);
        Assert.False(car.CutOut);
        car.Valve!.Isolated = true;
        Assert.True(car.CutOut);
        Assert.Null(car.BrakeValveCutOut);
    }

    [Fact]
    public void AnAmericanCarHasNoModeButHasItsCock()
    {
        var rig = new Rig(2, american: true);
        Assert.Null(rig.Front.BrakingMode);
        Assert.False(rig.Front.CutOut);
        rig.Front.Valve!.Isolated = true;
        Assert.True(rig.Front.CutOut);
    }

    [Fact]
    public void AnEngineReadsItsBrakeValveCutOutApartFromItsOwnBrake()
    {
        var rig = new Rig(2);
        var engine = rig.Cars[0];
        Assert.Equal(false, engine.BrakeValveCutOut);
        rig.Valve.CutIn = false;
        Assert.Equal(true, engine.BrakeValveCutOut);
        Assert.False(engine.CutOut);
        Assert.Equal(BrakingMode.Passenger, engine.BrakingMode);
    }

    [Fact]
    public void ACarWithNoValveHasNeither()
    {
        var bare = new CarBrake(15, Rig.Geometry);
        Assert.Null(bare.BrakingMode);
        Assert.Null(bare.CutOut);
        Assert.Null(bare.BrakeValveCutOut);
    }
}
