using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

/// <summary>An American steam engine's E-T equipment: the H-6 automatic, the S-6 independent, and the No. 6 distributing valve.</summary>
public class EtEquipmentTests
{
    private static Rig Train(int cars = 10) =>
        new(cars, american: true, engine: LocomotiveBrakeSpec.EtSteam(), driver: DriverBrakeValveSpec.H6());

    private static double EngineBar(Rig rig) => rig.Cars[0].CylinderGauge / Air.Bar;

    [Fact]
    public void TheH6ReducesAbout5PoundsEvery2Seconds()
    {
        var rig = Train();
        rig.Service(1.5);
        var er = rig.Valve.EqualizingReservoir;
        var first = rig.RunUntil(() => er.GaugeBar < 5 - 5 / 14.5038, 30);
        var full = first + rig.RunUntil(() => er.GaugeBar < 3.52, 30);
        Assert.InRange(first, 1.5, 2.5);
        Assert.InRange(full, 7.5, 10);
    }

    [Fact]
    public void TheH6LappedLeavesTheLeakToTheTrain()
    {
        // Lapped, "there is no further reduction of the latter except from leakage" (Wood p. 143): the H-6 doesn't make it up.
        var h6 = Train();
        var american = new Rig(10, american: true, driver: DriverBrakeValveSpec.American());
        foreach (var rig in new[] { h6, american })
        {
            rig.Service(0.5);
            rig.Run(20);
            rig.Valve.Position = DriverValvePosition.Lap;
            rig.Rear.PipeLeak = new Orifice(1.0);
            rig.Run(60);
        }
        Assert.True(h6.Rear.PipeSection.GaugeBar < h6.Valve.EqualizingReservoir.GaugeBar - 0.1);
        // The leak applies the train harder.
        Assert.True(h6.Rear.CylinderGauge > american.Rear.CylinderGauge + 0.2 * Air.Bar);
        Assert.InRange(american.Rear.PipeSection.GaugeBar, american.Valve.EqualizingReservoir.GaugeBar - 0.03, 5);
    }

    [Fact]
    public void TheEngineHoldsItsBrakeThroughReleaseAndHoldingAndLetsGoInRunning()
    {
        var rig = Train();
        rig.Service(1.0);
        rig.Run(30);
        var applied = EngineBar(rig);
        Assert.InRange(applied, 2.0, 3.8);

        // Release, then holding: the train lets go, the engine doesn't, and it doesn't graduate off.
        rig.Valve.Position = DriverValvePosition.Fill;
        rig.Run(10);
        rig.Valve.Position = DriverValvePosition.Holding;
        rig.Run(60);
        Assert.All(rig.Cars.Skip(1), c => Assert.False(c.ControlValve!.Applied));
        Assert.InRange(EngineBar(rig), applied - 0.05, applied + 0.05);

        // Running lets it go, all at once.
        rig.Valve.Position = DriverValvePosition.Running;
        Assert.InRange(rig.RunUntil(() => EngineBar(rig) < 0.1, 30), 0, 6);
    }

    [Fact]
    public void ARunningReleaseLetsTheEngineGoWithTheTrain()
    {
        var rig = Train();
        rig.Service(1.0);
        rig.Run(30);
        rig.Valve.Position = DriverValvePosition.Running;
        rig.Run(40);
        Assert.True(EngineBar(rig) < 0.1);
    }

    [Fact]
    public void ItsOwnEmergencyGives68PoundsAndTheTrains65()
    {
        var own = Train();
        own.Valve.Position = DriverValvePosition.Emergency;
        own.Run(10);
        Assert.InRange(EngineBar(own), 4.6, 4.75);

        // Parted at the rear, with the handle lapped.
        var parted = Train();
        parted.Valve.Position = DriverValvePosition.Lap;
        parted.Rear.PipeLeak = new Orifice(25);
        parted.Run(10);
        Assert.InRange(EngineBar(parted), 4.4, 4.55);
    }

    // The S-6's notches on the game's 0-1 handle: running, lap, slow application, quick application.
    private const double Release = 0, Running = 0.25, Lap = 0.5, Slow = 0.75, Quick = 1;

    [Fact]
    public void TheS6GivesItsReducingValvesFortyFivePounds()
    {
        var rig = Train();
        rig.Engine.Independent = Quick;
        rig.Run(10);
        Assert.InRange(EngineBar(rig), 3.0, 3.15);
        rig.Engine.Independent = Slow;
        rig.Run(60);
        Assert.InRange(EngineBar(rig), 3.0, 3.15);
    }

