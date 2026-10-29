using System;

namespace RealisticBrakes.Core
{
    /// <summary>Where the driver's brake valve handle stands.</summary>
    public enum DriverValvePosition
    {
        /// <summary>A filling stroke: main reservoir air straight into the pipe, leaving it overcharged, to release a long train quickly.</summary>
        Fill,

        /// <summary>Running: the pipe held at its charged pressure, any overcharge let down slowly.</summary>
        Running,

        /// <summary>Service: the pipe held under running by <see cref="DriverBrakeValve.ServiceReduction"/>.</summary>
        Service,

        /// <summary>Lap, on a manual-lap valve: the equalizing reservoir held where it stands, and the pipe with it.</summary>
        Lap,

        /// <summary>
        /// Service on a manual-lap valve: the equalizing reservoir let down
        /// through the service exhaust for as long as the handle stays, the
        /// driver lapping it at the reduction wanted.
        /// </summary>
        Apply,

        /// <summary>Emergency: the pipe opened wide to the air.</summary>
        Emergency,

        /// <summary>
        /// Holding, on the H-6: the pipe fed as in running, while the engine's
        /// own automatic brake is held applied as the train releases and
        /// recharges (Wood, The Westinghouse E-T Air Brake Instruction Pocket
        /// Book, p. 140; ICS, The Westinghouse Air-Brake Handbook, 1918,
        /// p. 140).
        /// </summary>
        Holding,
    }

    /// <summary>
    /// The sizes and settings of a self-lapping driver's brake valve, after
    /// the UIC valve Pugi et al. (2004) model. Bores not given by a source
    /// are inferred and checked by the tests.
    /// </summary>
    public sealed class DriverBrakeValveSpec
    {
        /// <summary>The equalizing reservoir, m³: the valve's reference for the pipe (inferred).</summary>
        public double EqualizingVolume { get; set; } = 0.005;

        /// <summary>The pipe's running pressure, gauge Pa: 5 bar (UIC 540).</summary>
        public double Running { get; set; } = 5.0 * Air.Bar;

        /// <summary>The deepest service reduction, Pa: 1.5 bar to full service.</summary>
        public double FullService { get; set; } = 1.5 * Air.Bar;

        /// <summary>
        /// The least service reduction a self-lapping handle makes, Pa: none
        /// on the European valve. The American 26-L's first notch is a
        /// minimum reduction of about 6–8 psi, as an AB train needs one that
        /// deep to apply and stay applied.
        /// </summary>
        public double MinimumReduction { get; set; }

        /// <summary>
        /// The low-pressure overcharge a filling stroke leaves, Pa (inferred;
        /// Pugi et al.'s rapid charging position overfeeds the pipe to 6.5
        /// bar absolute while held).
        /// </summary>
        public double Overcharge { get; set; } = 0.4 * Air.Bar;

        /// <summary>
        /// How far over running a filling stroke can drive the pipe at the
        /// engine, Pa (inferred). Held too long, it still overcharges the
        /// front cars' control reservoirs, and they drag when it drops.
        /// </summary>
        public double FillLimit { get; set; } = 1.0 * Air.Bar;

        /// <summary>
        /// How fast the overcharge is let down in running, Pa/s (inferred:
        /// 0.4 bar in 4 minutes, well under the 0.3 bar a minute the cars'
        /// valves ignore).
        /// </summary>
        public double OverchargeLetDown { get; set; } = 0.4 * Air.Bar / 240;

 public bool LetsDownRelease { get; set; }

 public bool FillingStroke { get; set; } = true;

        /// <summary>
        /// Whether the relay feeds the pipe in lap and in a manual-lap service,
        /// holding it to the equalizing reservoir against leaks, as self-lapping
        /// valves do. The H-6 doesn't: lapped, "pressure is no longer being
        /// supplied to the brake pipe and there is no further reduction of the
        /// latter except from leakage" (Wood p. 143), and in service its
        /// discharge valve only exhausts (p. 154).
        /// </summary>
        public bool MaintainsInLap { get; set; } = true;

        /// <summary>
        /// The H-6's warning port: in release the feed valve's air blows
        /// through it to the air, "giving warning of the possibility of brake
        /// pipe overcharge" (Wood p. 161). Closed on the other valves. Its
        /// bore is inferred, for a blow about a quarter as loud as the game's
        /// loudest exhaust.
        /// </summary>
        public Orifice WarningPort { get; set; } = new Orifice(0);

        /// <summary>The equalizing reservoir's charging port from the main reservoir.</summary>
        public Orifice EqualizingCharge { get; set; } = new Orifice(1.2);

        /// <summary>The equalizing reservoir's service exhaust, which sets the service rate (inferred: 1.5 bar in about 5 s).</summary>
        public Orifice EqualizingExhaust { get; set; } = new Orifice(1.4);

        /// <summary>The relay valve's supply from the main reservoir to the pipe, wide open.</summary>
        public Orifice RelaySupply { get; set; } = new Orifice(8.0);

