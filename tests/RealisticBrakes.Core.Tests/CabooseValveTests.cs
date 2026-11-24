using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

 public class CabooseValveTests
{
    private static double PipeBar(CarBrake car) => Air.ToGaugeBar(car.PipeSection.Pressure);

    private static double CylinderBar(CarBrake car) => car.CylinderGauge / Air.Bar;

    private static Rig Charged(bool american)
    {
        var rig = new Rig(10, BrakingMode.Goods, american: american);
        rig.Run(30);
        return rig;
    }

    [Fact]
    public void TheOpenAreaGrowsWithTheSquareOfTheTravel()
    {
        Assert.Equal(0, CarBrake.CabooseValveArea(0));
        Assert.Equal(0.25, CarBrake.CabooseValveArea(0.5), 6);
        Assert.Equal(1, CarBrake.CabooseValveArea(1));
        Assert.Equal(1, CarBrake.CabooseValveArea(3));
        Assert.Equal(0, CarBrake.CabooseValveArea(-1));
        Assert.Equal(0, CarBrake.CabooseValveArea(double.NaN));
    }

    [Fact]
    public void ClosedItLetsNothingOut()
    {
        var rig = Charged(american: true);
        rig.Run(10);
        Assert.All(rig.Cars, car => Assert.Equal(0, car.CabooseValveFlow));
        Assert.InRange(PipeBar(rig.Rear), 4.95, 5.05);
        Assert.All(rig.Cars.Skip(1), car => Assert.False(car.ControlValve!.Emergency));
    }

    [Fact]
    public void WideOpenAtTheRearItPutsAnAmericanTrainInEmergency()
    {
        var rig = Charged(american: true);
        rig.Rear.CabooseValve = 1;
        var t = rig.RunUntil(() => rig.Cars.Skip(1).All(c => c.ControlValve!.Emergency), 5);
        Assert.InRange(t, 0, 2);
        rig.Run(20);
        // The engineer's valve, left in running, can't hold the pipe against the vents.
        Assert.Equal(DriverValvePosition.Running, rig.Valve.Position);
        Assert.InRange(PipeBar(rig.Rear), 0, 0.1);
        Assert.InRange(PipeBar(rig.Front), 0, 0.3);
        Assert.All(rig.Cars.Skip(1), car => Assert.InRange(CylinderBar(car), 4.1, 4.5));
    }

    [Fact]
    public void OpenedALittleItAppliesFromTheRearInService()
    {
        var rig = Charged(american: true);
        rig.Rear.CabooseValve = 0.4;
        rig.Run(1);
        Assert.True(rig.Rear.CabooseValveFlow > 0.02, $"the valve blows {rig.Rear.CabooseValveFlow:F3} kg/s");
        rig.Run(29);
        Assert.All(rig.Cars.Skip(1), car => Assert.False(car.ControlValve!.Emergency));
        // Against the engineer's valve feeding in running, the pipe falls most at the rear, and the rear brakes hardest.
        Assert.InRange(PipeBar(rig.Rear), 3.8, 4.7);
        Assert.True(PipeBar(rig.Front) > PipeBar(rig.Rear) + 0.2);
        Assert.InRange(CylinderBar(rig.Rear), 1, 3);
        Assert.True(CylinderBar(rig.Rear) > CylinderBar(rig.Front));
    }

    [Fact]
    public void OpenedFurtherItBrakesHarder()
    {
        var applied = new List<double>();
        foreach (var travel in new[] { 0.3, 0.4, 0.5 })
        {
            var rig = Charged(american: true);
            rig.Rear.CabooseValve = travel;
            rig.Run(30);
            applied.Add(CylinderBar(rig.Rear));
        }
        Assert.True(applied[0] < applied[1] && applied[1] < applied[2], string.Join(", ", applied));
    }

    [Fact]
    public void ClosedAgainWithTheBrakeValveInRunningTheTrainReleases()
    {
        var rig = Charged(american: true);
        rig.Rear.CabooseValve = 1;
        rig.Run(20);
        rig.Rear.CabooseValve = 0;
        rig.Run(1);
        Assert.Equal(0, rig.Rear.CabooseValveFlow);
        rig.Run(179);
        Assert.InRange(PipeBar(rig.Rear), 4.8, 5.1);
        Assert.All(rig.Cars.Skip(1), car => Assert.InRange(CylinderBar(car), 0, 0.05));
    }

    [Fact]
    public void WideOpenItAppliesAEuropeanTrainInFull()
    {
        var rig = Charged(american: false);
        rig.Rear.CabooseValve = 1;
        rig.Run(30);
        Assert.InRange(PipeBar(rig.Rear), 0, 1);
        Assert.All(rig.Cars.Skip(1), car => Assert.InRange(CylinderBar(car), 3.7, 3.9));
        rig.Rear.CabooseValve = 0;
        rig.Run(120);
        Assert.All(rig.Cars.Skip(1), car => Assert.InRange(CylinderBar(car), 0, 0.05));
    }
}
