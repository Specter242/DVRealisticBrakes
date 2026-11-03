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
}