        /// <summary>The relay valve's exhaust from the pipe, wide open: about 15 % of the emergency port's area (Melzi and Grasso 2018).</summary>
        public Orifice RelayExhaust { get; set; } = new Orifice(9.7);

        /// <summary>The emergency port (inferred: a 25 mm bore).</summary>
        public Orifice EmergencyPort { get; set; } = new Orifice(25.0);

        /// <summary>The filling stroke's port from the main reservoir to the pipe.</summary>
        public Orifice FillPort { get; set; } = new Orifice(12.0);

        /// <summary>The pipe-to-reservoir difference, Pa, over which the relay opens from shut to wide (inferred).</summary>
        public double RelayBand { get; set; } = 0.1 * Air.Bar;

        /// <summary>The difference, Pa, the relay ignores (inferred).</summary>
        public double RelayDeadBand { get; set; } = 0.005 * Air.Bar;

        /// <summary>A European valve: a filling stroke leaves a low-pressure overcharge, let down slowly.</summary>
        public static DriverBrakeValveSpec Uic() => new DriverBrakeValveSpec();

        /// <summary>
        /// An American valve (the 26-L self-lapping, or the H-6 of the 6-ET's
        /// Release, Running, Lap, Service, and Emergency): release feeds main
        /// reservoir air straight in, and running lets what it put in bleed
        /// away slowly, so the cars' reservoirs follow it down without
        /// applying. The 26-L has no filling stroke. Its first service notch
        /// is a 0.45 bar minimum reduction: lighter, the pipe's rebound along
        /// a long train released most of its cars (a second review,
        /// 2026-10-24).
        /// </summary>
        public static DriverBrakeValveSpec American() => new DriverBrakeValveSpec
        {
            Overcharge = 0,
            MinimumReduction = 0.45 * Air.Bar,
            LetsDownRelease = true,
            FillingStroke = false,
        };

        /// <summary>
        /// The H-6 automatic brake valve of the E-T equipment, on an American
        /// steam engine: release, running, holding, lap, service, and emergency.
        /// Its equalizing reservoir is 812 cu in, 13.3 L (ICS p. 370), and falls
        /// in service "at the rate of about 5 pounds for each 2 seconds" (Wood
        /// p. 154), about 0.17 bar/s, so a 1.5 bar reduction takes 8 to 9 s
        /// (the ports set by the tests). Lapped or in service it doesn't feed
        /// the pipe.
        /// </summary>
        public static DriverBrakeValveSpec H6()
        {
            var spec = American();
            spec.EqualizingVolume = 0.0133;
            spec.EqualizingExhaust = new Orifice(H6ServiceBore);
            spec.EqualizingCharge = new Orifice(2.0);
            spec.MaintainsInLap = false;
            spec.WarningPort = new Orifice(WarningBore);
            return spec;
        }

        /// <summary>The H-6's warning port bore, mm (inferred).</summary>
        public const double WarningBore = 1.3;

        /// <summary>The H-6's service exhaust bore, mm (set by the tests to its 0.17 bar/s).</summary>
        public const double H6ServiceBore = 1.85;
    }

    /// <summary>
    /// A self-lapping driver's brake valve: the handle sets the equalizing
    /// reservoir, and a relay valve feeds the pipe from the main reservoir
    /// or exhausts it until the pipe at the engine matches. On a long train
    /// the relay's capacity and the pipe's friction, not the handle, set how
    /// fast the rear follows.
    /// </summary>
    public sealed class DriverBrakeValve
    {
        private readonly DriverBrakeValveSpec spec;

        public DriverBrakeValve(DriverBrakeValveSpec spec = null)
        {
            this.spec = spec ?? new DriverBrakeValveSpec();
            EqualizingReservoir = new Chamber(this.spec.EqualizingVolume);
        }

        public DriverBrakeValveSpec Spec => spec;

        public Chamber EqualizingReservoir { get; }

        public DriverValvePosition Position { get; set; } = DriverValvePosition.Running;

        /// <summary>The reduction under running asked for in <see cref="DriverValvePosition.Service"/>, Pa, up to full service.</summary>
        public double ServiceReduction { get; set; }

        /// <summary>Whether the valve is cut in. A cut-out valve leaves the pipe alone, as on a trailing engine.</summary>
        public bool CutIn { get; set; } = true;

        /// <summary>The overcharge still to let down, Pa.</summary>
        public double OverchargeLeft { get; private set; }

        /// <summary>The air fed into the pipe in the last step, kg/s.</summary>
        public double FeedFlow { get; private set; }

        /// <summary>The air let out of the pipe in the last step, kg/s.</summary>
        public double ExhaustFlow { get; private set; }

