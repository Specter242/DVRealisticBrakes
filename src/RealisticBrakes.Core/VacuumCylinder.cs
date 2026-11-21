using System;
using System.Collections.Generic;

namespace RealisticBrakes.Core
{
 public sealed class VacuumCylinderSpec
    {
        /// <summary>The cylinder's bore, m: 18 in on a four-wheel wagon (LMS 1946; Hunter 1962).</summary>
        public double Diameter { get; set; } = 18 * 0.0254;

        /// <summary>The piston's stroke that brings the blocks on, m: 5 in (Hunter 1962, Fig. 33; Roberts 1966: 4½ to 6 in).</summary>
        public double Stroke { get; set; } = 5 * 0.0254;

        /// <summary>
        /// The chamber above the piston, the piston down, m³: 93 L for an 18 in
        /// combined cylinder (Mohan 1964). Roberts 1966's stroke figures give
        /// the same: 20 in falls to 17½ in at a 4½ in stroke.
        /// </summary>
        public double ChamberVolume { get; set; } = 0.093;

        /// <summary>The space under the piston, the piston down, with its branch, m³ (inferred).</summary>
        public double UndersideVolume { get; set; } = 0.003;

        /// <summary>The branch from the train pipe to the underside: ¾ in, without reducing washers (Arnold 1934).</summary>
        public Orifice Branch { get; set; } = new Orifice(0.75 * 25.4);

        /// <summary>The ball valve's passage from the chamber to the underside: 7.1 mm (Mohan 1964).</summary>
        public Orifice BallValve { get; set; } = new Orifice(7.1);

        /// <summary>The lead the chamber needs over the underside to lift the ball, Pa: 18 mm of mercury (Mohan 1964).</summary>
        public double BallLift { get; set; } = 18 / 25.4 * Vacuum.Inch;

        /// <summary>The piston's weight as a difference across it, Pa: it falls under its own weight in release (inferred: an inch).</summary>
        public double PistonWeight { get; set; } = 1 * Vacuum.Inch;

        /// <summary>
        /// The release valve's port from the atmosphere into the chamber (the
        /// cord; BR's, Marsh 1952): sized so a 21 in bogie wagon releases in
        /// about 10 s by hand (Gresham 1928: 22 s, then 10 with an improved
        /// valve).
        /// </summary>
        public Orifice ReleaseValve { get; set; } = new Orifice(12);

        /// <summary>Whether the car has a direct admission (DA) valve (the setting `DirectAdmission`).</summary>
        public bool DirectAdmission { get; set; } = true;

        /// <summary>
        /// The DA valve's inlet from the atmosphere to the underside (inferred).
        /// With the valve fitted, the underside reaches the pipe only through a
        /// non-return valve, out to the pipe: "air entering the train pipe
        /// simply actuates the D.A. valve, and does not enter the brake
        /// cylinder" (LMS 1946 pp. 7–8).
        /// </summary>
        public Orifice DirectAdmissionInlet { get; set; } = new Orifice(12.7);

 public double DirectAdmissionLag { get; set; } = 0.25 * Vacuum.Inch;

        /// <summary>The piston's area, m².</summary>
        public double Area => Math.PI * Diameter * Diameter / 4;

        /// <summary>A four-wheel wagon's 18 in cylinder.</summary>
        public static VacuumCylinderSpec Wagon() => new VacuumCylinderSpec();

        /// <summary>A bogie wagon's 21 in cylinder, its chamber in the same proportion to its area.</summary>
        public static VacuumCylinderSpec Bogie()
        {
            var spec = new VacuumCylinderSpec { Diameter = 21 * 0.0254 };
            spec.ChamberVolume *= (21.0 / 18) * (21.0 / 18);
            return spec;
        }

        /// <summary>
        /// A bogie coach's two 18 in cylinders (LMS 1946), worked together on
        /// one branch: one cylinder of their combined area, chambers, and ports.
        /// </summary>
        public static VacuumCylinderSpec Coach()
        {
            var two = Math.Sqrt(2);
            var spec = new VacuumCylinderSpec();
            spec.Diameter *= two;
            spec.ChamberVolume *= 2;
            spec.UndersideVolume *= 2;
            spec.BallValve = spec.BallValve.Scaled(2);
            spec.ReleaseValve = spec.ReleaseValve.Scaled(2);
            spec.DirectAdmissionInlet = spec.DirectAdmissionInlet.Scaled(2);
            return spec;
        }
    }