    [Fact]
    public void SlowApplicationBuildsQuiteGraduallyAndQuickApplicationAtOnce()
    {
        // "Quite gradually ... very fine graduating" and "a sudden and heavy application" (Wood pp. 137-138).
        var slow = Train();
        slow.Engine.Independent = Slow;
        var t = slow.RunUntil(() => EngineBar(slow) >= 3.0, 60);
        Assert.InRange(t, 15, 25);
        var quick = Train();
        quick.Engine.Independent = Quick;
        Assert.InRange(quick.RunUntil(() => EngineBar(quick) >= 3.0, 60), 1, 4);
    }

    [Fact]
    public void LapHoldsWhatTheApplicationGave()
    {
        var rig = Train();
        rig.Engine.Independent = Slow;
        rig.Run(8);
        rig.Engine.Independent = Lap;
        rig.Run(2);
        var held = EngineBar(rig);
        Assert.InRange(held, 0.8, 2.2);
        rig.Run(60);
        Assert.InRange(EngineBar(rig), held - 0.03, held + 0.03);
    }

    [Fact]
    public void RunningLetsTheIndependentGoOnlyWithTheAutomaticInRunning()
    {
        // The release pipe runs through the H-6, which opens it only in running (ICS p. 146).
        var rig = Train();
        rig.Engine.Independent = Quick;
        rig.Run(5);
        rig.Valve.Position = DriverValvePosition.Lap;
        rig.Engine.Independent = Running;
        rig.Run(20);
        Assert.InRange(EngineBar(rig), 3.0, 3.15);
        rig.Valve.Position = DriverValvePosition.Holding;
        rig.Run(20);
        Assert.InRange(EngineBar(rig), 3.0, 3.15);
        rig.Valve.Position = DriverValvePosition.Running;
        Assert.InRange(rig.RunUntil(() => EngineBar(rig) < 0.1, 30), 0, 6);
    }

    [Fact]
    public void ALappedIndependentKeepsAnAutomaticApplicationThroughTheRecharge()
    {
        // In lap the S-6 blanks the release pipe: "the locomotive brake could not be released by returning the automatic brake
        // valve handle to running position until the independent brake valve should be replaced in running position also"
        // (Wood p. 137).
        var rig = Train();
        rig.Service(1.0);
        rig.Run(30);
        var applied = EngineBar(rig);
        rig.Engine.Independent = Lap;
        rig.Valve.Position = DriverValvePosition.Running;
        rig.Run(60);
        Assert.All(rig.Cars.Skip(1), c => Assert.False(c.ControlValve!.Applied));
        Assert.InRange(EngineBar(rig), applied - 0.05, applied + 0.05);
        rig.Engine.Independent = Running;
        Assert.InRange(rig.RunUntil(() => EngineBar(rig) < 0.1, 30), 0, 6);
    }

    [Fact]
    public void ReleaseLetsTheApplicationChamberOutAsFarAsItIsHeld()
    {
        // Held in release, the S-6 lets out the independent's air with the automatic's, graduated.
        var rig = Train();
        rig.Engine.Independent = Quick;
        rig.Run(5);
        rig.Engine.Independent = Lap;
        rig.Valve.Position = DriverValvePosition.Lap;
        rig.Run(2);
        Assert.Equal(1.0, rig.Engine.BailOffBy(1.0 * Air.Bar) / Air.Bar, 6);
        rig.Run(10);
        Assert.InRange(EngineBar(rig), 2.0, 2.15);
    }

    [Fact]
    public void ChamberDLagsThePipeInReleaseAndFollowsItInRunning()
    {
        // Release blows main reservoir air into chamber D through a small port; running joins it to the pipe, "so that chamber D
        // and the equalizing reservoir charge uniformly with the brake pipe" (ICS p. 138).
        var rig = Train(30);
        rig.Service(1.5);
        rig.Run(30);
        rig.Valve.Position = DriverValvePosition.Fill;
        rig.Run(3);
        var pipe = rig.Cars[0].PipeSection.GaugeBar;
        var er = rig.Valve.EqualizingReservoir.GaugeBar;
        Assert.True(er < pipe - 0.2, $"in release chamber D stood at {er:F2} bar, the pipe at the engine {pipe:F2}");
        rig.Valve.Position = DriverValvePosition.Running;
        rig.Run(10);
        pipe = rig.Cars[0].PipeSection.GaugeBar;
        er = rig.Valve.EqualizingReservoir.GaugeBar;
        Assert.InRange(er, pipe - 0.1, pipe + 0.1);
        // The feed valve feeds the pipe only to its 5 bar: as the train takes release's air, the pipe and chamber D settle there.
        rig.Run(240);
        pipe = rig.Cars[0].PipeSection.GaugeBar;
        er = rig.Valve.EqualizingReservoir.GaugeBar;
        Assert.InRange(er, pipe - 0.05, pipe + 0.05);
        Assert.InRange(er, 4.9, 5.1);
    }

