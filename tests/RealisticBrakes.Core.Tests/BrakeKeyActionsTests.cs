using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

/// <summary>The brake keys name the handles of an engine whose handles feed ports of their own (the WE-4U).</summary>
public class BrakeKeyActionsTests
{
    [Theory]
    [InlineData("BrakeIncremental", BrakeKeyControl.TrainBrake)]
    [InlineData("BrakeAbsolute", BrakeKeyControl.TrainBrake)]
    [InlineData("Train Controls/BrakeIncremental", BrakeKeyControl.TrainBrake)]
    [InlineData("brakeabsolute", BrakeKeyControl.TrainBrake)]
    [InlineData("IndependentBrakeIncremental", BrakeKeyControl.Independent)]
    [InlineData("IndependentBrakeAbsolute", BrakeKeyControl.Independent)]
    [InlineData("BrakeCutoutIncremental", BrakeKeyControl.BrakeCutout)]
    [InlineData("BrakeCutoutAbsolute", BrakeKeyControl.BrakeCutout)]
    [InlineData("BrakeCutoutToggle", BrakeKeyControl.BrakeCutout)]
    public void TheBrakeKeysNameTheirControl(string action, BrakeKeyControl expected) =>
        Assert.Equal(expected, BrakeKeyActions.Of(action));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ThrottleIncremental")]
    [InlineData("DynamicBrakeIncremental")]
    [InlineData("DynamicBrakeAbsolute")]
    [InlineData("Brake")]
    public void OtherKeysAreNoBrakeControl(string? action) => Assert.Equal(BrakeKeyControl.None, BrakeKeyActions.Of(action!));
}
