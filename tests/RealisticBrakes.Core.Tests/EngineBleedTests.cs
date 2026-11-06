using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

 public class EngineBleedTests
{
    private static double EngineBar(Rig rig) => rig.Cars[0].CylinderGauge / Air.Bar;

    private static void Apply(Rig rig)
    {
        rig.Service(1.0);
        rig.Run(15);
        rig.Valve.Position = DriverValvePosition.Lap;
        rig.Run(15);
    }

    [Fact]
    public void AEuropeanEngineLetsGoAndTheTrainStaysApplied()
    {
        var rig = new Rig(5);
        Apply(rig);
        Assert.True(EngineBar(rig) > 0.5, $"the engine stood at {EngineBar(rig):F2} bar");
        rig.Engine.PullRelease();
        rig.Run(40);
        Assert.InRange(EngineBar(rig), 0, 0.05);
        Assert.All(rig.Cars.Skip(1), c => Assert.True(c.CylinderGauge > 0.5 * Air.Bar));
    }

    [Fact]
    public void AnEtEngineLetsGoAndTheTrainStaysApplied()
    {
        // The No. 6 distributing valve holds the automatic application in its application chamber; a pull lets it out.
        var rig = new Rig(5, american: true, engine: LocomotiveBrakeSpec.EtSteam(), driver: DriverBrakeValveSpec.H6());
        Apply(rig);
        Assert.True(EngineBar(rig) > 0.5, $"the engine stood at {EngineBar(rig):F2} bar");
        rig.Engine.PullRelease();
        rig.Run(40);
        Assert.InRange(EngineBar(rig), 0, 0.05);
        Assert.All(rig.Cars.Skip(1), c => Assert.True(c.CylinderGauge > 0.5 * Air.Bar));

        // A further reduction applies the engine again, and a release lets it go as usual.
        rig.Service(1.5);
        rig.Run(15);
        rig.Valve.Position = DriverValvePosition.Lap;
        rig.Run(15);
        Assert.True(EngineBar(rig) > 0.3, $"a further reduction left the engine at {EngineBar(rig):F2} bar");
        rig.Valve.Position = DriverValvePosition.Running;
        rig.Run(60);
        Assert.InRange(EngineBar(rig), 0, 0.05);
    }

    [Fact]
    public void AnEtEngineOnEuropeanCarsLetsGoToo()
    {
        var rig = new Rig(5, engine: LocomotiveBrakeSpec.EtSteam(), driver: DriverBrakeValveSpec.H6());
        Apply(rig);
        rig.Engine.PullRelease();
        rig.Run(40);
        Assert.InRange(EngineBar(rig), 0, 0.05);
        Assert.All(rig.Cars.Skip(1), c => Assert.True(c.CylinderGauge > 0.5 * Air.Bar));
    }
}