    [Fact]
    public void ALapBeforeChamberDHasCaughtUpBringsThePipeDownToIt()
    {
        // Lapped straight from release, the equalizing piston lets the pipe at the engine down to chamber D's pressure: it
        // doesn't hold the release's higher pipe. The train's reservoirs, still recharging, take it lower still (the AB has
        // no accelerated release to hold the pipe up).
        var rig = Train(30);
        rig.Service(1.5);
        rig.Run(30);
        rig.Valve.Position = DriverValvePosition.Fill;
        rig.Run(3);
        var released = rig.Cars[0].PipeSection.GaugeBar;
        rig.Valve.Position = DriverValvePosition.Lap;
        var er = rig.Valve.EqualizingReservoir.GaugeBar;
        Assert.True(released > er + 0.2, $"the release raises the pipe at the engine over chamber D ({released:F2} against {er:F2} bar)");
        rig.Run(5);
        Assert.True(rig.Cars[0].PipeSection.GaugeBar <= er + 0.1, $"lapped, the pipe comes down to chamber D ({rig.Cars[0].PipeSection.GaugeBar:F2} against {er:F2} bar)");
    }

    [Fact]
    public void TheS6PositionsAreBandsOfTheHandle()
    {
        // Five notches: release, running, lap, slow, quick. Release counts as running unless a hand holds it there.
        Assert.Equal(IndependentPosition.Running, LocomotiveBrake.PositionOf(Release));
        Assert.Equal(IndependentPosition.Running, LocomotiveBrake.PositionOf(Running));
        Assert.Equal(IndependentPosition.Running, LocomotiveBrake.PositionOf(0.3));
        Assert.Equal(IndependentPosition.Lap, LocomotiveBrake.PositionOf(Lap));
        Assert.Equal(IndependentPosition.SlowApplication, LocomotiveBrake.PositionOf(Slow));
        Assert.Equal(IndependentPosition.QuickApplication, LocomotiveBrake.PositionOf(Quick));
        Assert.True(LocomotiveBrake.InRelease(Release));
        Assert.True(LocomotiveBrake.InRelease(0.1));
        Assert.False(LocomotiveBrake.InRelease(Running));
    }

    [Fact]
    public void TheIndependentCountsAsHeldInReleaseOnlyWhileABailIsHeld()
    {
        var rig = Train();
        rig.Service(1.5);
        rig.Run(30);
        Assert.False(rig.Engine.HeldInRelease);
        rig.Engine.BailOffBy(0.1 * Air.Bar);
        Assert.True(rig.Engine.HeldInRelease);
        rig.Run(LocomotiveBrake.BailHold + 0.1);
        Assert.False(rig.Engine.HeldInRelease);
    }

    [Fact]
    public void TheIndependentBailsOffAsFarAsItIsHeld()
    {
        var rig = Train();
        rig.Service(1.5);
        rig.Run(30);
        var applied = EngineBar(rig);
        Assert.Equal(1.0, rig.Engine.BailOffBy(1.0 * Air.Bar) / Air.Bar, 6);
        rig.Run(10);
        Assert.InRange(EngineBar(rig), applied - 1.05, applied - 0.95);
        // What's left stays, and a further reduction adds to it.
        rig.Service(1.0);
        rig.Run(20);
        Assert.InRange(EngineBar(rig), applied - 1.05, applied - 0.95);
    }

