using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

/// <summary>A controller's axis on a notched handle: the S-6's five notches and a stock DE6's twelve.</summary>
public class AxisNotchesTests
{
    [Fact]
    public void TheFirstReadingTakesTheNearestNotch()
    {
        Assert.Equal(0, AxisNotches.Snap(0.0, 5, -1));
        Assert.Equal(1, AxisNotches.Snap(0.3, 5, -1));
        Assert.Equal(2, AxisNotches.Snap(0.4, 5, -1));
        Assert.Equal(4, AxisNotches.Snap(1.0, 5, -1));
    }

    [Fact]
    public void NoiseAtTheMiddleOfTwoNotchesKeepsTheNotch()
    {
        // Running and lap meet at 0.375 on the S-6: an axis wandering a hundredth either side stays put.
        var notch = AxisNotches.Snap(0.37, 5, -1);
        foreach (var axis in new[] { 0.38, 0.365, 0.385, 0.36, 0.39, 0.37 })
        {
            notch = AxisNotches.Snap(axis, 5, notch);
            Assert.Equal(1, notch);
        }
    }

    [Fact]
    public void AnAxisMovedOnTakesTheNextNotch()
    {
        // A deadband of 0.2 notches past halfway: 0.7 notches, 0.175 of the S-6's travel from running.
        Assert.Equal(1, AxisNotches.Snap(0.42, 5, 1));
        Assert.Equal(2, AxisNotches.Snap(0.43, 5, 1));
        Assert.Equal(2, AxisNotches.Snap(0.33, 5, 2));
        Assert.Equal(1, AxisNotches.Snap(0.32, 5, 2));
    }

    [Fact]
    public void BothEndsAreReachedFromTheNotchBeside()
    {
        Assert.Equal(0, AxisNotches.Snap(0.0, 5, 1));
        Assert.Equal(4, AxisNotches.Snap(1.0, 5, 3));
        Assert.Equal(0, AxisNotches.Snap(0.0, 12, 1));
        Assert.Equal(11, AxisNotches.Snap(1.0, 12, 10));
    }

    [Fact]
    public void AFastSweepGoesStraightToTheNearest()
    {
        Assert.Equal(4, AxisNotches.Snap(1.0, 5, 0));
        Assert.Equal(0, AxisNotches.Snap(0.02, 5, 4));
    }

    [Fact]
    public void AHandleOfManyNotchesKeepsAMinimumDeadband()
    {
        // 24 notches are 0.043 of the travel apart: the deadband is at least 0.01 of the travel, not 0.2 of a notch.
        const int notches = 24;
        var notch = AxisNotches.Snap(10.5 / 23, notches, -1);
        foreach (var wobble in new[] { -0.009, 0.009, -0.005, 0.008 })
            Assert.Equal(notch, AxisNotches.Snap((10.5 / 23) + wobble, notches, notch));
    }

    [Fact]
    public void ValuesAreTheNotchesOnTheHandlesScale()
    {
        Assert.Equal(0f, AxisNotches.Value(0, 5));
        Assert.Equal(0.25f, AxisNotches.Value(1, 5));
        Assert.Equal(1f, AxisNotches.Value(4, 5));
        Assert.Equal(1f, AxisNotches.Value(9, 5));
    }
}
