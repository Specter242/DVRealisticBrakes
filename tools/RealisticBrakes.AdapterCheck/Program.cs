using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DV.Simulation.Brake;
using HarmonyLib;
using LocoSim.Definitions;
using LocoSim.Implementations;

namespace RealisticBrakes.AdapterCheck
{
    /// <summary>
    /// Checks the adapter's patches against the installed game: every target
    /// resolves, every parameter Harmony injects by name exists on its
    /// target, and the internal field the adapter writes is there.
    /// </summary>
    internal static class Program
    {
        private static int failures;

        private static int Main()
        {
            // The mod manager loads every type of the mod; one needing another mod's assembly, absent here as for a player
            // without that mod, fails the whole mod (seen 2026-10-27).
            Type[] types;
            try
            {
                types = typeof(RealisticBrakes.Main).Assembly.GetTypes();
                Check(true, $"every one of the mod's {types.Length} types loads without the optional mods' assemblies");
            }
            catch (ReflectionTypeLoadException e)
            {
                Check(false, "every one of the mod's types loads without the optional mods' assemblies (not: "
                    + string.Join("; ", e.LoaderExceptions.Select(x => x.Message).Distinct()) + ")");
                types = e.Types.Where(t => t != null).ToArray();
            }
            var patches = types
                .Where(t => t.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0)
                .ToList();
            var bad = new List<string>();
            var resolved = 0;
            var originals = new HashSet<MethodBase>();
            foreach (var type in patches)
            {
                foreach (var (original, patch) in Targets(type, bad))
                {
                    resolved++;
                    originals.Add(original);
                    var names = new HashSet<string>(original.GetParameters().Select(p => p.Name));
                    // A transpiler takes the method's code, not its arguments.
                    if (patch.Name == "Transpiler")
                        continue;
                    foreach (var parameter in patch.GetParameters())
                    {
                        var name = parameter.Name;
                        if (name.StartsWith("__") || names.Contains(name))
                            continue;
                        bad.Add($"{type.Name}.{patch.Name}: {original.DeclaringType.Name}.{original.Name} has no parameter '{name}'");
                    }
                }
            }
            Check(bad.Count == 0, $"{patches.Count} patch classes, {resolved} targets resolve with their injected parameters" + (bad.Count > 0 ? $" (not: {string.Join("; ", bad)})" : ""));

            var field = AccessTools.Field(typeof(BrakeSystem), "mainReservoirPressureUnsmoothed");
            Check(field != null && field.FieldType == typeof(float), "BrakeSystem.mainReservoirPressureUnsmoothed is a float field");
            var chapters = AccessTools.Field(typeof(ChapterSelector), "allChapters");
            Check(chapters != null && chapters.FieldType == typeof(List<ChapterSelector.PageBookChapter>),
                "a booklet's chapter selector keeps its chapters in a list, which the brake booklet sets to its thumb tabs");
            var placed = AccessTools.Field(typeof(ItemPlacerOffset), "customRotationOffset");
            Check(placed != null && placed.FieldType == typeof(UnityEngine.Vector3),
                "an item's placer keeps its turn on the table in a Vector3, which the brake booklet sets to the vehicle catalog's");
            var notchAngle = AccessTools.Field(typeof(SteppedJoint), "cachedAngle");
            Check(notchAngle != null && notchAngle.FieldType == typeof(float),
                "a notched lever keeps the angle it last aimed its spring from in a float, which the S-6 handle clears to re-aim it");

            foreach (var name in Patches.StockAirPatches.Methods)
            {
                var method = AccessTools.DeclaredMethod(typeof(BrakeSystem), name);
                Check(method != null && method.ReturnType == typeof(void) && method.GetParameters().Length == 1,
                    $"BrakeSystem.{name}(dt) is the void air method the model stands in for");
            }

            CheckSaveRoundTrip();
            CheckBailClick();
            CheckSettingDefaults();
            CheckPumpSteam();

            // Apply every patch as the mod does, then take them off again.
            var harmony = new Harmony("RealisticBrakes.AdapterCheck");
            var failed = new List<string>();
            foreach (var type in patches)
            {
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception e)
                {
                    failed.Add($"{type.Name}: {e.GetBaseException().Message}");
                }
            }
            var patched = Harmony.GetAllPatchedMethods().Count(m => Harmony.GetPatchInfo(m).Owners.Contains(harmony.Id));
            harmony.UnpatchAll(harmony.Id);
            // Mono can't resolve Unity's internal calls as it compiles the patched stock methods, and says so; that is harmless here.
            Check(failed.Count == 0 && patched == originals.Count,
                $"Harmony applies every patch class ({patched} methods patched)" + (failed.Count > 0 ? $" (not: {string.Join("; ", failed)})" : ""));