    [Fact]
    public void InItsOwnEmergencyABailLastsOnlyWhileTheLeverIsHeld()
    {
        // The maintaining port puts back what the independent lets out once it's lapped (Wood p. 120).
        var rig = Train();
        rig.Valve.Position = DriverValvePosition.Emergency;
        rig.Run(10);
        Assert.InRange(EngineBar(rig), 4.6, 4.75);
        for (var t = 0.0; t < 2; t += Rig.Frame)
        {
            rig.Engine.BailOffBy(1.0 * Air.Bar * Rig.Frame);
            rig.Run(Rig.Frame);
        }
        Assert.InRange(EngineBar(rig), 2.4, 3.0);
        rig.Run(10);
        Assert.InRange(EngineBar(rig), 4.6, 4.75);

        // Another player's hold comes as a bail and then only "still held" (0) every quarter second, with a longer hold;
        // their let-go ends it at once.
        rig.Engine.BailOffBy(4.7 * Air.Bar, 1.0);
        for (var t = 0.0; t < 8; t += 0.25)
        {
            rig.Run(0.25);
            rig.Engine.BailOffBy(0, 1.0);
        }
        Assert.True(EngineBar(rig) < 0.2, $"held by another player, the engine stood at {EngineBar(rig):F2} bar");
        rig.Engine.EndBail();
        rig.Run(10);
        Assert.InRange(EngineBar(rig), 4.6, 4.75);

        // A one-time bail (another mod's, through the API) lets off for its moment, then the port puts it back.
        rig.Engine.BailOff();
        rig.Run(LocomotiveBrake.BailHold);
        Assert.True(EngineBar(rig) < 4.4, $"a one-time bail left the engine at {EngineBar(rig):F2} bar");
        rig.Run(10);
        Assert.InRange(EngineBar(rig), 4.6, 4.75);

        // A train's emergency has no maintaining port: there the bail stays.
        var parted = Train();
        parted.Valve.Position = DriverValvePosition.Lap;
        parted.Rear.PipeLeak = new Orifice(25);
        parted.Run(10);
        parted.Engine.BailOffBy(1.0 * Air.Bar);
        parted.Run(10);
        Assert.InRange(EngineBar(parted), 3.4, 3.55);
    }

    [Fact]
    public void ANewApplicationAfterABailStartsFromWhatWasLeft()
    {
        var rig = Train();
        rig.Service(1.5);
        rig.Run(30);
        rig.Engine.BailOff();
        rig.Run(8);
        Assert.True(EngineBar(rig) < 0.1);
        // Released and recharged in holding, then a light application: the engine takes only that.
        rig.Valve.Position = DriverValvePosition.Fill;
        rig.Run(10);
        rig.Valve.Position = DriverValvePosition.Holding;
        rig.Run(90);
        rig.Service(0.5);
        rig.Run(20);
        Assert.InRange(EngineBar(rig), 0.6, 2.0);
    }

    [Fact]
    public void TheDuplexGovernorFollowsTheHandle()
    {
        var rig = Train();
        var pipe = rig.Cars[0].PipeSection;
        rig.Valve.Position = DriverValvePosition.Running;
        Assert.Equal(pipe.GaugeBar + 1.4, rig.Engine.GovernorSetting(pipe) / Air.Bar, 3);
        rig.Valve.Position = DriverValvePosition.Fill;
        Assert.Equal(5 + 1.4, rig.Engine.GovernorSetting(pipe) / Air.Bar, 3);
        rig.Valve.Position = DriverValvePosition.Lap;
        Assert.Equal(7.6, rig.Engine.GovernorSetting(pipe) / Air.Bar, 3);
        Assert.True(double.IsNaN(new LocomotiveBrake().GovernorSetting(pipe)));
    }

    [Fact]
    public void ReleaseBlowsThroughTheWarningPortForAsLongAsItStands()
    {
        // The game's exhaust sound saturates at 5 of its units, 5 / 841 kg/s (CarAir.StockFlowPerKgPerSecond).
        const double saturation = 5 / (Air.RT / Air.Bar * 1000);
        var rig = Train();
        rig.Valve.Position = DriverValvePosition.Fill;
        rig.Run(60);
        Assert.InRange(rig.Valve.ExhaustFlow / saturation, 0.2, 0.3);
        rig.Valve.Position = DriverValvePosition.Running;
        rig.Run(5);
        Assert.True(rig.Valve.ExhaustFlow < 0.01 * saturation);

        // The 26-L and the European valve have none.
        foreach (var spec in new[] { DriverBrakeValveSpec.American(), DriverBrakeValveSpec.Uic() })
        {
            var other = new Rig(10, american: true, driver: spec);
            other.Valve.Position = DriverValvePosition.Fill;
            other.Run(60);
            Assert.True(other.Valve.ExhaustFlow < 0.01 * saturation);
        }
    }

    [Fact]
    public void TheDistributingValveTakesNoPipeAirForQuickService()
    {
        // "The venting of brake pipe air is not included" (Wood p. 56).
        Assert.False(LocomotiveBrakeSpec.EtSteam().Distributor.QuickService.IsOpen);
    }
}

