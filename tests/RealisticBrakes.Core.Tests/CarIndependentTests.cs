using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

 public class CarIndependentTests
{
    private static Rig Train(bool american) => new(3, BrakingMode.Passenger, american: american);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ItAppliesTheCarFromItsOwnReservoirAndLetsItGo(bool american)
    {
        var rig = Train(american);
        rig.Run(30);
        var car = rig.Cars[2];
        car.Valve!.IndependentCylinder = 2.0 * Air.Bar;
        rig.Run(20);
        Assert.InRange(car.CylinderGauge / Air.Bar, 1.9, 2.05);
        Assert.InRange(rig.Cars[1].CylinderGauge / Air.Bar, 0, 0.05);
        Assert.InRange(rig.Cars[3].CylinderGauge / Air.Bar, 0, 0.05);
        Assert.False(car.Valve.Applied, "the car's valve stays released: the train brake didn't apply it");
        car.Valve.IndependentCylinder = 0;
        rig.Run(30);
        Assert.InRange(car.CylinderGauge / Air.Bar, 0, 0.05);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ATrainApplicationBeyondItGoesOnAndReleasesBackToIt(bool american)
    {
        var rig = Train(american);
        rig.Run(30);
        var car = rig.Cars[2];
        car.Valve!.IndependentCylinder = 1.0 * Air.Bar;
        rig.Run(20);
        rig.Service(1.5);
        rig.Run(40);
        Assert.True(car.CylinderGauge > 3.0 * Air.Bar, $"full service left the car at {car.CylinderGauge / Air.Bar:F2} bar");
        rig.Valve.Position = DriverValvePosition.Running;
        rig.Run(120);
        Assert.InRange(car.CylinderGauge / Air.Bar, 0.9, 1.05);
        Assert.InRange(rig.Cars[1].CylinderGauge / Air.Bar, 0, 0.05);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithItsReservoirEmptyItDoesNothing(bool american)
    {
        // Off air (the engine's valve cut out, the train never charged), the car has nothing to fill its cylinder with.
        var rig = new Rig(3, BrakingMode.Passenger, charged: false, american: american);
        rig.Valve.CutIn = false;
        var car = rig.Cars[2];
        car.Valve!.IndependentCylinder = 2.0 * Air.Bar;
        rig.Run(20);
        Assert.InRange(car.CylinderGauge / Air.Bar, 0, 0.05);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StandingAloneItSpendsTheReservoirsAir(bool american)
    {
        // A car charged and left alone: each application takes air the car can't get back.
        var rig = Train(american);
        rig.Run(30);
        var car = rig.Cars[2];
        var before = car.Valve!.AuxiliaryReservoir.Pressure;
        car.Valve.IndependentCylinder = 3.0 * Air.Bar;
        rig.Valve.CutIn = false;
        rig.Run(20);
        Assert.True(car.Valve.AuxiliaryReservoir.Pressure < before - 0.1 * Air.Bar, "the auxiliary reservoir gave the cylinder its air");
    }
}
