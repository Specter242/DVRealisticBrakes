using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

 public class TractionCutTests
{
    private static double Bar(double gauge) => gauge * Air.Bar;

    [Fact]
    public void AEuropeanEngineLosesPowerUnderThreeAndAHalfBarNotInAFullService()
    {
        var cut = new TractionCut(TractionCutStyle.BrakePipe);
        Assert.False(cut.Update(Bar(5.0), false, 0.8));
        // A full service holds the pipe at 3.5 bar, within the relay's dead band.
        cut.Update(Bar(3.495), false, 0.8);
        Assert.False(cut.Cut);
        cut.Update(Bar(3.4), false, 0.8);
        Assert.True(cut.Cut);
    }

    [Fact]
    public void PowerComesBackOnlyOnceThePipeHasRecoveredAndTheThrottleIsClosed()
    {
        foreach (var style in new[] { TractionCutStyle.BrakePipe, TractionCutStyle.PcSwitch })
        {
            var cut = new TractionCut(style);
            cut.Update(0, true, 0.8);
            Assert.True(cut.Cut, $"{style} didn't cut on an emergency");
            // Recovering, but not yet back: still cut, throttle closed or not.
            cut.Update(Bar(4.0), false, 0);
            Assert.True(cut.Cut);
            // Back, with the throttle open: still cut.
            cut.Update(Bar(4.9), false, 0.5);
            Assert.True(cut.Cut);
            // Back, and closed: power returns.
            Assert.True(cut.Update(Bar(4.9), false, 0));
            Assert.False(cut.Cut);
        }
    }

    [Fact]
    public void ThePcSwitchOpensOnAnEmergencyNotOnAServiceApplication()
    {
        var cut = new TractionCut(TractionCutStyle.PcSwitch);
        // An American full service (and more) leaves the pipe well over 2 bar.
        cut.Update(Bar(3.0), false, 0.8);
        Assert.False(cut.Cut);
        cut.Update(Bar(4.8), true, 0.8);
        Assert.True(cut.Cut, "the handle's emergency position opens it at once");
        cut.Clear();
        cut.Update(Bar(1.9), false, 0.8);
        Assert.True(cut.Cut, "an emergency from the train side opens it");
    }

    [Fact]
    public void SteamAndOtherModsEnginesAreNeverCut()
    {
        var cut = new TractionCut(TractionCutStyle.None);
        Assert.False(cut.Update(0, true, 1));
        Assert.False(cut.Cut);
    }

    [Fact]
    public void ATrainInEmergencyCutsTheEnginesPowerAndARecoveredOneGivesItBack()
    {
        // The model's own pipe: a European train vented in emergency falls under 3.5 bar at the engine at once.
        var rig = new Rig(10);
        var cut = new TractionCut(TractionCutStyle.BrakePipe);
        double EnginePipe() => rig.Cars[0].PipeSection.Pressure - Air.Atmosphere;
        rig.Valve.Position = DriverValvePosition.Emergency;
        var cutAt = rig.RunUntil(() => { cut.Update(EnginePipe(), true, 0.6); return cut.Cut; }, 5);
        Assert.InRange(cutAt, 0, 1);
        rig.Run(10);
        rig.Valve.Position = DriverValvePosition.Running;
        var back = rig.RunUntil(() => { cut.Update(EnginePipe(), false, 0); return !cut.Cut; }, 300);
        Assert.False(double.IsNaN(back), "power never came back");
        Assert.True(EnginePipe() > TractionCut.RecoveredAbove);
    }
}
