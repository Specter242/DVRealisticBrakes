using System;

namespace RealisticBrakes.Core
{
    /// <summary>The engine brake control a key action works.</summary>
    public enum BrakeKeyControl
    {
        /// <summary>Not a brake control's action.</summary>
        None,

        /// <summary>The train brake (automatic).</summary>
        TrainBrake,

        /// <summary>The independent brake.</summary>
        Independent,

        /// <summary>The brake valve's cut-out.</summary>
        BrakeCutout,
    }

 public static class BrakeKeyActions
    {
        /// <summary>The brake control <paramref name="action"/> works, or <see cref="BrakeKeyControl.None"/>.</summary>
        public static BrakeKeyControl Of(string action)
        {
            if (string.IsNullOrEmpty(action))
                return BrakeKeyControl.None;
            var name = action.Substring(action.LastIndexOf('/') + 1).Trim();
            if (Is(name, "BrakeIncremental") || Is(name, "BrakeAbsolute"))
                return BrakeKeyControl.TrainBrake;
            if (Is(name, "IndependentBrakeIncremental") || Is(name, "IndependentBrakeAbsolute"))
                return BrakeKeyControl.Independent;
            if (Is(name, "BrakeCutoutIncremental") || Is(name, "BrakeCutoutAbsolute") || Is(name, "BrakeCutoutToggle"))
                return BrakeKeyControl.BrakeCutout;
            return BrakeKeyControl.None;
        }

        private static bool Is(string name, string action) => string.Equals(name, action, StringComparison.OrdinalIgnoreCase);
    }
}
