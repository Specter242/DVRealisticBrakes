using System.Collections.Generic;

namespace RealisticBrakes.Core
{
    /// <summary>
    /// A car's brake valve with its reservoirs and cylinder, whichever family:
    /// the European <see cref="UicDistributor"/> or the American
    /// <see cref="AarControlValve"/>.
    /// </summary>
    public interface ICarValve
    {
        /// <summary>The reservoir that fills the cylinder in service.</summary>
        Chamber AuxiliaryReservoir { get; }

        /// <summary>The brake cylinder.</summary>
        Chamber Cylinder { get; }

        /// <summary>Every chamber of the valve.</summary>
        IReadOnlyList<Chamber> Chambers { get; }

        /// <summary>Whether the valve stands applied (out of release).</summary>
        bool Applied { get; }

        /// <summary>Whether the isolating (cut-out) cock has cut the valve out: the car runs unbraked, the pipe passing through.</summary>
        bool Isolated { get; set; }

        /// <summary>The empty/loaded setting.</summary>
        LoadSetting Load { get; set; }

        /// <summary>Full service's cylinder pressure on the loaded setting, from a 5 bar pipe, gauge Pa.</summary>
        double FullServiceCylinder { get; }

        /// <summary>The air taken from the pipe in the last step, kg/s (negative: given to it).</summary>
        double PipeDraw { get; }

        /// <summary>The air put into the cylinder in the last step, kg/s.</summary>
        double FillFlow { get; }

        /// <summary>The air the reservoirs took from the pipe in the last step, kg/s (their charging, for its sound).</summary>
        double ChargeFlow { get; }

        /// <summary>The air let out to the atmosphere in the last step, kg/s.</summary>
        double ExhaustFlow { get; }

        /// <summary>
        /// Sets the empty setting to give <paramref name="cylinder"/> gauge Pa
        /// at full service (<see cref="BrakeRigging.EmptyCylinder"/>).
        /// </summary>
        void SetEmptyCylinder(double cylinder);

        /// <summary>Charges every reservoir to <paramref name="pressure"/> Pa, released.</summary>
        void ChargeTo(double pressure);

 void PullRelease();

        /// <summary>The release valve held open (a long pull): it drains the car's reservoirs too, taking it off air.</summary>
        bool ReleaseHeld { get; set; }

        /// <summary>Advances the valve <paramref name="dt"/> s against its car's section of <paramref name="pipe"/>.</summary>
        void Step(Chamber pipe, double dt);
    }
}
