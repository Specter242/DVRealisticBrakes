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

    private static Rig UicOnAar(int cars) =>
        new(cars, american: true, engine: new LocomotiveBrakeSpec(), driver: DriverBrakeValveSpec.Uic());

    [Fact]
    public void AEuropeanValveOnAmericanCarsGivesDirectRelease()
    {
        var rig = UicOnAar(10);
        rig.Service(1.5);
        rig.Run(40);
        Assert.All(Wagons(rig), c => Assert.InRange(c.CylinderGauge / Air.Bar, 3.4, 3.8));

        // Partial release on the European valve (from 1.5 bar reduction back up to 0.9 bar):
        // the European engine graduates down, while the American AB cars release in full.
        rig.Service(0.9);
        rig.Run(45);
        Assert.InRange(rig.Engine.Cylinder.GaugeBar, 1.9, 2.5);
        Assert.All(Wagons(rig), c =>
        {
            Assert.False(c.ControlValve!.Applied);
            Assert.InRange(c.CylinderGauge / Air.Bar, 0, 0.05);
        });
    }

    [Fact]
    public void AEuropeanValveEmergencyTripsAndRechargesAmericanCars()
    {
        var rig = UicOnAar(20);
        rig.Valve.Position = DriverValvePosition.Emergency;
        var applied = rig.RunUntil(() => rig.Rear.ControlValve!.Applied, 30);
        Assert.False(double.IsNaN(applied), "emergency never reached the rear AB car");
        rig.Run(10);
        Assert.All(Wagons(rig), c =>
        {
            Assert.True(c.ControlValve!.Emergency);
            Assert.InRange(c.CylinderGauge / Air.Bar, 4.1, 4.45);
        });

        // After the AB vent valves blow down (~60 s), running releases and recharges every car.
        rig.Run(80);
        rig.Valve.Position = DriverValvePosition.Running;
        var released = rig.RunUntil(() => Wagons(rig).All(c => !c.ControlValve!.Applied && c.CylinderGauge < 0.05 * Air.Bar), 300);
        Assert.False(double.IsNaN(released), "AB cars never released after emergency");
        var recharged = rig.RunUntil(() => Wagons(rig).All(c =>
            c.ControlValve!.AuxiliaryReservoir.GaugeBar > 4.9 && c.ControlValve.EmergencyReservoir.GaugeBar > 4.9), 600);
        Assert.False(double.IsNaN(recharged), "AB reservoirs never recharged after emergency");
    }

    [Fact]
    public void AEuropeanFillingStrokeRechargesAmericanCarsWithoutDraggingAfterward()
    {
        var rig = UicOnAar(20);
        rig.Service(1.5);
        rig.Run(40);
        rig.Valve.Position = DriverValvePosition.Fill;
        rig.Run(6);
        // Confirm Fill actually overcharges above nominal 5.0 bar (replacing Fill with Running fails here).
        Assert.True(rig.Valve.OverchargeLeft >= 0.4 * Air.Bar - 1,
            $"expected filling stroke overcharge, got {rig.Valve.OverchargeLeft / Air.Bar:F3} bar");
        Assert.True(rig.Cars[0].PipeSection.GaugeBar > 5.2,
            $"expected Fill to overcharge the engine pipe above 5.0 bar, got {rig.Cars[0].PipeSection.GaugeBar:F2} bar");

        rig.Valve.Position = DriverValvePosition.Running;
        // Monitor every released AB car on every frame from the moment Running starts through full let-down,
        // seeding from each car's state at the end of Fill so a first-frame reapplication is caught.
        var everReleased = new bool[rig.Cars.Count];
        for (var i = 1; i < rig.Cars.Count; i++)
            everReleased[i] = !rig.Cars[i].ControlValve!.Applied;
        var peakFrontPipe = rig.Front.PipeSection.GaugeBar;
        var reappliedAt = rig.RunUntil(() =>
        {
            peakFrontPipe = Math.Max(peakFrontPipe, rig.Front.PipeSection.GaugeBar);
            for (var i = 1; i < rig.Cars.Count; i++)
            {
                var applied = rig.Cars[i].ControlValve!.Applied;
                if (!applied)
                    everReleased[i] = true;
                else if (everReleased[i])
                    return true;
            }
            return false;
        }, 480);

        Assert.True(double.IsNaN(reappliedAt), $"a released AB car reapplied {reappliedAt:F1} s after returning to Running");
        Assert.True(peakFrontPipe > 5.2, $"filling stroke never overcharged the front car's pipe (peak {peakFrontPipe:F2} bar)");
        Assert.All(Wagons(rig), c =>
        {
            Assert.False(c.ControlValve!.Applied);
            Assert.InRange(c.CylinderGauge / Air.Bar, 0, 0.05);
            Assert.True(c.ControlValve.AuxiliaryReservoir.GaugeBar > 4.9);
            Assert.True(c.ControlValve.EmergencyReservoir.GaugeBar > 4.9);
        });
        Assert.Equal(0, rig.Valve.OverchargeLeft, 3);
        Assert.InRange(rig.Rear.PipeSection.GaugeBar, 4.95, 5.05);
    }
}
