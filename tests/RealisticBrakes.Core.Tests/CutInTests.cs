using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

/// <summary>A car cut out and cut back in with the train's brakes applied.</summary>
public class CutInTests
{
    [Fact]
    public void AnAmericanCarCutInDuringAnApplicationDoesNotTripAnEmergency()
    {
        var rig = new Rig(5, american: true);
        rig.Run(2);
        rig.Front.Valve!.Isolated = true;
        rig.Service(1.0);
        rig.Run(30);
        var pipe = Air.ToGaugeBar(rig.Rear.PipeSection.Pressure);
        Assert.InRange(pipe, 3.8, 4.2);

        // The game's lever is thrown at once; a brakeman opens the cock slowly, so the quick-action chamber, still at the
        // pressure it was cut out with, comes level with the pipe without opening the vent valve.
        rig.Front.Valve.Isolated = false;
        rig.Run(10);
        Assert.All(rig.Cars.Skip(1), car => Assert.False(car.ControlValve!.Emergency));
        Assert.InRange(Air.ToGaugeBar(rig.Rear.PipeSection.Pressure), pipe - 0.2, pipe + 0.2);
    }

    [Fact]
    public void AnAmericanCarsBleedPulledBeforeItWasCutOutIsForgotten()
    {
        var rig = new Rig(5, american: true);
        rig.Service(1.0);
        rig.Run(30);
        var valve = rig.Front.ControlValve!;
        valve.PullRelease();
        Assert.True(valve.CylinderReleaseOpen);
        valve.Isolated = true;
        rig.Run(5);
        valve.Isolated = false;
        rig.Run(30);
        Assert.False(valve.CylinderReleaseOpen);
    }

    [Fact]
    public void AEuropeanCarsBleedPulledWhileCutOutIsForgotten()
    {
        var rig = new Rig(5, BrakingMode.Passenger);
        rig.Run(2);
        var valve = rig.Front.Distributor!;
        valve.Isolated = true;
        valve.PullRelease();
        rig.Service(1.0);
        rig.Run(30);
        Assert.False(valve.ReleaseValveOpen);

        // Cut in, it applies with the train and stays applied.
        valve.Isolated = false;
        rig.Run(30);
        Assert.InRange(rig.Front.CylinderGauge / Air.Bar, 1.5, 3.0);
    }
}