 public sealed class VacuumCylinder : ICarValve
    {
        private readonly VacuumCylinderSpec spec;
        private readonly Chamber chamber;
        private readonly Chamber underside;
        private readonly Chamber equivalent;
        private readonly Chamber[] chambers;
        private readonly double fullForce;
        private double travel;
        private bool pulling;
        private double emptyShare = 1;

        public VacuumCylinder(VacuumCylinderSpec spec = null)
        {
            this.spec = spec ?? new VacuumCylinderSpec();
            chamber = new Chamber(this.spec.ChamberVolume);
            underside = new Chamber(this.spec.UndersideVolume);
            equivalent = new Chamber(0.001);
            chambers = new[] { chamber, underside };
            fullForce = ForceAt(Air.Atmosphere, ChamberAtStroke(Math.Min(Air.Atmosphere, Vacuum.FromInches(Vacuum.Working) + this.spec.BallLift)));
        }

        public VacuumCylinderSpec Spec => spec;

 public Chamber Chamber => chamber;

        /// <summary>The space under the piston, on the train pipe's branch.</summary>
        public Chamber Underside => underside;

 public Chamber AuxiliaryReservoir => chamber;

        /// <summary>The air cylinder that would give the same share of the car's force, for the game (gauge: its stock take-up and span).</summary>
        public Chamber Cylinder => equivalent;

        public IReadOnlyList<Chamber> Chambers => chambers;

 public double Travel => travel;

        /// <summary>The force on the blocks, N: the difference across the piston less its weight, once it is at its stroke.</summary>
        public double Force { get; private set; }

        /// <summary>The force with 21 in in the chamber and the underside open to the air, N.</summary>
        public double FullForce => fullForce;

        /// <summary>The share of <see cref="FullForce"/> the blocks bear now, on the car's load setting.</summary>
        public double ForceShare { get; private set; }

        /// <summary>Whether the piston is lifted off its seat.</summary>
        public bool Applied => travel > 0;

        /// <summary>Whether the brake is isolated: the branch is shut and the car runs unbraked.</summary>
        public bool Isolated { get; set; }

        public LoadSetting Load { get; set; }

        /// <summary>The air cylinder that gives the full force: the game's braking factor reaches 1 at it.</summary>
        public double FullServiceCylinder => BrakeRigging.StockTakeUp + BrakeRigging.StockSpan;

        public double PipeDraw { get; private set; }

        public double FillFlow { get; private set; }

 public double ChargeFlow { get; private set; }

        /// <summary>The air let in from the atmosphere in the last step (the DA valve and the cord), kg/s.</summary>
        public double ExhaustFlow { get; private set; }

        /// <summary>Whether the cord is pulled and held, which lets the chamber up to the atmosphere.</summary>
        public bool ReleaseHeld { get; set; }

        /// <inheritdoc/>
        public double IndependentCylinder { get; set; }

        public void SetEmptyCylinder(double cylinder) => emptyShare = BrakeRigging.StockBrakingFactor(cylinder);

        /// <summary>Charges the car to the pipe's <paramref name="pressure"/> Pa, released: the chamber stands the ball's lift behind it.</summary>
        public void ChargeTo(double pressure)
        {
            SetPiston(0);
            underside.Pressure = pressure;
            chamber.Pressure = Math.Min(Air.Atmosphere, pressure + spec.BallLift);
            pulling = false;
            UpdateForce();
        }

 public void Restore(double chamberPressure, double undersidePressure)
        {
            SetPiston(undersidePressure - chamberPressure > spec.PistonWeight ? spec.Stroke : 0);
            chamber.Pressure = chamberPressure;
            underside.Pressure = undersidePressure;
            BalancePiston();
            UpdateForce();
        }

        /// <summary>A short pull on the cord: air into the chamber until the piston falls, and the car stays released.</summary>
        public void PullRelease() => pulling = true;

