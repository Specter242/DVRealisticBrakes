using System;

namespace RealisticBrakes.Core
{
    /// <summary>
    /// A part of a car's air brake whose pressure the public API reads (<see cref="CarBrake.GaugePressure"/>). The names
    /// are the API's: they don't change.
    /// </summary>
    public enum BrakePart
    {
        /// <summary>The car's own section of the brake pipe.</summary>
        BrakePipe,

        /// <summary>The auxiliary reservoir: a car's valve's, or an engine's distributor's.</summary>
        AuxiliaryReservoir,

        /// <summary>The control reservoir: a European car's or an engine's distributor's. An American (AB) car has none.</summary>
        ControlReservoir,

        /// <summary>The emergency reservoir of an American (AB) car.</summary>
        EmergencyReservoir,

        /// <summary>The brake cylinder.</summary>
        BrakeCylinder,

        /// <summary>An engine's main reservoir.</summary>
        MainReservoir,

        /// <summary>The equalizing reservoir of an engine's brake valve.</summary>
        EqualizingReservoir,
    }

    /// <summary>The API's part names.</summary>
    public static class BrakeParts
    {
        /// <summary>The part named <paramref name="name"/>, without regard to case; false for any other name.</summary>
        public static bool TryParse(string name, out BrakePart part)
        {
            part = default;
            if (string.IsNullOrEmpty(name) || !char.IsLetter(name[0]))
                return false;
            return Enum.TryParse(name, true, out part) && Enum.IsDefined(typeof(BrakePart), part);
        }
    }
}
