using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

/// <summary>Each engine's own brake standard (<see cref="EngineStandards"/>).</summary>
public class EngineStandardsTests
{
    [Theory]
    [InlineData("LocoS282A", AirStandard.American)]
    [InlineData("LocoS060", AirStandard.American)]
    [InlineData("LocoDE2", AirStandard.European)]
    [InlineData("LocoDE6", AirStandard.European)]
    [InlineData("LocoDM3", AirStandard.European)]
    [InlineData("S484-840A", AirStandard.American)]
    [InlineData("A.T.&S.F. 2901-A", AirStandard.American)]
    [InlineData("Jk3A", AirStandard.American)]
    [InlineData("S2102-870", AirStandard.American)]
    [InlineData("S2102-720", AirStandard.European)]
    [InlineData("EMD-GP7", AirStandard.American)]
    [InlineData("DE4-750", AirStandard.American)]
    [InlineData("DE4-751", AirStandard.American)]
    public void AnEngineHasItsPrototypesStandard(string id, AirStandard expected)
    {
        Assert.Equal(expected, EngineStandards.Known(id, null));
        Assert.Equal(expected, EngineStandards.For(false, expected == AirStandard.American ? AirStandard.European : AirStandard.American, id, null));
    }

    [Fact]
    public void AnEngineNotInTheTableFollowsTheSetting()
    {
        Assert.Null(EngineStandards.Known("em_lms_8f_loc", null));
        Assert.Equal(AirStandard.European, EngineStandards.For(false, AirStandard.European, "SomeoneElsesLoco", "SomeoneElsesLoco"));
        Assert.Equal(AirStandard.American, EngineStandards.For(false, AirStandard.American, "em_lms_8f_loc", null));
    }

    [Fact]
    public void TheCarTypeIdIsTriedWhenTheLiveryIsntKnown()
    {
        Assert.Equal(AirStandard.American, EngineStandards.Known("LocoS282A_Weathered", "LocoS282A"));
    }

    [Fact]
    public void TheOverrideGivesEveryEngineTheSetting()
    {
        Assert.Equal(AirStandard.European, EngineStandards.For(true, AirStandard.European, "LocoS282A", null));
        Assert.Equal(AirStandard.American, EngineStandards.For(true, AirStandard.American, "LocoDE2", null));
    }
}