        public void Step(Chamber pipe, double dt)
        {
            var drawn = 0.0;
            var admitted = 0.0;
            var charged = 0.0;
            if (!Isolated)
            {
                if (spec.DirectAdmission)
                {
                    var lead = pipe.Pressure - underside.Pressure;
                    if (lead > spec.DirectAdmissionLag)
                    {
                        var limit = underside.MassToReach(pipe.Pressure - spec.DirectAdmissionLag);
                        var inflow = Math.Min(spec.DirectAdmissionInlet.MassFlow(Air.Atmosphere, underside.Pressure) * dt, Math.Max(0, limit));
                        underside.Mass += inflow;
                        admitted += inflow;
                    }
                    drawn = -spec.Branch.PassOneWay(underside, pipe, dt);
                }
                else
                {
                    // The underside "is always in communication with the train pipe" (LMS 1946 p.6).
                    drawn = spec.Branch.Pass(pipe, underside, dt);
                }
                charged = PassBall(dt);
            }
            if (ReleaseHeld || pulling)
            {
                var inflow = -spec.ReleaseValve.OpenToAtmosphere(chamber, dt);
                admitted += Math.Max(0, inflow);
            }
            BalancePiston();
            UpdateForce();
            if (pulling && !ReleaseHeld && travel <= 0)
                pulling = false;

            PipeDraw = drawn / dt;
            FillFlow = Math.Max(0, drawn + admitted) / dt;
            ChargeFlow = charged / dt;
            ExhaustFlow = admitted / dt;
        }

        // The ball lifts while the chamber leads the underside by its lift, and lets the chamber down to that lead.
        private double PassBall(double dt)
        {
            var lead = chamber.Pressure - (underside.Pressure + spec.BallLift);
            if (lead <= 0)
                return 0;
            var moved = spec.BallValve.MassFlow(chamber.Pressure, underside.Pressure + spec.BallLift) * dt;
            var level = lead * chamber.Volume * underside.Volume / ((chamber.Volume + underside.Volume) * Air.RT);
            moved = Math.Min(moved, level);
            chamber.Mass -= moved;
            underside.Mass += moved;
            return moved;
        }

        // The piston floats where the difference across it bears its weight, between its seat and its stroke.
        private void BalancePiston()
        {
            var stroke = spec.Area * spec.Stroke;
            var below = spec.UndersideVolume;
            var above = spec.ChamberVolume;
            var weight = spec.PistonWeight / Air.RT;
            var mBelow = underside.Mass;
            var mAbove = chamber.Mass;
            // The lift's pressure lead at the seat, as a mass: c > 0 means the piston rises.
            var c = mBelow * above - mAbove * below - weight * below * above;
            double y;
            if (c <= 0)
                y = 0;
            else
            {
                // k·y² − (mBelow + mAbove + k·(above − below))·y + c = 0, its smaller root in the stable form.
                var b = mBelow + mAbove + weight * (above - below);
                var disc = Math.Max(0, b * b - 4 * weight * c);
                y = 2 * c / (b + Math.Sqrt(disc));
            }
            SetPiston(Math.Min(stroke, y) / spec.Area);
        }

        private void SetPiston(double newTravel)
        {
            travel = Math.Max(0, Math.Min(spec.Stroke, newTravel));
            var swept = spec.Area * travel;
            underside.Displace(spec.UndersideVolume + swept);
            chamber.Displace(spec.ChamberVolume - swept);
        }

        private void UpdateForce()
        {
            var atStroke = travel >= spec.Stroke * (1 - 1e-9);
            Force = Isolated || !atStroke ? 0 : ForceAt(underside.Pressure, chamber.Pressure);
            var share = Force / fullForce;
            if (Load == LoadSetting.Empty)
                share *= emptyShare;
            ForceShare = share;
            equivalent.Pressure = Air.Atmosphere + (share > 0 ? BrakeRigging.StockTakeUp + share * BrakeRigging.StockSpan : 0);
        }

        private double ForceAt(double below, double above) => Math.Max(0, (below - above - spec.PistonWeight) * spec.Area);

        // The chamber's pressure, from <paramref name="pressure"/> with the piston down, once the piston is at its stroke.
        private double ChamberAtStroke(double pressure) => pressure * spec.ChamberVolume / (spec.ChamberVolume - spec.Area * spec.Stroke);
    }
}
