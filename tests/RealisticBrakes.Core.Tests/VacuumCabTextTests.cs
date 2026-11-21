using System.Globalization;
using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

 public class VacuumCabTextTests
{
    private const double WagonLength = 1500 * 0.3048 / 70;

    private static VacuumRig DieselApplied(double inches)
    {
        var rig = new VacuumRig(5, WagonLength, engine: VacuumEngineSpec.Diesel());
        rig.Engine.MainReservoir.Pressure = Air.FromAbsoluteBar(9);
        rig.Engine.Position = VacuumValvePosition.Service;
        rig.Engine.Target = Vacuum.FromInches(inches);
        rig.Run(40);
        return rig;
    }

    private static string Train(VacuumRig rig) => VacuumCabText.TrainBrake(rig.Engine, rig.Cars[0].PipeSection.Pressure);

    [Fact]
    public void ADieselBailsOffAndTheTrainStaysApplied()
    {
        var rig = DieselApplied(10);
        var share = rig.Engine.BrakeShare;
        var train = rig.Rear.VacuumCylinder.ForceShare;
        Assert.InRange(share, 0.45, 0.6);

        Assert.InRange(rig.Engine.BailOff(), 0.45, 0.6);
        rig.Run(10);

        Assert.True(rig.Engine.BrakeShare < 0.02, $"the engine's brake stands at {rig.Engine.BrakeShare:F3}");
        Assert.InRange(rig.Rear.VacuumCylinder.ForceShare, train - 0.03, train + 0.03);
    }

    [Fact]
    public void AFurtherApplicationPutsTheEngineBackByThatMuch()
    {
        var rig = DieselApplied(14);
        var bailed = rig.Engine.AutomaticShare;
        rig.Engine.BailOff();
        rig.Run(10);
        rig.Engine.Target = Vacuum.FromInches(7);
        rig.Run(40);
        var expected = rig.Engine.AutomaticShare - bailed;
        Assert.True(expected > 0.25, $"only {expected:F2} further applied");
        Assert.InRange(rig.Engine.BrakeShare, expected - 0.03, expected + 0.03);
    }

    [Fact]
    public void AReleaseClearsTheBail()
    {
        var rig = DieselApplied(10);
        rig.Engine.BailOff();
        rig.Run(10);
        rig.Engine.Position = VacuumValvePosition.Running;
        rig.Run(90);
        Assert.True(rig.Engine.BailedOff < 0.01, $"{rig.Engine.BailedOff:F3} still bailed");

        rig.Engine.Position = VacuumValvePosition.Service;
        rig.Engine.Target = Vacuum.FromInches(10);
        rig.Run(40);
        Assert.InRange(rig.Engine.BrakeShare, 0.45, 0.6);
    }

    [Fact]
    public void ABailLeavesTheIndependentOn()
    {
        var rig = DieselApplied(10);
        rig.Engine.Independent = 0.3;
        rig.Engine.BailOff();
        rig.Run(10);
        Assert.InRange(rig.Engine.BrakeShare, 0.27, 0.33);
    }

    [Fact]
    public void ASteamBrakeCannotBeBailedOff()
    {
        var rig = new VacuumRig(5, WagonLength);
        rig.Engine.Position = VacuumValvePosition.Apply;
        rig.Run(4);
        rig.Engine.Position = VacuumValvePosition.Lap;
        rig.Run(5);
        var share = rig.Engine.BrakeShare;
        Assert.True(share > 0.2, $"the steam brake stands at {share:F2}");

        Assert.False(rig.Engine.CanBailOff);
        Assert.Equal(0, rig.Engine.BailOff());
        rig.Run(5);
        Assert.Equal(0, rig.Engine.BailedOff);
        Assert.True(rig.Engine.BrakeShare >= share - 0.03, $"the steam brake fell to {rig.Engine.BrakeShare:F2}");
    }

    [Fact]
    public void TheSteamHandleSaysWhatEachPositionDoes()
    {
        var rig = new VacuumRig(0, WagonLength);
        rig.Run(1);
        Assert.Equal("Train brake: running (small ejector on the pipe, 21.0 in)", Train(rig));
        rig.Engine.PumpOn = false;
        Assert.Equal("Train brake: running (the small ejector's steam valve is shut, 21.0 in)", Train(rig));
        rig.Engine.Position = VacuumValvePosition.Release;
        Assert.Equal("Train brake: release (large ejector on the pipe, 21.0 in)", Train(rig));
        rig.Engine.PumpOn = true;
        Assert.Equal("Train brake: release (both ejectors on the pipe, 21.0 in)", Train(rig));
        rig.Engine.Position = VacuumValvePosition.Lap;
        Assert.StartsWith("Train brake: lap (pipe cut off at 21.0 in", Train(rig));
        rig.Engine.Position = VacuumValvePosition.Apply;
        Assert.EndsWith("lap to hold it)", Train(rig));
        rig.Engine.Position = VacuumValvePosition.Emergency;
        Assert.StartsWith("Train brake: emergency", Train(rig));
        rig.Engine.CutIn = false;
        Assert.StartsWith("Train brake: cut out", Train(rig));
        Assert.StartsWith("Brake valve: cut out", VacuumCabText.Cutout(rig.Engine));
    }

