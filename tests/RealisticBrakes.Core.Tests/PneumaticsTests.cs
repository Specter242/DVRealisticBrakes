using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

public class PneumaticsTests
{
    [Fact]
    public void ChokedFlowFollowsTheUpstreamPressure()
    {
        var hole = new Orifice(2);
        var low = hole.MassFlow(Air.FromAbsoluteBar(4), Air.Atmosphere);
        var high = hole.MassFlow(Air.FromAbsoluteBar(8), Air.Atmosphere);
        Assert.Equal(2.0, high / low, 3);
        // About 4.7 g/s through a 2 mm sharp-edged hole from 8 bar absolute: ṁ = 0.0404·Cd·A·p/√T
        // (pneumatics tables give about 1 g/s, 55 L/min of free air, through 1 mm from 6 bar gauge).
        Assert.InRange(high, 0.0045, 0.0050);
    }

    [Fact]
    public void FlowRunsBothWaysAndStopsWhenLevel()
    {
        var hole = new Orifice(3);
        var a = Air.FromGaugeBar(5);
        var b = Air.FromGaugeBar(4.9);
        Assert.True(hole.MassFlow(a, b) > 0);
        Assert.Equal(-hole.MassFlow(a, b), hole.MassFlow(b, a), 12);
        Assert.Equal(0, hole.MassFlow(a, a));
    }

    [Fact]
    public void PassingNeverOvershootsLevel()
    {
        var big = new Chamber(0.001, Air.FromGaugeBar(8));
        var small = new Chamber(0.0001, Air.Atmosphere);
        var total = big.Mass + small.Mass;
        new Orifice(20).Pass(big, small, 10);
        Assert.Equal(big.Pressure, small.Pressure, 3);
        Assert.Equal(total, big.Mass + small.Mass, 12);
    }

    [Fact]
    public void ACheckValveOnlyLetsAirOneWay()
    {
        var pipe = new Chamber(0.01, Air.FromGaugeBar(3.5));
        var reservoir = new Chamber(0.1, Air.FromGaugeBar(5));
        var moved = new Orifice(5).PassOneWay(pipe, reservoir, 1);
        Assert.Equal(0, moved);
        Assert.Equal(5, reservoir.GaugeBar, 6);
    }

    [Fact]
    public void VentingStopsAtItsFloor()
    {
        var c = new Chamber(0.01, Air.FromGaugeBar(5));
        new Orifice(10).Vent(c, 100, Air.FromGaugeBar(3.5));
        Assert.Equal(3.5, c.GaugeBar, 6);
    }

    [Fact]
    public void ARestrictionFollowsWithItsTimeConstant()
    {
        var source = new Chamber(1000, Air.FromGaugeBar(5));
        var follower = new Chamber(0.015, Air.FromGaugeBar(4));
        var r = Restriction.WithTimeConstant(0.015, 10);
        for (var i = 0; i < 1000; i++)
            r.Pass(source, follower, 0.01);
        // After one time constant, 1 − 1/e of the way there.
        Assert.Equal(4 + (1 - Math.Exp(-1)), follower.GaugeBar, 2);
    }

    [Fact]
    public void AClosedPipeKeepsItsAirAndStaysLevel()
    {
        var sections = Enumerable.Range(0, 20).Select(_ => new Chamber(Rig.Geometry.SectionVolume(15), Air.FromGaugeBar(5))).ToList();
        var pipe = new BrakePipe(sections, Enumerable.Repeat(15.0, 20).ToList(), Rig.Geometry);
        var mass = sections.Sum(s => s.Mass);
        for (var i = 0; i < 5000; i++)
            pipe.Step(0.004);
        Assert.Equal(mass, sections.Sum(s => s.Mass), 9);
        Assert.All(sections, s => Assert.Equal(5, s.GaugeBar, 6));
    }

    [Fact]
    public void ADropRunsDownThePipeAtAboutTheSpeedOfSound()
    {
        const int n = 40;
        var sections = Enumerable.Range(0, n).Select(_ => new Chamber(Rig.Geometry.SectionVolume(15), Air.FromGaugeBar(5))).ToList();
        var pipe = new BrakePipe(sections, Enumerable.Repeat(15.0, n).ToList(), Rig.Geometry) { FrontOpen = true };
        var t = 0.0;
        while (sections[^1].GaugeBar > 4.9 && t < 30)
        {
            pipe.Step(0.002);
            t += 0.002;
        }
        var speed = (n - 1) * 15 / t;
        // A frictionless pipe carries it at √(RT) ≈ 290 m/s; friction smears the front.
        Assert.InRange(speed, 150, Air.SoundSpeed * 1.05);
    }

    [Fact]
    public void RebuildingAPipeKeepsTheFlowsItShares()
    {
        var a = new Chamber(0.01, Air.FromGaugeBar(5));
        var b = new Chamber(0.01, Air.FromGaugeBar(4));
        var c = new Chamber(0.01, Air.FromGaugeBar(4));
        var lengths = new[] { 15.0, 15.0 };
        var old = new BrakePipe(new[] { a, b }, lengths, Rig.Geometry);
        old.Step(0.004);
        var flow = old.LinkFlow(0);
        Assert.True(flow > 0);
        // The same hose seen from the other end, as a brake set's cars reverse when it merges.
        var rebuilt = new BrakePipe(new[] { c, b, a }, new[] { 15.0, 15.0, 15.0 }, Rig.Geometry);
        rebuilt.CarryFlowsFrom(old);
        Assert.Equal(0, rebuilt.LinkFlow(0));
        Assert.Equal(-flow, rebuilt.LinkFlow(1), 12);
    }
}