            Console.WriteLine(failures == 0 ? "adapter checks: all passed" : $"adapter checks: {failures} failed");
            return failures == 0 ? 0 : 1;
        }

        /// <summary>Each (target, patch method) of a patch class, as Harmony's class processor resolves them.</summary>
        private static IEnumerable<(MethodBase original, MethodInfo patch)> Targets(Type type, List<string> bad)
        {
            var patchMethods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(m => m.Name == "Prefix" || m.Name == "Postfix" || m.Name == "Transpiler" || m.Name == "Finalizer"
                            || m.GetCustomAttributes(typeof(HarmonyPrefix), false).Length > 0
                            || m.GetCustomAttributes(typeof(HarmonyPostfix), false).Length > 0)
                .ToList();
            var targetMethods = AccessTools.DeclaredMethod(type, "TargetMethods");
            if (targetMethods != null)
            {
                var originals = ((IEnumerable<MethodBase>)targetMethods.Invoke(null, null)).ToList();
                if (originals.Count == 0 || originals.Any(m => m == null))
                    bad.Add($"{type.Name}: missing target methods");
                foreach (var original in originals.Where(m => m != null))
                    foreach (var patch in patchMethods)
                        yield return (original, patch);
                yield break;
            }
            var classInfo = HarmonyMethodExtensions.GetFromType(type);
            foreach (var patch in patchMethods)
            {
                var info = HarmonyMethod.Merge(classInfo.Concat(HarmonyMethodExtensions.GetFromMethod(patch)).ToList());
                var original = AccessTools.DeclaredMethod(info.declaringType, info.methodName, info.argumentTypes);
                if (original == null)
                    bad.Add($"{type.Name}.{patch.Name}: no target {info.declaringType?.Name}.{info.methodName}");
                else
                    yield return (original, patch);
            }
        }