/// <summary>Period details of American cars (ICS, The Westinghouse Air-Brake Handbook, 1918).</summary>
public class AmericanCarTests
{
    [Fact]
    public void TheHeadEndOfALongTrainReleasesWithTheRear()
    {
        var rig = new Rig(50, american: true);
        rig.Service(1.5);
        rig.Run(60);
        rig.Valve.Position = DriverValvePosition.Running;
        var retarded = false;
        double front = double.NaN, rear = double.NaN;
        for (var t = 0.0; t < 120 && (double.IsNaN(front) || double.IsNaN(rear)); t += Rig.Frame)
        {
            rig.Run(Rig.Frame);
            retarded |= rig.Front.ControlValve!.Retarded;
            if (double.IsNaN(front) && rig.Front.CylinderGauge < 0.4 * Air.Bar)
                front = t;
            if (double.IsNaN(rear) && rig.Rear.CylinderGauge < 0.4 * Air.Bar)
                rear = t;
        }
        Assert.True(retarded, "the head end should release retarded");
        Assert.False(rig.Rear.ControlValve!.Retarded);
        // "At practically the same time" (p. 209).
        Assert.InRange(front - rear, -4, 4);
    }

    [Fact]
    public void TheRetainersVentTakesAbout20To25SecondsFrom50To15Pounds()
    {
        var valve = new AarControlValve { Retainer = Retainer.LowPressure };
        var pipe = new Chamber(1000, Air.FromGaugeBar(5));
        valve.ChargeTo(pipe.Pressure);
        valve.Cylinder.Pressure = Air.FromGaugeBar(4);
        const double dt = 0.002;
        for (var t = 0.0; t < 40; t += dt)
        {
            pipe.Pressure = Air.FromGaugeBar(4.4 - Math.Min(1, t / 5) * 0.9);
            valve.Step(pipe, dt);
        }
        // Applied at about 3.5 bar; now released through the retainer.
        double? from = null;
        for (var t = 0.0; t < 120; t += dt)
        {
            pipe.Pressure = Air.FromGaugeBar(5);
            valve.Step(pipe, dt);
            if (from == null && valve.Cylinder.GaugeBar < 50 / 14.5038)
                from = t;
            if (valve.Cylinder.GaugeBar < 15 / 14.5038)
            {
                Assert.InRange(t - from!.Value, 20, 26);
                return;
            }
        }
        Assert.Fail("the cylinder never came down to 15 lb");
    }

    [Fact]
    public void ShorterPistonTravelBrakesHarder()
    {
        double FullService(double travel) => FullServiceTo(travel, 3.5);
        double FullServiceTo(double travel, double pipeBar)
        {
            var valve = new AarControlValve(new AarControlValveSpec { CylinderVolume = AarControlValveSpec.CylinderVolumeAt(travel) });
            var pipe = new Chamber(1000, Air.FromGaugeBar(5));
            valve.ChargeTo(pipe.Pressure);
            for (var t = 0.0; t < 40; t += 0.002)
            {
                pipe.Pressure = Air.FromGaugeBar(Math.Max(pipeBar, 5 - 0.3 * t));
                valve.Step(pipe, 0.002);
            }
            return valve.Cylinder.GaugeBar;
        }
        Assert.Equal(AarControlValveSpec.CylinderVolumeAt(8), new AarControlValveSpec().CylinderVolume, 9);
        var seven = FullService(7);
        var nine = FullService(9);
        // Full service levels the reservoir with the cylinder, so the difference is less than in a light application's.
        Assert.InRange(seven - nine, 0.1, 0.3);
        var lightSeven = FullServiceTo(7, 4.5);
        var lightNine = FullServiceTo(9, 4.5);
        Assert.True(lightSeven - lightNine > 0.15, $"a 0.5 bar reduction gave {lightSeven:F2} bar at 7 in, {lightNine:F2} at 9");
    }

    [Fact]
    public void ACoachsEmergencyGivesAboutASixthMoreThanFullService()
    {
        double Cylinder(double pipeBar)
        {
            var coach = new UicDistributor(UicDistributorSpec.AmericanCoach()) { Mode = BrakingMode.Passenger };
            var pipe = new Chamber(1000, Air.FromGaugeBar(5));
            coach.ChargeTo(pipe.Pressure);
            for (var t = 0.0; t < 30; t += 0.002)
            {
                pipe.Pressure = Air.FromGaugeBar(pipeBar);
                coach.Step(pipe, 0.002);
            }
            return coach.Cylinder.GaugeBar;
        }
        Assert.InRange(Cylinder(3.5), 3.7, 3.9);
        Assert.InRange(Cylinder(0), 4.3, 4.45);
    }
}