        /// <summary>The pressure the equalizing reservoir is set to, Pa absolute.</summary>
        public double EqualizingTarget
        {
            get
            {
                var running = Air.Atmosphere + spec.Running;
                switch (Position)
                {
                    case DriverValvePosition.Fill:
                        return running + Math.Max(spec.Overcharge, OverchargeLeft);
                    case DriverValvePosition.Service:
                        return running - Math.Max(spec.MinimumReduction, Math.Min(spec.FullService, ServiceReduction));
                    case DriverValvePosition.Emergency:
                    case DriverValvePosition.Apply:
                        return Air.Atmosphere;
                    case DriverValvePosition.Lap:
                        return EqualizingReservoir.Pressure;
                    default:
                        // Running, and the H-6's holding, which feeds the pipe as running does.
                        return running + OverchargeLeft;
                }
            }
        }

        /// <summary>Sets the overcharge still to let down, Pa, as saved.</summary>
        public void RestoreOvercharge(double overcharge) =>
            OverchargeLeft = Math.Max(0, Math.Min(Math.Max(spec.Overcharge, spec.LetsDownRelease ? spec.FillLimit : 0), overcharge));

        /// <summary>Charges the equalizing reservoir to running, as on an engine found charged.</summary>
        public void ChargeTo(double pressure)
        {
            EqualizingReservoir.Pressure = pressure;
            OverchargeLeft = 0;
        }

        /// <summary>Advances the valve <paramref name="dt"/> s between <paramref name="mainReservoir"/> and its engine's section of <paramref name="pipe"/>.</summary>
        public void Step(Chamber mainReservoir, Chamber pipe, double dt)
        {
            FeedFlow = 0;
            ExhaustFlow = 0;
            if (!CutIn)
                return;

            var running = Air.Atmosphere + spec.Running;
            var er = EqualizingReservoir;
            switch (Position)
            {
                case DriverValvePosition.Fill:
                    if (spec.LetsDownRelease)
                    {
                        // The equalizing reservoir charges with the pipe, so running lets down what release put in from where it stands.
                        spec.FillPort.PassOneWay(pipe, er, dt, running + spec.FillLimit);
                        OverchargeLeft = Math.Max(spec.Overcharge, Math.Min(spec.FillLimit, er.Pressure - running));
                    }
                    else
                        OverchargeLeft = spec.Overcharge;
                    break;
                case DriverValvePosition.Running:
                case DriverValvePosition.Holding:
                    OverchargeLeft = Math.Max(0, OverchargeLeft - spec.OverchargeLetDown * dt);
                    break;
                case DriverValvePosition.Lap when spec.LetsDownRelease:
                    // Lapped, an overcharge stands with the equalizing reservoir, and running lets it down from there.
                    OverchargeLeft = Math.Max(0, Math.Min(spec.FillLimit, er.Pressure - running));
                    break;
                default:
                    OverchargeLeft = 0;
                    break;
            }

            var target = EqualizingTarget;
            if (er.Pressure < target)
                spec.EqualizingCharge.PassOneWay(mainReservoir, er, dt, target);
            else if (er.Pressure > target)
            {
                var port = Position == DriverValvePosition.Emergency ? spec.EmergencyPort : spec.EqualizingExhaust;
                port.Vent(er, dt, target);
            }

            var pipeBefore = pipe.Mass;
            if (Position == DriverValvePosition.Emergency)
            {
                spec.EmergencyPort.Vent(pipe, dt);
            }
            else
            {
                if (Position == DriverValvePosition.Fill)
                    spec.FillPort.PassOneWay(mainReservoir, pipe, dt, Air.Atmosphere + spec.Running + spec.FillLimit);
                // The H-6 lapped or in service only exhausts the pipe; it feeds it in running, holding, and release.
                var supply = spec.MaintainsInLap || (Position != DriverValvePosition.Lap && Position != DriverValvePosition.Apply);
                Relay(mainReservoir, pipe, dt, exhaust: Position != DriverValvePosition.Fill, supply);
            }
            var change = (pipe.Mass - pipeBefore) / dt;
            if (change > 0)
                FeedFlow = change;
            else
                ExhaustFlow = -change;

            // The H-6's warning port blows the feed valve's air away for as long as the handle stands in release.
            if (Position == DriverValvePosition.Fill && spec.WarningPort.IsOpen)
            {
                var blown = spec.WarningPort.MassFlow(Math.Min(mainReservoir.Pressure, running), Air.Atmosphere) * dt;
                blown = Math.Min(blown, Math.Max(0, -mainReservoir.MassToReach(Air.Atmosphere)));
                mainReservoir.Mass -= blown;
                ExhaustFlow += blown / dt;
            }
        }

        private void Relay(Chamber mainReservoir, Chamber pipe, double dt, bool exhaust, bool supply = true)
        {
            var reference = EqualizingReservoir.Pressure;
            var error = pipe.Pressure - reference;
            var opening = (Math.Abs(error) - spec.RelayDeadBand) / spec.RelayBand;
            if (opening <= 0)
                return;
            if (error < 0)
            {
                if (supply)
                    spec.RelaySupply.Opened(opening).PassOneWay(mainReservoir, pipe, dt, reference);
            }
            else if (exhaust)
                spec.RelayExhaust.Opened(opening).Vent(pipe, dt, reference);
        }
    }
}
