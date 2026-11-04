using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

/// <summary>The hand brakes of a cut just spawned or loaded (<see cref="FreshCut"/>).</summary>
public class FreshCutTests
{
    [Fact]
    public void AParkedCutItsBrakesCantHoldGetsItsHandBrakes()
    {
        Assert.True(FreshCut.Secures(standing: true, driven: false, holds: false));
        Assert.False(FreshCut.Secures(standing: true, driven: false, holds: true));
        Assert.False(FreshCut.Secures(standing: false, driven: false, holds: false));
    }

    [Fact]
    public void ATrainAnotherModsDriverRunsGetsNone()
    {
        // Night Shadow VI, 2026-11-03: AI Traffic's trains were left with every hand brake on.
        Assert.False(FreshCut.Secures(standing: true, driven: true, holds: false));
    }

    [Fact]
    public void HandBrakesSetHereComeOffWhenADriverTakesOver()
    {
        Assert.True(FreshCut.Frees(driven: true, handBrake: 1.0));
        // A parked cut no driver takes keeps them, and one someone has moved since is theirs.
        Assert.False(FreshCut.Frees(driven: false, handBrake: 1.0));
        Assert.False(FreshCut.Frees(driven: true, handBrake: 0.5));
    }

    [Fact]
    public void ACutIsWeighedOnceItsCouplingsHaveFormed()
    {
        Assert.Equal(FreshCut.Step.Wait, FreshCut.Next(age: 1, standing: true, driven: false));
        Assert.Equal(FreshCut.Step.Weigh, FreshCut.Next(age: 3.1, standing: true, driven: false));
    }

    [Fact]
    public void ACutStillMovingAtItsCheckIsWatchedUntilItStands()
    {
        // The in-game check of 2026-11-03: a parked cut still settling at 3 s was never secured.
        Assert.Equal(FreshCut.Step.Wait, FreshCut.Next(age: 3.1, standing: false, driven: false));
        Assert.Equal(FreshCut.Step.Wait, FreshCut.Next(age: 20, standing: false, driven: false));
        Assert.Equal(FreshCut.Step.Weigh, FreshCut.Next(age: 20, standing: true, driven: false));
    }

    [Fact]
    public void ACutStillMovingAfterHalfAMinuteIsLetGo()
    {
        Assert.Equal(FreshCut.Step.Drop, FreshCut.Next(age: 30, standing: false, driven: false));
    }

    [Fact]
    public void ACutADriverRunsIsLetGoAtOnce()
    {
        Assert.Equal(FreshCut.Step.Drop, FreshCut.Next(age: 0.5, standing: true, driven: true));
        Assert.Equal(FreshCut.Step.Drop, FreshCut.Next(age: 10, standing: false, driven: true));
    }
}