        // A car's own air through its save entry and the game's JSON, as a save and a load do.
        private static void CheckSaveRoundTrip()
        {
            var geometry = RealisticBrakes.Core.PipeGeometry.Uic();
            var car = new RealisticBrakes.Core.CarBrake(15, geometry, new RealisticBrakes.Core.UicDistributor
            {
                Mode = RealisticBrakes.Core.BrakingMode.Passenger,
                Load = RealisticBrakes.Core.LoadSetting.Empty,
            });
            car.Distributor.RestoreLatch(true, 0.4e5);
            car.Distributor.QuickServiceChamber.Pressure = 4.2e5;
            car.Distributor.PullRelease();
            var engine = new RealisticBrakes.Core.LocomotiveBrake(null, new RealisticBrakes.Core.DriverBrakeValve());
            var loco = new RealisticBrakes.Core.CarBrake(20, geometry, locomotive: engine);
            engine.DriverValve.EqualizingReservoir.Pressure = 5.6e5;
            engine.DriverValve.RestoreOvercharge(0.25e5);
            engine.BailedOff = 1.2e5;

            var carJson = Newtonsoft.Json.Linq.JObject.Parse(Sim.SavedAir.Write(car).ToString());
            var locoJson = Newtonsoft.Json.Linq.JObject.Parse(Sim.SavedAir.Write(loco).ToString());
            var car2 = new RealisticBrakes.Core.CarBrake(15, geometry, new RealisticBrakes.Core.UicDistributor());
            var engine2 = new RealisticBrakes.Core.LocomotiveBrake(null, new RealisticBrakes.Core.DriverBrakeValve());
            var loco2 = new RealisticBrakes.Core.CarBrake(20, geometry, locomotive: engine2);
            Sim.SavedAir.Read(carJson, car2);
            Sim.SavedAir.Read(locoJson, loco2);
            var d = car2.Distributor;
            Check(d.Mode == RealisticBrakes.Core.BrakingMode.Passenger && d.Load == RealisticBrakes.Core.LoadSetting.Empty && d.Applied && d.ReleaseValveOpen
                  && Math.Abs(d.DeepestReduction - 0.4e5) < 1 && Math.Abs(d.QuickServiceChamber.Pressure - 4.2e5) < 1
                  && Math.Abs(engine2.DriverValve.EqualizingReservoir.Pressure - 5.6e5) < 1 && Math.Abs(engine2.DriverValve.OverchargeLeft - 0.25e5) < 1
                  && Math.Abs(engine2.BailedOff - 1.2e5) < 1,
                "a car's levers, latch, quick-service chamber, and an engine's equalizing reservoir, overcharge, and bail-off survive a save and load");

            var american = new RealisticBrakes.Core.CarBrake(15, geometry, new RealisticBrakes.Core.AarControlValve
            {
                Retainer = RealisticBrakes.Core.Retainer.HighPressure,
                Load = RealisticBrakes.Core.LoadSetting.Empty,
            });
            american.ControlValve.RestoreLatch(true, true);
            american.ControlValve.RestoreCylinderRelease(true);
            american.ControlValve.EmergencyReservoir.Pressure = 4.3e5;
            var americanJson = Newtonsoft.Json.Linq.JObject.Parse(Sim.SavedAir.Write(american).ToString());
            var american2 = new RealisticBrakes.Core.CarBrake(15, geometry, new RealisticBrakes.Core.AarControlValve());
            Sim.SavedAir.Read(americanJson, american2);
            var a = american2.ControlValve;
            var european = new RealisticBrakes.Core.CarBrake(15, geometry, new RealisticBrakes.Core.UicDistributor());
            Sim.SavedAir.Read(americanJson, european);
            Check(a.Retainer == RealisticBrakes.Core.Retainer.HighPressure && a.Load == RealisticBrakes.Core.LoadSetting.Empty && a.Applied && a.Emergency
                  && a.CylinderReleaseOpen
                  && Math.Abs(a.EmergencyReservoir.Pressure - 4.3e5) < 1 && european.Distributor.Load == RealisticBrakes.Core.LoadSetting.Loaded,
                "an American valve's retainer, load, latch, bled cylinder, and emergency reservoir survive a save and load; a European car ignores them");

            var et = new RealisticBrakes.Core.LocomotiveBrake(RealisticBrakes.Core.LocomotiveBrakeSpec.EtSteam(),
                new RealisticBrakes.Core.DriverBrakeValve(RealisticBrakes.Core.DriverBrakeValveSpec.H6()));
            et.HeldAutomatic = 1.5e5;
            et.IndependentChamber.Pressure = 3.0e5;
            var etJson = Newtonsoft.Json.Linq.JObject.Parse(Sim.SavedAir.Write(new RealisticBrakes.Core.CarBrake(20, geometry, locomotive: et)).ToString());
            var et2 = new RealisticBrakes.Core.LocomotiveBrake(RealisticBrakes.Core.LocomotiveBrakeSpec.EtSteam(),
                new RealisticBrakes.Core.DriverBrakeValve(RealisticBrakes.Core.DriverBrakeValveSpec.H6()));
            Sim.SavedAir.Read(etJson, new RealisticBrakes.Core.CarBrake(20, geometry, locomotive: et2));
            Check(Math.Abs(et2.HeldAutomatic - 1.5e5) < 1 && Math.Abs(et2.IndependentChamber.Pressure - 3.0e5) < 10,
                "an E-T engine's held train brake and the S-6 independent's held air survive a save and load");
        }

