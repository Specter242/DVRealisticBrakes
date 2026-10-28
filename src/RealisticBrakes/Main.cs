using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;
using Object = UnityEngine.Object;

namespace RealisticBrakes
{
    public static class Main
    {
        public static UnityModManager.ModEntry Mod { get; private set; }

        public static Settings Settings { get; private set; }

        /// <summary>The mod's patch classes that failed to apply this session, by name.</summary>
        internal static IReadOnlyList<string> FailedPatches { get; private set; } = new string[0];

        /// <summary>The optional patch classes that failed to apply this session, which the brake runs without.</summary>
        internal static ICollection<Type> FailedOptionalPatches { get; } = new HashSet<Type>();

        /// <summary>Whether the model runs the brake: the setting, and every patch in place.</summary>
        public static bool Active => Settings != null && Settings.Enabled && FailedPatches.Count == 0;

        // Entry method named in Info.json. Patches stay applied until the game
        // restarts; the setting switches the model on and off, the stock brake
        // taking over from the pressures it left.
        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Mod = modEntry;
            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            Settings.AcceptLoaded();
            modEntry.OnGUI = _ =>
            {
                Settings.Draw(modEntry);
            };
            modEntry.OnSaveGUI = _ => Settings.Save(modEntry);

            var harmony = new Harmony(modEntry.Info.Id);
            var failed = new List<string>();
            foreach (var type in LoadableTypes().Where(t => t.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0))
            {
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception e)
                {
                    var message = $"Patch {type.FullName} failed: {e.GetBaseException().GetType().Name}: {e.GetBaseException().Message}";
                    if (type.IsDefined(typeof(OptionalPatchAttribute), false))
                    {
                        // A convenience beside the brake (the bail click, the widened handles): the brake runs without it.
                        FailedOptionalPatches.Add(type);
                        modEntry.Logger.Warning(message);
                        continue;
                    }
                    failed.Add(type.FullName);
                    modEntry.Logger.Error(message);
                }
            }
            FailedPatches = failed;
            if (failed.Count > 0)
            {
                // Half a brake is worse than the stock one: undo ours and leave the game's.
                harmony.UnpatchAll(modEntry.Info.Id);
                modEntry.Logger.Error("Realistic Brakes stands aside: the stock brake runs.");
                return true;
            }
            var multiplayer = UnityModManager.FindMod("Multiplayer");
            if (multiplayer != null && multiplayer.Enabled)
            {
                try
                {
                    StartMultiplayer();
                    modEntry.Logger.Log("Multiplayer is loaded: the car levers, held bleeds, bail-offs, the handles' added notches, and the host's brake settings sync through the host.");
                }
                catch (Exception e)
                {
                    modEntry.Logger.Warning($"Multiplayer is loaded, but its API couldn't be reached ({e.GetBaseException().Message}): Realistic Brakes won't sync its own controls.");
                }
            }
            var firing = UnityModManager.FindMod("RealisticFiring");
            if (firing != null && firing.Enabled)
                modEntry.Logger.Log("Realistic Firing is loaded: its air pump fits reach the model, and a click on the independent lever bails off through this mod while it runs the brake.");

            var host = new GameObject("[RealisticBrakes]");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<AirOverlay>();
            host.AddComponent<Notice>();
            host.AddComponent<Controls.LeverBench>();
            host.AddComponent<Sim.HandBrakeHold>();
            modEntry.Logger.Log("Realistic Brakes: the model runs every brake set.");
            return true;
        }

        // The integrations with optional mods are assemblies of their own beside this one, loaded only when those mods are: the mod
        // manager loads every type of this assembly, and one needing a mod that isn't there failed the whole mod (seen on the
        // laptop, 2026-10-27, with the Multiplayer mod disabled).
        private static void StartMultiplayer() => StartIntegration("RealisticBrakes.Multiplayer", "RealisticBrakes.Sim.MultiplayerLink");

        // Loaded from its bytes: with no folder of its own, its reference to this assembly binds to the one the mod manager
        // loaded (from a cached copy), never a second copy of the file beside it, whose state would be its own.
        private static void StartIntegration(string assembly, string type, params object[] arguments)
        {
            var loaded = Assembly.Load(System.IO.File.ReadAllBytes(System.IO.Path.Combine(Mod.Path, assembly + ".dll")));
            var start = loaded.GetType(type, true).GetMethod("Start", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (start == null)
                throw new MissingMethodException(type, "Start");
            start.Invoke(null, arguments);
            var copies = AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == Assembly.GetExecutingAssembly().GetName().Name);
            if (copies > 1)
                Mod.Logger.Warning($"{assembly} loaded a second copy of Realistic Brakes: its link may not reach the running mod");
        }


        // The mod's types (all of them load: none needs another mod's assembly).
        private static IEnumerable<Type> LoadableTypes()
        {
            try
            {
                return Assembly.GetExecutingAssembly().GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }

        private static bool reported;

        /// <summary>
        /// Names, once, at the first brake tick (every mod loaded by then),
        /// other mods that replace the same stock brake methods (Valve 394,
        /// Self-Lapping Brakes, and the like). Postfixes alone, such as Leak
        /// Down's, are fine; another prefix on an air method fights the
        /// model, or is skipped along with the stock method.
        /// </summary>
        internal static void ReportOtherBrakeModsOnce()
        {
            if (reported)
                return;
            reported = true;
            var modEntry = Mod;
            var brake = typeof(DV.Simulation.Brake.BrakeSystem);
            var others = new HashSet<string>();
            foreach (var name in new[] { "SimulateTrainBrake", "SimulateCarControlValve", "SimulateLocoControlValve", "SimulateCompressor", "TargetCylinderPressure" })
            {
                var method = AccessTools.Method(brake, name);
                var info = method == null ? null : Harmony.GetPatchInfo(method);
                if (info == null)
                    continue;
                foreach (var patch in info.Prefixes.Concat(info.Transpilers))
                    if (patch.owner != modEntry.Info.Id)
                        others.Add(patch.owner);
            }
            if (others.Count > 0)
                modEntry.Logger.Warning($"Other mods also replace the stock brake ({string.Join(", ", others)}); the two will fight. Use one brake mod at a time.");
        }
    }

    /// <summary>
    /// Marks a patch class that isn't part of the brake itself: if it fails
    /// to apply, the mod warns and runs on without it, instead of standing
    /// aside for the stock brake.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class OptionalPatchAttribute : Attribute
    {
    }
}
