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
        var leading = double.NaN;
        while (sections[^1].GaugeBar > 4.9 && t < 30)
        {
            pipe.Step(0.002);
            t += 0.002;
            if (double.IsNaN(leading) && sections[^1].GaugeBar <= 4.99)
                leading = t;
        }
        var distance = (n - 1) * 15.0;
        var leadingSpeed = distance / leading;
        var frontSpeed = distance / t;
        // The leading edge travels near isothermal √(RT) ≈ 290 m/s; Darcy–Weisbach friction smears the 0.1 bar front to ~167 m/s.
        Assert.InRange(leadingSpeed, 260, Air.SoundSpeed * 1.05);
        Assert.InRange(frontSpeed, 155, 180);
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

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    public void AdvanceWithNonPositiveOrNanDtLeavesAirUnchangedAndLineAwake(double dt)
    {
        var rig = new Rig(5);
        rig.Service(1.0);
        rig.Run(2);
        var snapshot = rig.Cars.SelectMany(c => c.Chambers).Select(c => c.Pressure).ToArray();
        rig.Line.Advance(dt);
        Assert.True(double.IsPositiveInfinity(rig.Line.ChangeRate));
        var after = rig.Cars.SelectMany(c => c.Chambers).Select(c => c.Pressure).ToArray();
        Assert.Equal(snapshot, after);
    }

    [Fact]
    public void AdvanceCapsLongStepsAt400Substeps()
    {
        // A 10 s hitch is capped at BrakeLine.MaxSubsteps (400) × MaxSubstep (0.004 s) = 1.6 s of air.
        var capped = new Rig(10);
        var reference = new Rig(10);
        var full = new Rig(10);
        foreach (var rig in new[] { capped, reference, full })
            rig.Service(1.5);

        capped.Line.Advance(10.0);
        // Step the reference through 400 individual 0.004 s calls so the cap cannot mask itself.
        for (var i = 0; i < 400; i++)
            reference.Line.Advance(0.004);
        full.Run(10.0);

        Assert.True(double.IsFinite(capped.Line.ChangeRate));
        Assert.True(capped.Line.ChangeRate > 1.0);
        Assert.All(capped.Cars.SelectMany(c => c.Chambers), c => Assert.True(double.IsFinite(c.Pressure)));
        var cappedPressures = capped.Cars.SelectMany(c => c.Chambers).Select(c => c.Pressure).ToArray();
        var refPressures = reference.Cars.SelectMany(c => c.Chambers).Select(c => c.Pressure).ToArray();
        Assert.Equal(refPressures.Length, cappedPressures.Length);
        for (var i = 0; i < refPressures.Length; i++)
            Assert.Equal(refPressures[i], cappedPressures[i], 6);
        // Must not have advanced a full 10 seconds of application.
        Assert.True(capped.Valve.EqualizingReservoir.GaugeBar > full.Valve.EqualizingReservoir.GaugeBar + 0.5,
            $"capped ER {capped.Valve.EqualizingReservoir.GaugeBar:F2} bar vs 10 s ER {full.Valve.EqualizingReservoir.GaugeBar:F2} bar");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ChamberRejectsNonPositiveOrNonFiniteVolume(double volume)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Chamber(volume));
        var c = new Chamber(0.01);
        Assert.Throws<ArgumentOutOfRangeException>(() => c.Resize(volume));
        Assert.Throws<ArgumentOutOfRangeException>(() => c.Displace(volume));
    }

    [Fact]
    public void ChamberContainsNonFiniteAndNegativeMassOrPressure()
    {
        // Non-finite initial pressures fall back to finite atmospheric pressure; negative clamps to 0.
        Assert.Equal(Air.Atmosphere, new Chamber(0.01, double.NaN).Pressure, 6);
        Assert.Equal(Air.Atmosphere, new Chamber(0.01, double.PositiveInfinity).Pressure, 6);
        Assert.Equal(Air.Atmosphere, new Chamber(0.01, double.NegativeInfinity).Pressure, 6);
        Assert.Equal(0, new Chamber(0.01, -500).Pressure, 6);

        var c = new Chamber(0.01, Air.FromGaugeBar(5));
        var validPressure = c.Pressure;
        var validMass = c.Mass;

        c.Pressure = double.NaN;
        c.Pressure = double.PositiveInfinity;
        c.Pressure = double.NegativeInfinity;
        Assert.Equal(validPressure, c.Pressure, 9);

        c.Mass = double.NaN;
        c.Mass = double.PositiveInfinity;
        c.Mass = double.NegativeInfinity;
        Assert.Equal(validMass, c.Mass, 12);

        c.Mass = -1;
        Assert.Equal(0, c.Mass);
        Assert.Equal(0, c.Pressure);

        c.Pressure = Air.FromGaugeBar(5);
        c.Pressure = -100;
        Assert.Equal(0, c.Mass);
        Assert.Equal(0, c.Pressure);
    }

    [Fact]
    public void ChamberConstructorResizeAndDisplaceStayFiniteAndAtomic()
    {
        var huge = new Chamber(double.MaxValue);
        Assert.True(double.IsFinite(huge.Mass));
        Assert.True(double.IsFinite(huge.Pressure));

        var displaced = new Chamber(1);
        var beforeVolume = displaced.Volume;
        var beforePressure = displaced.Pressure;
        displaced.Displace(double.Epsilon);
        Assert.True(double.IsFinite(displaced.Pressure));
        Assert.Equal(beforeVolume, displaced.Volume);
        Assert.Equal(beforePressure, displaced.Pressure, 9);

        var resized = new Chamber(1, Air.FromGaugeBar(5));
        var resizedBeforeVolume = resized.Volume;
        var resizedBeforePressure = resized.Pressure;
        resized.Resize(double.MaxValue);
        Assert.True(double.IsFinite(resized.Mass));
        Assert.True(double.IsFinite(resized.Pressure));
        Assert.Equal(resizedBeforeVolume, resized.Volume);
        Assert.Equal(resizedBeforePressure, resized.Pressure, 9);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void BrakeLineAdvanceRejectsNonPositiveAndNaNSteps(double dt)
    {
        var rig = new Rig(3);
        rig.Run(60);
        var before = rig.Cars.SelectMany(c => c.Chambers).Select(c => c.Pressure).ToArray();
        rig.Line.Advance(dt);
        Assert.True(double.IsPositiveInfinity(rig.Line.ChangeRate));
        var after = rig.Cars.SelectMany(c => c.Chambers).Select(c => c.Pressure).ToArray();
        Assert.Equal(before, after);
    }

    [Fact]
    public void BrakeLineAdvanceCapsInfiniteAndHugeStepsAtMaxSubsteps()
    {
        var huge = new Rig(5);
        huge.Service(1.5);
        huge.Line.Advance(1000);

        var infinite = new Rig(5);
        infinite.Service(1.5);
        infinite.Line.Advance(double.PositiveInfinity);

        var capped = new Rig(5);
        capped.Service(1.5);
        capped.Line.Advance(BrakeLine.MaxSubsteps * BrakeLine.MaxSubstep);

        Assert.True(double.IsFinite(huge.Line.ChangeRate));
        Assert.True(double.IsFinite(infinite.Line.ChangeRate));
        Assert.Equal(capped.Line.ChangeRate, huge.Line.ChangeRate, 9);
        Assert.Equal(capped.Line.ChangeRate, infinite.Line.ChangeRate, 9);

        var cappedPressures = capped.Cars.SelectMany(c => c.Chambers).Select(c => c.Pressure).ToArray();
        var hugePressures = huge.Cars.SelectMany(c => c.Chambers).Select(c => c.Pressure).ToArray();
        var infinitePressures = infinite.Cars.SelectMany(c => c.Chambers).Select(c => c.Pressure).ToArray();
        Assert.All(hugePressures, p => Assert.True(double.IsFinite(p)));
        Assert.All(infinitePressures, p => Assert.True(double.IsFinite(p)));
        Assert.Equal(cappedPressures, hugePressures);
        Assert.Equal(cappedPressures, infinitePressures);
    }

    [Fact]
    public void BrakePipeStepStaysFiniteUnderExtremeInputs()
    {
        var a = new Chamber(0.01, Air.FromGaugeBar(5));
        var b = new Chamber(0.01, Air.FromGaugeBar(4));
        var pipe = new BrakePipe(new[] { a, b }, new[] { 15.0, 15.0 }, Rig.Geometry) { FrontOpen = true, RearOpen = true };

        foreach (var badDt in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var beforeA = a.Pressure;
            var beforeB = b.Pressure;
            pipe.Step(badDt);
            Assert.Equal(beforeA, a.Pressure);
            Assert.Equal(beforeB, b.Pressure);
            Assert.Equal(0, pipe.FrontVentFlow);
            Assert.Equal(0, pipe.RearVentFlow);
        }

        // Extreme pressure difference stays finite.
        a.Pressure = 1e100;
        b.Pressure = 1;
        pipe.Step(0.002);
        Assert.True(double.IsFinite(a.Pressure));
        Assert.True(double.IsFinite(b.Pressure));
        Assert.True(double.IsFinite(pipe.LinkFlow(0)));
        Assert.True(double.IsFinite(pipe.FrontVentFlow));
        Assert.True(double.IsFinite(pipe.RearVentFlow));
    }
}
