using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

/// <summary>The words by the cursor on an engine's brake controls.</summary>
public class CabValveTextTests
{
    private static Rig Steam() =>
        new(5, american: true, engine: LocomotiveBrakeSpec.EtSteam(), driver: DriverBrakeValveSpec.H6());

    [Fact]
    public void TheManualLapHandleNamesEachPosition()
    {
        var rig = Steam();
        string At(DriverValvePosition position)
        {
            rig.Valve.Position = position;
            rig.Run(0.5);
            return CabValveText.TrainBrake(rig.Engine, selfLapping: false);
        }
        Assert.StartsWith("Train brake: release", At(DriverValvePosition.Fill));
        Assert.Equal("Train brake: running", At(DriverValvePosition.Running));
        Assert.StartsWith("Train brake: holding", At(DriverValvePosition.Holding));
        // The moment in release left the equalizing reservoir overcharged, to let down in running.
        Assert.Matches(@"^Train brake: lap \(equalizing reservoir [56]\.\d bar\)$", At(DriverValvePosition.Lap));
        Assert.StartsWith("Train brake: service (equalizing reservoir coming down", At(DriverValvePosition.Apply));
        Assert.Equal("Train brake: emergency", At(DriverValvePosition.Emergency));
        rig.Valve.CutIn = false;
        Assert.StartsWith("Train brake: cut out", CabValveText.TrainBrake(rig.Engine, false));
        Assert.StartsWith("Brake valve: cut out", CabValveText.Cutout(rig.Valve));
    }

    [Fact]
    public void TheSelfLappingHandleGivesItsReduction()
    {
        var rig = new Rig(5, american: true, driver: DriverBrakeValveSpec.American());
        rig.Service(0.75);
        Assert.Equal("Train brake: service (0.75 bar reduction)", CabValveText.TrainBrake(rig.Engine, selfLapping: true));
        rig.Service(1.5);
        Assert.Equal("Train brake: full service (1.5 bar reduction)", CabValveText.TrainBrake(rig.Engine, true));
        rig.Valve.Position = DriverValvePosition.Fill;
        Assert.StartsWith("Train brake: filling stroke", CabValveText.TrainBrake(rig.Engine, true));
    }

    [Fact]
    public void TheS6ShowsItsPositionItsOwnAndTheTrainBrakesApplication()
    {
        var rig = Steam();
        Assert.Equal("Independent brake: running", CabValveText.Independent(rig.Engine, true));
        rig.Engine.Independent = 1;
        rig.Run(5);
        rig.Engine.Independent = 0.5;
        rig.Run(1);
        Assert.Matches(@"^Independent brake: lap, 3\.\d bar$", CabValveText.Independent(rig.Engine, true));
        rig.Engine.Independent = 0.75;
        Assert.StartsWith("Independent brake: slow application", CabValveText.Independent(rig.Engine, true));
        rig.Engine.Independent = 0;
        rig.Run(10);
        Assert.Equal("Independent brake: running", CabValveText.Independent(rig.Engine, true));
        rig.Service(1.5);
        rig.Run(30);
        Assert.Matches(@"^Independent brake: running; train brake on the engine 3\.\d bar \(click and hold to bail off\)$", CabValveText.Independent(rig.Engine, true));
        rig.Engine.BailOffBy(1.0 * Air.Bar);
        rig.Run(1);
        Assert.Matches(@"^Independent brake: running; train brake on the engine 2\.\d bar, 1\.0 bailed off \(click and hold to bail off\)$", CabValveText.Independent(rig.Engine, true));
        Assert.DoesNotContain("bail off)", CabValveText.Independent(rig.Engine, false));
        // A handle with a release notch says so, the click setting aside; held there, it reads release.
        Assert.EndsWith("(hold the handle in release, below running, to bail off)", CabValveText.Independent(rig.Engine, false, releaseNotch: true));
        rig.Engine.BailOffBy(0.1 * Air.Bar);
        Assert.StartsWith("Independent brake: release;", CabValveText.Independent(rig.Engine, true, releaseNotch: true));
        Assert.DoesNotContain("bail off)", CabValveText.Independent(rig.Engine, true, releaseNotch: true));
    }

    [Fact]
    public void AnIndependentAppliedWithTheTrainBrakeOutOfRunningWaitsForIt()
    {
        var rig = Steam();
        rig.Engine.Independent = 1;
        rig.Run(5);
        rig.Valve.Position = DriverValvePosition.Lap;
        rig.Engine.Independent = 0;
        rig.Run(2);
        Assert.Matches(@"^Independent brake: running, 3\.\d bar, held until the train brake is in running \(click and hold to bail off\)$",
            CabValveText.Independent(rig.Engine, true));
    }

    [Fact]
    public void ASelfLappingIndependentShowsItsPressure()
    {
        var rig = new Rig(5);
        Assert.Equal("Independent brake: released", CabValveText.Independent(rig.Engine, true));
        rig.Engine.Independent = 0.5;
        rig.Run(1);
        Assert.Equal("Independent brake: 1.9 bar (of 3.8)", CabValveText.Independent(rig.Engine, true));
    }
}
