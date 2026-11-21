using RealisticBrakes.Core;
using UnityEngine;
using UnityModManagerNet;

namespace RealisticBrakes
{
    /// <summary>Which family of car brake the model fits.</summary>
    public enum Equipment
    {
        European,
        American,

 Vacuum,
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

    // The settings, in the order the mod's window and the Comms Tablet show them: the brake model, the cab, the display,
    // and the keys last. Labels stay short; the rest is each one's tooltip. Moving a field doesn't break a saved
    // Settings.xml, which is read by name; renaming one does.
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        // The model settings a multiplayer host gives its guests (Sim.Net.ModelSettings) say so in their tooltips.
        private const string HostApplies = "\nIn multiplayer, the host's setting is used.";

        [Header("Brake model")]
        [Draw("Run the realistic brake", Tooltip = "Off: the game's own brake runs, from the pressures this one left." + HostApplies)]
        public bool Enabled = true;

        [Draw("Brake equipment", DrawType.ToggleGroup, Tooltip = "For the cars, and for the engines whose own isn't known (or every engine, with\nthe setting below). European: UIC distributors, graduated release, G/P; the\nengines keep a self-lapping independent. American: AB valves, direct release,\nretainers, emergency reservoirs; manual-lap engines (steamers, DM3) get the\nE-T (H-6 valve, S-6 independent), self-lapping engines the 26-L. Vacuum, every\nengine and car: British practice at 21 in, graduated both ways, slow on long\ntrains; steam engines have ejectors and a steam brake, their air pump shut and\nits handwheel blocked, the small ejector running while there's steam; diesels\nhave exhausters. Changing it re-reads every car." + HostApplies)]
        public Equipment Equipment = Equipment.European;

        [Draw("Every engine uses this equipment", InvisibleOn = "Equipment|Vacuum", Tooltip = "Off: each engine has its own prototype's: the S282, S060, and the American\ncustom engines American, the stock diesels and the BR95 European, the rest the\nsetting. The cars always use the setting." + HostApplies)]
        public bool EveryEngineUsesSetting;

        [Draw("Vacuum cars have direct admission valves", VisibleOn = "Equipment|Vacuum", Tooltip = "They let air straight into each cylinder, so a long train brakes in about half a\nminute (BR from the 1950s). Off, as before them, the rear of a long train is\nunbraked for some 20 s and takes about two minutes." + HostApplies)]
        // Synced in multiplayer whatever the build, so the session's packets read alike.
        public bool DirectAdmission = true;

        [Draw("Freight cars' braking for their load", DrawType.ToggleGroup, InvisibleOn = "Equipment|European", Tooltip = "American and vacuum freight cars. Period (steam era): no empty/load valve;\nevery car brakes with its empty force, so loaded trains stop slower (use\nretainers on American grades). EmptyLoad (modern automatic): loaded cars brake\nwith their loaded force." + HostApplies)]
        public LoadBraking LoadBraking = LoadBraking.Period;

        [Draw("Freight cars brake with their own force", Tooltip = "Not their load's: a loaded car stops slower than an empty one, and each car's\nempty/loaded valve, set from its load, matters." + HostApplies)]
        public bool BrakeForceFromCar = true;

        [Draw("Freight cars' G/P lever as they come into service", DrawType.ToggleGroup, Tooltip = "Passenger coaches and engines run in P." + HostApplies)]
        public BrakingMode FreightMode = BrakingMode.Goods;

        public bool ManualEmptyLoaded;

        [Draw("Power cut by the brakes on diesels and electrics", Tooltip = "European engines lose power while the brake pipe is under 3.5 bar, American ones\nwhen their PC switch opens on an emergency. Power comes back once the pipe has\nrecharged and the throttle is closed.")]
        public bool TractionCut = true;

        [Header("Cab")]
        [Draw("Train brake handles gain notches", Tooltip = "Self-lapping handles gain a filling stroke past release (held 0.4 s; springs\nback when let go) and emergency past full service. Manual-lap handles (steam\nengines, DM3) gain release before running, and H-6 holding on American engines.\nTakes effect as a cab loads." + HostApplies)]
        public bool WidenTrainBrakeHandles = true;

        [Draw("Click the independent brake to bail off", Tooltip = "In VR, hold the lever and press the trigger. The engine's own brakes release\nfrom a train brake application; the train's stay on.")]
        public bool ClickToBailOff = true;