        // The click on the independent lever (IndependentBailClick) rests on the
        // game's lever events and, in VR, its own use of a held object.
        private static void CheckBailClick()
        {
            const BindingFlags any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var control = typeof(DV.CabControls.ControlImplBase);
            Check(control.GetEvent("Grabbed") != null && control.GetEvent("Ungrabbed") != null && control.GetProperty("Value") != null,
                "the cab's levers say when they're grabbed and let go, and where they stand");
            var interactable = typeof(VRTK.VRTK_InteractableObject);
            var gameUse = typeof(DV.VRTK_Extensions.VRTK_InteractUse_DV);
            Check(interactable.GetField("isUsable") != null && interactable.GetField("useOnlyIfGrabbed") != null
                  && interactable.GetEvent("InteractableObjectUsed") != null
                  && gameUse.GetMethods(any).Any(m => m.Name == "OnUseButtonReleased")
                  && typeof(DV.VRTK_Extensions.VRTK_InteractableObject_DV).GetMethods(any).Any(m => m.Name == "OnInteractableObjectGrabbed")
                  && typeof(DV.CabControls.VRTK.LeverVRTK).IsSubclassOf(control)
                  && typeof(DV.CabControls.VRTK.VRTK_ControlImplBaseInteractableObject).IsSubclassOf(interactable),
                "the VR independent lever can take the trigger while held: VRTK's use flags and event, the game's use on release and its grabbing hand");
            Check(Controls.TrainBrakeHandle.Supported,
                "a widened train brake handle finds the game's lever value update, its lever setter, and notched-joint setup");
            Check(typeof(Patches.CabControlsPatch).IsDefined(typeof(OptionalPatchAttribute), false)
                  && typeof(Patches.WidenedHandleNotchPatch).IsDefined(typeof(OptionalPatchAttribute), false)
                  && typeof(Patches.WidenedHandleSetPatch).IsDefined(typeof(OptionalPatchAttribute), false)
                  && typeof(Patches.CarControlsPatch).IsDefined(typeof(OptionalPatchAttribute), false)
                  && typeof(Patches.BenchSpawnerPatch).IsDefined(typeof(OptionalPatchAttribute), false)
                  && typeof(Patches.AirPumpSteamPatch).IsDefined(typeof(OptionalPatchAttribute), false)
                  && !typeof(Patches.BrakesetTickPatch).IsDefined(typeof(OptionalPatchAttribute), false),
                "only the cab controls' and the air pumps' steam patches are optional; the brake's own patches stand or fall together");
        }

        private static void CheckSettingDefaults()
        {
            var settings = new Settings();
            var manual = typeof(Settings).GetField(nameof(Settings.ManualEmptyLoaded));
            Check(!settings.ShowOverlay && !settings.ManualEmptyLoaded && settings.ShowValveText
                  && !manual.GetCustomAttributes(true).Any(a => a.GetType().Name == "DrawAttribute"),
                "a new install starts with the pressure overlay off, the brake controls' text on, and the empty/loaded lever automatic, its by-hand switch not in the menu");
            var draw = typeof(Settings).GetField(nameof(Settings.LoadBraking)).GetCustomAttributes(true).OfType<UnityModManagerNet.DrawAttribute>().SingleOrDefault();
            Check(settings.LoadBraking == LoadBraking.Period && draw != null && draw.VisibleOn == "Equipment|American",
                "American cars' load braking starts at Period, in the menu only with American equipment");
        }