    [Fact]
    public void TheDieselHandleReadsInInches()
    {
        var rig = DieselApplied(12);
        Assert.StartsWith("Train brake: service (holding 12.0 in, the pipe at 1", Train(rig));
        rig.Engine.Position = VacuumValvePosition.Release;
        Assert.StartsWith("Train brake: filling (exhausters at full speed", Train(rig));
        rig.Engine.Position = VacuumValvePosition.Running;
        Assert.StartsWith("Train brake: running (exhausters on the pipe", Train(rig));
        rig.Engine.PumpOn = false;
        Assert.StartsWith("Train brake: running (exhausters stopped with the engine", Train(rig));
    }

    [Fact]
    public void TheWheelIsNamedForTheSmallEjector()
    {
        var engine = new VacuumEngine(VacuumEngineSpec.Steam());
        Assert.Equal("Small ejector steam valve: open", VacuumCabText.EjectorValve(engine));
        engine.Supply = 0.2;
        Assert.Equal("Small ejector steam valve: open, short of boiler pressure", VacuumCabText.EjectorValve(engine));
        engine.PumpOn = false;
        Assert.StartsWith("Small ejector steam valve: shut", VacuumCabText.EjectorValve(engine));
        Assert.Null(VacuumCabText.EjectorValve(new VacuumEngine(VacuumEngineSpec.Diesel())));
    }

    [Fact]
    public void TheIndependentOffersTheBailOnlyWhereItWorks()
    {
        var diesel = DieselApplied(10);
        Assert.Matches(@"^Independent brake: released; train brake on the engine [12]\.\d bar \(click to bail off\)$", VacuumCabText.Independent(diesel.Engine, true));
        Assert.DoesNotContain("bail off", VacuumCabText.Independent(diesel.Engine, false));
        diesel.Engine.BailOff();
        diesel.Run(2);
        Assert.Matches(@"train brake on the engine 0\.\d bar, \d\.\d bar bailed off$", VacuumCabText.Independent(diesel.Engine, true));

        var steam = new VacuumRig(0, WagonLength);
        steam.Engine.Independent = 0.5;
        steam.Engine.Position = VacuumValvePosition.Apply;
        steam.Run(5);
        var text = VacuumCabText.Independent(steam.Engine, true);
        Assert.StartsWith("Independent brake: 90 psi of steam (of 180 psi); train brake on the engine", text);
        Assert.DoesNotContain("bail", text);
    }

    [Fact]
    public void CutOutDistinguishesTrailingFromLoneEngine()
    {
        var rig = new VacuumRig(0, WagonLength);
        rig.Engine.CutIn = false;
        Assert.Equal(
            "Train brake: cut out (the leading engine's valve works the pipe)",
            VacuumCabText.TrainBrake(rig.Engine, rig.Cars[0].PipeSection.Pressure));
        Assert.Equal(
            "Train brake: cut out (the leading engine's valve works the pipe)",
            VacuumCabText.TrainBrake(rig.Engine, rig.Cars[0].PipeSection.Pressure, anotherValveCutIn: true));
        Assert.Equal(
            "Train brake: cut out (no brake valve working the pipe — cut it in to control the train)",
            VacuumCabText.TrainBrake(rig.Engine, rig.Cars[0].PipeSection.Pressure, anotherValveCutIn: false));
    }

    [Fact]
    public void FormatsNumbersWithInvariantCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var steam = new VacuumRig(0, WagonLength);
            steam.Run(1);
            Assert.Equal("Train brake: running (small ejector on the pipe, 21.0 in)", Train(steam));

            var diesel = DieselApplied(12);
            Assert.StartsWith("Train brake: service (holding 12.0 in, the pipe at 1", Train(diesel));
            Assert.Matches(@"^Independent brake: released; train brake on the engine [12]\.\d bar \(click to bail off\)$",
                VacuumCabText.Independent(diesel.Engine, true));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