        [Draw("Equalizing reservoir hand on the main reservoir gauge", Tooltip = "On engines with a manual-lap brake valve: the steam engines, the DM3, and\ncustom engines such as the FPD-4. The gauge gains the equalizing reservoir's\nhand, blue; the main reservoir's turns red. A gauge with an equalizing hand of\nits own keeps it, and it reads the equalizing reservoir. Takes effect as a cab\nloads.")]
        public bool EqualizingHand = true;

        [Draw("Vacuum ejectors' volume, %", DrawType.Slider, Min = 0, Max = 200, VisibleOn = "Equipment|Vacuum", Tooltip = "The steam engines' vacuum ejectors, in the cab and outside. 100 is the mod's own\nlevel; 0 silences them. Takes effect at once.")]
        public int EjectorVolume = 100;

        [Header("Display")]
        [Draw("Brake handle positions by the crosshair", Tooltip = "Looking at an engine's train brake, independent brake, or brake valve cut-out,\nor a car's brake levers and bleed handle, shows where it stands (not in VR).\nBailing off, reservoir draining, and the booklet button show a notice. Off, the\nnotices go to the log only.")]
        public bool ShowValveText = true;

        [Draw("Brake pressure overlay", Tooltip = "A box on the screen with each car's brake pressures, for the train you're on.")]
        public bool ShowOverlay;

        [Header("Keys")]
        [Draw("Bail-off key", Tooltip = "Bails off the engine you're on, as the click does. Unbound until you set it.")]
        public KeyBinding BailOffKey = new KeyBinding();

        [Draw("Angleichen key (European brake valves)", Tooltip = "While held on the engine you're on, slowly raises the brake pipe to at most\n5.5 bar, to free brakes that stick at 5.0; let go, it comes down at 0.1 bar a\nminute. Unbound until you set it.")]
        public KeyBinding AdjustKey = new KeyBinding();


        public override void Save(UnityModManager.ModEntry modEntry)
        {
            // A guest in a session saves its own model settings, not the host's it is using (Sim.Net).
            var own = Sim.Net.OwnSettings;
            if (own == null)
            {
                Save(this, modEntry);
                return;
            }
            var host = Sim.Net.HostSettings ?? Sim.Net.ModelSettings.From(this);
            own.Value.To(this);
            Save(this, modEntry);
            host.To(this);
        }

        private Equipment drawnEquipment = Equipment.European;
        private bool drawnEveryEngine;
        private Sim.Net.ModelSettings drawnModel;

        /// <summary>Notes the equipment as loaded, so only a change of it re-reads the cars.</summary>
        public void AcceptLoaded()
        {
            drawnEquipment = Equipment;
            drawnEveryEngine = EveryEngineUsesSetting;
            drawnLevers = (Enabled, Equipment, ManualEmptyLoaded, BrakeForceFromCar);
            drawnModel = Sim.Net.ModelSettings.From(this);
        }

        // The settings that choose the cars' lever rows, as last fitted: none while the model is off.
        private (bool, Equipment, bool, bool) drawnLevers;

        public void OnChange()
        {
            if (Sim.Net.IsGuest)
            {
                // Enforce host-owned model settings for the entire guest session (including edits from UMM or external UIs),
                // reverting them before any local rebuild while letting client-only settings apply and save freely.
                var host = Sim.Net.HostSettings ?? drawnModel;
                if (!host.SameAs(Sim.Net.ModelSettings.From(this)))
                    host.To(this);
            }
            if (Equipment != drawnEquipment || EveryEngineUsesSetting != drawnEveryEngine)
            {
                drawnEquipment = Equipment;
                drawnEveryEngine = EveryEngineUsesSetting;
                Sim.BrakeSets.Invalidate();
                Sim.TractionCuts.Reset();
            }
            if (!Enabled || !TractionCut)
                Sim.TractionCuts.ReleaseAll();
            var levers = (Enabled, Equipment, ManualEmptyLoaded, BrakeForceFromCar);
            if (levers != drawnLevers)
            {
                drawnLevers = levers;
                Controls.CarLevers.RefitAll();
            }
            var model = Sim.Net.ModelSettings.From(this);
            if (!model.SameAs(drawnModel))
            {
                drawnModel = model;
                if (!Sim.Net.IsGuest)
                    Sim.Net.NotifyHostSettingsChanged();
            }
        }
    }
}
