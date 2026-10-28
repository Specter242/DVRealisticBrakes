using System;
using System.Collections.Generic;

namespace RealisticBrakes.Core
{
 public sealed class BrakeLine
    {
        /// <summary>The longest substep, s, whatever the pipe would allow (the valves' small chambers want short steps too).</summary>
        public const double MaxSubstep = 0.004;

        /// <summary>
        /// The most substeps one step takes: 1.6 s of air, enough for a 20 fps
        /// frame at the game's default ten-times setting. A longer hitch is
        /// slowed to this.
        /// </summary>
        public const int MaxSubsteps = 400;

        private readonly CarBrake[] cars;
        private readonly Chamber[] chambers;
        private readonly double[] before;
        private readonly bool[] mainReservoirPiped;
        private readonly double substep;

        /// <summary>
        /// The line through <paramref name="cars"/>, front to rear.
        /// <paramref name="mainReservoirPiped"/>, if given, says for each car
        /// whether it carries the main reservoir pipe: engines share their
        /// main reservoirs through a run of cars that all do, as the game's
        /// <c>BalanceMainReservoirs</c> has it. The hoses' flows carry over
        /// from <paramref name="previous"/> lines these cars ran in.
        /// </summary>
        public BrakeLine(IReadOnlyList<CarBrake> cars, PipeGeometry geometry, IReadOnlyList<bool> mainReservoirPiped = null, IEnumerable<BrakeLine> previous = null)
        {
            if (cars == null || cars.Count == 0)
                throw new ArgumentException("A brake line needs at least one car.", nameof(cars));
            this.cars = new CarBrake[cars.Count];
            var sections = new Chamber[cars.Count];
            var lengths = new double[cars.Count];
            for (var i = 0; i < cars.Count; i++)
            {
                this.cars[i] = cars[i];
                sections[i] = cars[i].PipeSection;
                lengths[i] = cars[i].Length;
            }
            Pipe = new BrakePipe(sections, lengths, geometry);
            if (previous != null)
                foreach (var line in previous)
                    Pipe.CarryFlowsFrom(line?.Pipe);
            this.mainReservoirPiped = new bool[cars.Count];
            if (mainReservoirPiped != null)
                for (var i = 0; i < Math.Min(cars.Count, mainReservoirPiped.Count); i++)
                    this.mainReservoirPiped[i] = mainReservoirPiped[i];
            substep = Math.Min(MaxSubstep, Pipe.MaxStableStep);
            var all = new List<Chamber>();
            foreach (var car in this.cars)
                all.AddRange(car.Chambers);
            chambers = all.ToArray();
            before = new double[chambers.Length];
        }

        /// <summary>
        /// How fast the air was still changing over the last step: the
        /// fastest any chamber's pressure moved, Pa/s. A line standing still
        /// (cars parked, valves lapped, reservoirs level) needn't be run until
        /// something changes.
        /// </summary>
        public double ChangeRate { get; private set; } = double.PositiveInfinity;

        public BrakePipe Pipe { get; }

        public IReadOnlyList<CarBrake> Cars => cars;

        /// <summary>The main reservoir pipe between engines (inferred: a 25 mm hose's worth).</summary>
        public Orifice MainReservoirHose { get; set; } = new Orifice(19);

        /// <summary>Advances every car and the pipe <paramref name="dt"/> s.</summary>
        public void Advance(double dt)
        {
            if (!(dt > 0))
            {
                // Nothing ran: whatever changed hasn't been acted on, so the line mustn't sleep on the old rate.
                ChangeRate = double.PositiveInfinity;
                return;
            }
            var steps = (int)Math.Ceiling(dt / substep);
            if (steps > MaxSubsteps)
            {
                steps = MaxSubsteps;
                dt = steps * substep;
            }
            var h = dt / steps;
            for (var i = 0; i < chambers.Length; i++)
                before[i] = chambers[i].Pressure;
            for (var s = 0; s < steps; s++)
            {
                foreach (var car in cars)
                    car.Step(h);
                Pipe.Step(h);
                ShareMainReservoirs(h);
            }
            var fastest = 0.0;
            for (var i = 0; i < chambers.Length; i++)
                fastest = Math.Max(fastest, Math.Abs(chambers[i].Pressure - before[i]));
            ChangeRate = fastest / dt;
        }

        private void ShareMainReservoirs(double dt)
        {
            LocomotiveBrake last = null;
            for (var i = 0; i < cars.Length; i++)
            {
                if (!mainReservoirPiped[i])
                {
                    last = null;
                    continue;
                }
                var engine = cars[i].Locomotive;
                if (engine == null)
                    continue;
                if (last != null)
                    MainReservoirHose.Pass(last.MainReservoir, engine.MainReservoir, dt);
                last = engine;
            }
        }
    }
}
