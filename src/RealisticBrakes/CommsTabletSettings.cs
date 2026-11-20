using RealisticBrakes.Controls;

namespace RealisticBrakes
{
 public static class CommsTabletSettings
    {
        private static readonly string[] actions = { "Give brake booklet" };
        private static readonly string[] help = { "Puts " + BrakeBooklet.Title + " in front of you." };

        /// <summary>The buttons, in order: read each time the section is drawn, about twice a second while it's seen.</summary>
        public static string[] Actions() => actions;

        /// <summary>One line under each button.</summary>
        public static string[] ActionHelp() => help;

        /// <summary>
        /// Runs button <paramref name="index"/> on the main thread, exactly as
        /// the settings' "Spawn the brake booklet" does, and returns the notice
        /// it shows; null if nothing happened (no game loaded, no booklet to
        /// copy, or no such button). The booklet lands in front of the player,
        /// whose hand, usually holding the tablet, is left alone.
        /// </summary>
        public static string Act(int index) => index == 0 && BrakeBooklet.Spawn() ? BrakeBooklet.SpawnedNotice : null;
    }
}
