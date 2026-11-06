using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

 public class MixedEquipmentTests
{
    private static Rig EtOnUic(int cars) =>
        new(cars, BrakingMode.Goods, engine: LocomotiveBrakeSpec.EtSteam(), driver: DriverBrakeValveSpec.H6());

    private static IEnumerable<CarBrake> Wagons(Rig rig) => rig.Cars.Skip(1);

    private static string Applied(Rig rig) =>
        $"{Wagons(rig).Count(c => c.CylinderGauge >= 0.05 * Air.Bar)} cars still applied, the most at {Wagons(rig).Max(c => c.CylinderGauge) / Air.Bar:F2} bar";

    [Theory]
    [InlineData(40, 30)]
    [InlineData(40, 90)]
    [InlineData(80, 120)]
    public void AnH6RechargeInReleaseLeavesNoEuropeanCarApplied(int cars, int releaseSeconds)
    {
        // Arnoldio's group (2026-11-05): an S282 on European cars, recharged in release after an emergency, left the front
        // cars applied at about 1 bar until each was bled. Release had overcharged their control reservoirs to 5.4 bar, and
        // the H-6's feed valve holds only 5.0 in running.
        var rig = EtOnUic(cars);
        rig.Valve.Position = DriverValvePosition.Emergency;
        rig.Run(20);
        rig.Valve.Position = DriverValvePosition.Fill;
        rig.Run(releaseSeconds);
        rig.Valve.Position = DriverValvePosition.Running;
        var released = rig.RunUntil(() => Wagons(rig).All(c => c.CylinderGauge < 0.05 * Air.Bar), 900);
        Assert.False(double.IsNaN(released), $"15 minutes in running: {Applied(rig)}");
        var dragged = rig.RunUntil(() => Wagons(rig).Any(c => c.Distributor!.Applied), 600);
        Assert.True(double.IsNaN(dragged), $"a car applied again {dragged:F0} s after the train released");
    }

    [Fact]
    public void AnH6RechargeInRunningReleasesAsBefore()
    {
        var rig = EtOnUic(40);
        rig.Valve.Position = DriverValvePosition.Emergency;
        rig.Run(20);
        rig.Valve.Position = DriverValvePosition.Running;
        var released = rig.RunUntil(() => Wagons(rig).All(c => c.CylinderGauge < 0.05 * Air.Bar), 600);
        Assert.InRange(released, 120, 420);
    }

    [Fact]
    public void ACutLeftOverchargedIsFreedBehindAnH6ByAShortRelease()
    {
        // A cut a European engine left at its 5.4 bar overcharge, then an S282 at 5.0: the drop applies it, as for any engine
        // at 5.0. Release frees it, and its control reservoirs come down to the H-6's 5.0 while released, so running then
        // keeps it released.
        var rig = EtOnUic(20);
        foreach (var car in Wagons(rig))
            car.ChargeTo(Air.FromGaugeBar(5.4));
        rig.Run(30);
        Assert.All(Wagons(rig), c => Assert.True(c.Distributor!.Applied));
        rig.Valve.Position = DriverValvePosition.Fill;
        rig.RunUntil(() => Wagons(rig).All(c => !c.Distributor!.Applied), 120);
        rig.Run(20);
        rig.Valve.Position = DriverValvePosition.Running;
        rig.Run(600);
        Assert.All(Wagons(rig), c => Assert.InRange(c.CylinderGauge / Air.Bar, 0, 0.05));
        Assert.All(Wagons(rig), c => Assert.InRange(c.Distributor!.ControlReservoir.GaugeBar, 4.9, 5.01));
    }

    [Fact]
    public void TheCeilingFollowsTheValveFeedingThePipe()
    {
        // The European valve's low-pressure overcharge still reaches the cars' control reservoirs (cj187, 2026-10-31).
        var european = new Rig(5);
        european.Valve.Position = DriverValvePosition.Fill;
        european.Run(5);
        european.Valve.Position = DriverValvePosition.Running;
        european.Run(30);
        Assert.All(Wagons(european), c => Assert.InRange(c.Distributor!.ControlReservoirCeiling / Air.Bar, 5.399, 5.401));
        Assert.All(Wagons(european), c => Assert.True(c.Distributor!.ControlReservoir.GaugeBar > 5.25));

        var american = EtOnUic(5);
        american.Run(1);
        Assert.All(Wagons(american), c => Assert.InRange(c.Distributor!.ControlReservoirCeiling / Air.Bar, 4.999, 5.001));

        // With no valve feeding the pipe (a cut standing alone, or the valve cut out), the distributor's own limit stands.
        american.Valve.CutIn = false;
        american.Run(1);
        Assert.All(Wagons(american), c => Assert.True(double.IsNaN(c.Distributor!.ControlReservoirCeiling)));
    }
}
