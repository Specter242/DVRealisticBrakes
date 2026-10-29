using RealisticBrakes.Core;
using UnityModManagerNet;

namespace RealisticBrakes
{
    /// <summary>Which family of car brake the model fits.</summary>
    public enum Equipment
    {
        European,
        American,
    }

    /// <summary>How American freight cars' brakes are sized for their load.</summary>
    public enum LoadBraking
    {
        /// <summary>
        /// As in the steam era: no empty/load equipment, so every car brakes
        /// with the force sized to its empty weight, loaded or not (ICS, The
        /// Westinghouse Air-Brake Handbook, 1918).
        /// </summary>
        Period,

        /// <summary>Automatic empty/load equipment, as on modern heavy cars: a loaded car brakes with its loaded force.</summary>
        EmptyLoad,
    }

    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        [Draw("Run the realistic brake (off: the game's own brake runs, from the pressures this one left)")]
        public bool Enabled = true;

        [Draw("Car brake equipment: European (UIC distributors, graduated release, G/P) or American (AB valves, direct release, retainers, emergency reservoirs). Changing it re-reads every car", DrawType.ToggleGroup)]
        public Equipment Equipment = Equipment.European;

        [Draw("Freight cars brake with their own force, not their load's: a loaded car stops slower than an empty one, and each car's empty/loaded valve, set from its load, matters")]
        public bool BrakeForceFromCar = true;

        [Draw("American freight cars' braking for their load: Period (no empty/load equipment, as in the steam era: every car brakes with its empty force, so a loaded train stops much slower; hold it on grades with the retainers) or EmptyLoad (automatic empty/load equipment, as on modern heavy cars)", DrawType.ToggleGroup, VisibleOn = "Equipment|American")]
        public LoadBraking LoadBraking = LoadBraking.Period;

        [Draw("Freight cars' G/P lever as they come into service (passenger coaches and engines run in P)", DrawType.ToggleGroup)]
        public BrakingMode FreightMode = BrakingMode.Goods;

        public bool ManualEmptyLoaded;

        [Draw("Train brake handles gain notches: on diesels, a filling stroke past release (held 0.4 s; it springs back when you let go) and emergency past full service; on steam engines, release before running, which stays, and with American equipment the H-6's holding after running. Takes effect as a cab loads")]
        public bool WidenTrainBrakeHandles = true;

        [Draw("Steam engines' main reservoir gauge gains the equalizing reservoir's hand (blue; the main reservoir's turns red). Takes effect as a cab loads")]
        public bool EqualizingHand = true;

        [Draw("Click the independent brake lever to bail off (in VR, hold it and press the trigger): the engine's own brakes release from a train brake application, the train's stay on")]
        public bool ClickToBailOff = true;

        [Draw("Looking at an engine's train brake, independent brake, or brake valve cut-out shows where it stands by the crosshair (not in VR)")]
        public bool ShowValveText = true;

        [Draw("Show each car's brake pressures for the train you're on")]
        public bool ShowOverlay;

        // The lever bench, a development tool for seating the car levers in
        // game: shown and hidden by this key, unbound unless set in
        // Settings.xml (not in the menu). Nothing it sets is saved; it
        // writes the seats to the log to be built in.
        public KeyBinding DevBenchKey = new KeyBinding();

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            // A guest in a session saves its own model settings, not the host's it is using (Sim.Net).
            var own = Sim.Net.OwnSettings;
            if (own == null)
            {
                Save(this, modEntry);
                return;
            }
            var host = Sim.Net.ModelSettings.From(this);
            own.Value.To(this);
            Save(this, modEntry);
            host.To(this);
        }

        private Equipment drawnEquipment = Equipment.European;

        /// <summary>Notes the equipment as loaded, so only a change of it re-reads the cars.</summary>
        public void AcceptLoaded()
        {
            drawnEquipment = Equipment;
            drawnLevers = (Enabled, Equipment, ManualEmptyLoaded, BrakeForceFromCar);
        }

        // The settings that choose the cars' lever rows, as last fitted: none while the model is off.
        private (bool, Equipment, bool, bool) drawnLevers;

        public void OnChange()
        {
            if (Equipment != drawnEquipment)
            {
                drawnEquipment = Equipment;
                Sim.BrakeSets.Invalidate();
            }
            var levers = (Enabled, Equipment, ManualEmptyLoaded, BrakeForceFromCar);
            if (levers != drawnLevers)
            {
                drawnLevers = levers;
                Controls.CarLevers.RefitAll();
            }
        }
    }
}