        private static void CheckPumpSteam()
        {
            var harmony = new Harmony("RealisticBrakes.AdapterCheck.PumpSteam");
            var firing = new Harmony("RealisticBrakes.AdapterCheck.FiringStandIn");
            var settings = AccessTools.Property(typeof(RealisticBrakes.Main), nameof(RealisticBrakes.Main.Settings));
            var before = settings.GetValue(null);
            try
            {
                harmony.CreateClassProcessor(typeof(Patches.AirPumpSteamPatch)).Patch();
                firing.Patch(AccessTools.Method(typeof(SteamCompressor), nameof(SteamCompressor.Tick)),
                    postfix: new HarmonyMethod(typeof(Program), nameof(FiringStandIn)));
                settings.SetValue(null, new Settings());
                var stock = NewS060Pump(20f, out var stockSteam);
                firingPump = NewS060Pump(18.4f, out var firingSteam);
                Run(stock, firingPump);
                var limited = stockSteam.Value;
                var kept = firingSteam.Value;
                RealisticBrakes.Main.Settings.Enabled = false;
                var off = NewS060Pump(20f, out var offSteam);
                Run(off);
                var most = 20f * Patches.AirPumpSteamPatch.SteamPerAir;
                Check(Math.Abs(limited - most) < 0.002f && Math.Abs(kept - 0.18f) < 0.001f && Math.Abs(offSteam.Value - 1f) < 0.01f,
                    $"the S060's air pump flat out takes {limited:F3} kg/s of steam ({most:F3} for its 20 L/s; the game's {offSteam.Value:F2} with the " +
                    $"model off); one another mod sets after keeps its {kept:F3}");
            }
            finally
            {
                harmony.UnpatchAll(harmony.Id);
                firing.UnpatchAll(firing.Id);
                settings.SetValue(null, before);
                firingPump = null;
            }
        }

        private static SteamCompressor firingPump;

        // Realistic Firing's AirPumpPatch on the pump it fitted: its share of its full rate times its 0.18 kg/s.
        private static void FiringStandIn(SteamCompressor __instance)
        {
            if (__instance == firingPump)
                __instance.steamConsumptionReadOut.Value = UnityEngine.Mathf.Clamp01(__instance.productionRateNormalizedReadOut.Value) * 0.18f;
        }

        // The S060's pump (the game's: 20 bar·L/s, 1 kg/s of steam flat out at 3 bar), at 14 bar of steam, governor on, the
        // main reservoir half full; its air as given.
        private static SteamCompressor NewS060Pump(float air, out Port steam)
        {
            var def = new SteamCompressorDefinition { ID = "compressor", maxProductionRate = air, maxSteamConsumption = 1f, pressureForMaxProduction = 3f };
            var pump = (SteamCompressor)def.InstantiateImplementation();
            pump.SetGameParams(new SimGameParams(false, false, false, 1f, 1f));
            void Wire(PortReferenceDefinition reference, PortValueType type, float value) =>
                pump.GetAllPortReferences().Single(r => r.id == $"{def.ID}.{reference.ID}")
                    .SetPortReference(new Port(reference.ID.ToLowerInvariant(), new PortDefinition(PortType.READONLY_OUT, type, "OUT"), value));
            Wire(def.compressorControl, PortValueType.CONTROL, 1f);
            Wire(def.steamPressure, PortValueType.PRESSURE, 14f);
            pump.GetAllPorts().Single(p => p.id == $"{def.ID}.{def.activationSignalExtIn.ID}").ExternalValueUpdate(1f);
            pump.GetAllPorts().Single(p => p.id == $"{def.ID}.{def.mainResPressureNormalizedExtIn.ID}").ExternalValueUpdate(0.5f);
            steam = pump.GetAllPorts().Single(p => p.id == $"{def.ID}.{def.steamConsumptionReadOut.ID}");
            return pump;
        }

        // 30 s of the game's ticks, past the pump's smoothing.
        private static void Run(params SteamCompressor[] pumps)
        {
            for (var i = 0; i < 1500; i++)
                foreach (var pump in pumps)
                    pump.Tick(0.02f);
        }

        private static void Check(bool ok, string what)
        {
            Console.WriteLine((ok ? "ok   " : "FAIL ") + what);
            if (!ok)
                failures++;
        }
    }
}
