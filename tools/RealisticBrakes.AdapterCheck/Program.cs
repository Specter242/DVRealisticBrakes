using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
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
            var easing = AccessTools.Field(typeof(BrakeSystem), "brakingFactorRef");
            Check(easing != null && easing.FieldType == typeof(float),
                "BrakeSystem.brakingFactorRef is a float field, which the hold sets as it freezes a standing cut's braking factor");
            var auxiliary = AccessTools.Field(typeof(BrakeSystem), "auxReservoirPressure");
            Check(auxiliary != null && auxiliary.FieldType == typeof(float),
                "BrakeSystem.auxReservoirPressure is a float field, which a reused car is given a new car's value in");

            // The public API other mods call by reflection keeps its shape (scripts/public/README.md, "For other mods").
            var api = typeof(RealisticBrakes.Api);
            const BindingFlags publicStatic = BindingFlags.Public | BindingFlags.Static;
            var apiVersion = api.GetProperty("ApiVersion", publicStatic);
            var apiActive = api.GetProperty("Active", publicStatic);
            var apiBail = api.GetMethod("BailOff", publicStatic, null, new[] { typeof(TrainCar) }, null);
            var apiPressure = api.GetMethod("Pressure", publicStatic, null, new[] { typeof(TrainCar), typeof(string) }, null);
            var apiEquipment = api.GetMethod("Equipment", publicStatic, null, new[] { typeof(TrainCar) }, null);
            var apiMode = api.GetMethod("BrakeMode", publicStatic, null, new[] { typeof(TrainCar) }, null);
            var apiCutOut = api.GetMethod("CutOut", publicStatic, null, new[] { typeof(TrainCar) }, null);
            var apiValveCutOut = api.GetMethod("BrakeValveCutOut", publicStatic, null, new[] { typeof(TrainCar) }, null);
            Check(api.IsPublic && apiVersion?.PropertyType == typeof(int) && (int)apiVersion.GetValue(null) == 2
                  && apiActive?.PropertyType == typeof(bool) && apiBail?.ReturnType == typeof(float)
                  && apiPressure?.ReturnType == typeof(float) && apiEquipment?.ReturnType == typeof(string)
                  && apiMode?.ReturnType == typeof(string) && apiCutOut?.ReturnType == typeof(bool?) && apiValveCutOut?.ReturnType == typeof(bool?)
                  && (float)apiBail.Invoke(null, new object[] { null }) == 0f
                  && float.IsNaN((float)apiPressure.Invoke(null, new object[] { null, "BrakePipe" })) && apiEquipment.Invoke(null, new object[] { null }) == null
                  && apiMode.Invoke(null, new object[] { null }) == null && apiCutOut.Invoke(null, new object[] { null }) == null
                  && apiValveCutOut.Invoke(null, new object[] { null }) == null,
                "the public API exposes ApiVersion 2 and keeps Active, BailOff, Pressure, Equipment, BrakeMode, CutOut, and BrakeValveCutOut, returning 0, NaN, and null for no car");
            var apiVacuum = api.GetMethod("VacuumInches", publicStatic, null, new[] { typeof(TrainCar), typeof(string) }, null);
            Check(apiVacuum?.ReturnType == typeof(float)
                  && float.IsNaN((float)apiVacuum.Invoke(null, new object[] { null, "BrakePipe" }))
                  && float.IsNaN((float)apiVacuum.Invoke(null, new object[] { null, "VacuumChamber" }))
                  && float.IsNaN((float)apiVacuum.Invoke(null, new object[] { null, "Underside" }))
                  && float.IsNaN((float)apiVacuum.Invoke(null, new object[] { null, "BrakeCylinder" })),
                "the private build's public API adds VacuumInches(TrainCar, string) and returns NaN for no car");
            CheckBailGuard();
            var chapters = AccessTools.Field(typeof(ChapterSelector), "allChapters");
            Check(chapters != null && chapters.FieldType == typeof(List<ChapterSelector.PageBookChapter>),
                "a booklet's chapter selector keeps its chapters in a list, which the brake booklet sets to its thumb tabs");
            var placed = AccessTools.Field(typeof(ItemPlacerOffset), "customRotationOffset");
            Check(placed != null && placed.FieldType == typeof(UnityEngine.Vector3),
                "an item's placer keeps its turn on the table in a Vector3, which the brake booklet sets to the vehicle catalog's");
            var prefabName = AccessTools.Field(typeof(InventoryItemSpec), "itemPrefabName");
            Check(prefabName != null && prefabName.FieldType == typeof(string),
                "an item's spec keeps the prefab name saves record in a string, which the brake booklet sets to its own");
            var madeCount = AccessTools.Field(typeof(StartingItemsController), "instantiatedItemCount");
            var safety = AccessTools.Field(typeof(StartingItemsController), "ITEM_INSTANTIATION_SAFETY_POSITION");
            var apart = AccessTools.Field(typeof(StartingItemsController), "ITEM_INSTANTIATION_SAFETY_OFFSET");
            Check(madeCount != null && madeCount.FieldType == typeof(int) && safety != null && safety.IsStatic && safety.FieldType == typeof(UnityEngine.Vector3)
                    && apart != null && apart.IsStatic && apart.FieldType == typeof(UnityEngine.Vector3),
                "the game makes saved items above the world, each apart from the last, as the brake booklet's loader copies");
            var notchAngle = AccessTools.Field(typeof(SteppedJoint), "cachedAngle");
            Check(notchAngle != null && notchAngle.FieldType == typeof(float),
                "a notched lever keeps the angle it last aimed its spring from in a float, which the S-6 handle clears to re-aim it");
            // An engine whose handles feed ports of their own (the WE-4U): its brake handles are the ones the game's brake keys move.
            var actionName = AccessTools.Field(typeof(DV.KeyboardInput.AKeyboardInput.ActionReference), "name");
            var gameActions = typeof(DV.Interaction.Inputs.InputManager).GetNestedType("RewiredActionConsts", BindingFlags.Public | BindingFlags.NonPublic)?
                .GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral).Select(f => f.Name).ToList() ?? new List<string>();
            var brakeActions = gameActions.Where(n => Core.BrakeKeyActions.Of(n) != Core.BrakeKeyControl.None).OrderBy(n => n, StringComparer.Ordinal).ToList();
            var expectedActions = new[] { "BrakeAbsolute", "BrakeCutoutAbsolute", "BrakeCutoutIncremental", "BrakeCutoutToggle", "BrakeIncremental",
                "IndependentBrakeAbsolute", "IndependentBrakeIncremental" };
            Check(actionName != null && actionName.FieldType == typeof(string)
                  && Controls.CabHandles.ActionFields(typeof(DV.KeyboardInput.MouseScrollKeyboardInput)).Length > 0
                  && Controls.CabHandles.ActionFields(typeof(DV.KeyboardInput.AnalogSetValueJoystickInput)).Length > 0
                  && brakeActions.SequenceEqual(expectedActions),
                $"a handle's key inputs name their actions, and the game's brake actions are the seven the handles are found by ({string.Join(", ", brakeActions)})");

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
            CheckMultiplayer();

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
            american.ControlValve.RestoreCylinderRelease(true, 4.3e5);
            american.ControlValve.EmergencyReservoir.Pressure = 4.3e5;
            var americanJson = Newtonsoft.Json.Linq.JObject.Parse(Sim.SavedAir.Write(american).ToString());
            var american2 = new RealisticBrakes.Core.CarBrake(15, geometry, new RealisticBrakes.Core.AarControlValve());
            Sim.SavedAir.Read(americanJson, american2);
            var a = american2.ControlValve;
            var european = new RealisticBrakes.Core.CarBrake(15, geometry, new RealisticBrakes.Core.UicDistributor());
            Sim.SavedAir.Read(americanJson, european);
            Check(a.Retainer == RealisticBrakes.Core.Retainer.HighPressure && a.Load == RealisticBrakes.Core.LoadSetting.Empty && a.Applied && a.Emergency
                  && a.CylinderReleaseOpen && Math.Abs(a.BledPipeMin - 4.3e5) < 10
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
            var pipe = RealisticBrakes.Core.PipeGeometry.VacuumTrainPipe();
            var vacuum = new RealisticBrakes.Core.CarBrake(15, pipe, new RealisticBrakes.Core.VacuumCylinder(RealisticBrakes.Core.VacuumCylinderSpec.Bogie())
            {
                Load = RealisticBrakes.Core.LoadSetting.Empty,
            });
            vacuum.VacuumCylinder.Restore(RealisticBrakes.Core.Vacuum.FromInches(20), RealisticBrakes.Core.Vacuum.FromInches(4));
            var steam = new RealisticBrakes.Core.VacuumEngine(RealisticBrakes.Core.VacuumEngineSpec.Steam());
            steam.RestoreOwnBrake(0.6);
            var vacuumJson = Newtonsoft.Json.Linq.JObject.Parse(Sim.SavedAir.Write(vacuum).ToString());
            var steamJson = Newtonsoft.Json.Linq.JObject.Parse(Sim.SavedAir.Write(new RealisticBrakes.Core.CarBrake(20, pipe, vacuumEngine: steam)).ToString());
            var vacuum2 = new RealisticBrakes.Core.CarBrake(15, pipe, new RealisticBrakes.Core.VacuumCylinder(RealisticBrakes.Core.VacuumCylinderSpec.Bogie()));
            Sim.SavedAir.Read(vacuumJson, vacuum2);
            var steam2 = new RealisticBrakes.Core.VacuumEngine(RealisticBrakes.Core.VacuumEngineSpec.Steam());
            Sim.SavedAir.Read(steamJson, new RealisticBrakes.Core.CarBrake(20, pipe, vacuumEngine: steam2));
            var europeanToo = new RealisticBrakes.Core.CarBrake(15, geometry, new RealisticBrakes.Core.UicDistributor());
            Sim.SavedAir.Read(vacuumJson, europeanToo);
            var v = vacuum2.VacuumCylinder;
            Check(v.Load == RealisticBrakes.Core.LoadSetting.Empty && v.Applied && Math.Abs(v.ForceShare - vacuum.VacuumCylinder.ForceShare) < 1e-3
                  && Math.Abs(v.Chamber.Pressure - vacuum.VacuumCylinder.Chamber.Pressure) < 10 && Math.Abs(steam2.BrakeShare - 0.6) < 1e-3
                  && europeanToo.Distributor.Load == RealisticBrakes.Core.LoadSetting.Loaded,
                "a vacuum car's chamber, piston, and load, and a steam engine's steam brake, survive a save and load; a European car ignores them");
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
            var every = typeof(Settings).GetField(nameof(Settings.EveryEngineUsesSetting)).GetCustomAttributes(true).OfType<UnityModManagerNet.DrawAttribute>().SingleOrDefault();
            Check(!settings.EveryEngineUsesSetting && every != null,
                "each engine starts on its own prototype's brake equipment, the override in the menu and off");
            var draw = typeof(Settings).GetField(nameof(Settings.LoadBraking)).GetCustomAttributes(true).OfType<UnityModManagerNet.DrawAttribute>().SingleOrDefault();
            Check(settings.LoadBraking == LoadBraking.Period && draw != null && draw.InvisibleOn == "Equipment|European",
                "American and vacuum cars' load braking starts at Period, in the menu with either");
            var direct = typeof(Settings).GetField(nameof(Settings.DirectAdmission)).GetCustomAttributes(true).OfType<UnityModManagerNet.DrawAttribute>().SingleOrDefault();
            Check(settings.DirectAdmission && direct != null && direct.VisibleOn == "Equipment|Vacuum",
                "vacuum cars' DA valves start on, in the menu only with vacuum equipment");
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

        // Multiplayer reliability checks: AdjustKey disconnect/forget cleanup, ModelSettings and host notification,
        // guest UI model lock, build flavor, and (when MultiplayerAPI.dll is installed) packet wire round-trips.
        private static void CheckMultiplayer()
        {
            CheckAdjustKeyCleanup();
            CheckMultiplayerSettings();
            CheckMultiplayerPackets();
        }

        private static TrainCar MakeFakeCar(int id)
        {
            var car = (TrainCar)FormatterServices.GetUninitializedObject(typeof(TrainCar));
            AccessTools.Field(typeof(UnityEngine.Object), "m_CachedPtr")?.SetValue(car, new IntPtr(id));
            AccessTools.Field(typeof(UnityEngine.Object), "m_InstanceID")?.SetValue(car, id);
            return car;
        }

        private static void CheckAdjustKeyCleanup()
        {
            var car1 = MakeFakeCar(101);
            var car2 = MakeFakeCar(102);
            try
            {
                Controls.AdjustKey.SetHold(car1, 3, true);
                Controls.AdjustKey.SetHold(car1, 4, true);
                Controls.AdjustKey.SetHold(car1, Controls.TrainBrakeHandle.LocalHand, true);
                Controls.AdjustKey.SetHold(car2, 3, true);

                var holdsBefore = Controls.AdjustKey.Holds().ToList();
                var holders1Before = Controls.AdjustKey.HoldersOf(car1).OrderBy(p => p).ToList();
                var disconnect = Sim.Net.ReleaseHands(3);
                var holders1AfterDisconnect = Controls.AdjustKey.HoldersOf(car1).ToList();
                var car2AfterDisconnect = Controls.AdjustKey.Held(car2);
                var car1AfterDisconnect = Controls.AdjustKey.Held(car1);

                var endSession = Sim.Net.ReleaseHands(null);
                var holders1AfterEnd = Controls.AdjustKey.HoldersOf(car1).ToList();
                var car1LocalKept = Controls.AdjustKey.Held(car1);

                Controls.AdjustKey.Forget(car1);
                var car1AfterForget = Controls.AdjustKey.Held(car1);
                var holdsAfterForget = Controls.AdjustKey.Holds().ToList();

                Check(holdsBefore.Count == 4
                      && holders1Before.SequenceEqual(new byte[] { 3, 4 })
                      && disconnect.Count == 2 && disconnect.All(a => a.Kind == Sim.NetActionKind.Adjust && a.Value == 0)
                      && !car2AfterDisconnect && car1AfterDisconnect
                      && holders1AfterDisconnect.SequenceEqual(new byte[] { 4 })
                      && endSession.Count == 1 && endSession[0].Kind == Sim.NetActionKind.Adjust && endSession[0].Value == 0
                      && holders1AfterEnd.Count == 0 && car1LocalKept
                      && !car1AfterForget && holdsAfterForget.Count == 0,
                    "AdjustKey tracks holds per player, releases remote holds on disconnect and session end while keeping local hand, and clears on Forget");
            }
            finally
            {
                Controls.AdjustKey.Forget(car1);
                Controls.AdjustKey.Forget(car2);
            }
        }

        private sealed class FakeNetLink : Sim.INetLink
        {
            public bool IsGuest { get; set; }
            public int SettingsNotifications { get; private set; }
            public int Ticks { get; private set; }
            public void Report(Sim.NetAction action) { }
            public void NotifyHostSettingsChanged() => SettingsNotifications++;
            public void Tick() => Ticks++;
        }

        private static void CheckMultiplayerSettings()
        {
            var prevLink = Sim.Net.Link;
            var settingsProp = AccessTools.Property(typeof(RealisticBrakes.Main), nameof(RealisticBrakes.Main.Settings));
            var prevSettings = settingsProp.GetValue(null);
            var staleField = AccessTools.Field(typeof(Sim.BrakeSets), "stale");
            var prevWarn = Notice.WarnObserver;
            var prevShow = Notice.ShowObserver;
            var warnings = new List<string>();
            var notices = new List<string>();
            Notice.WarnObserver = warnings.Add;
            Notice.ShowObserver = notices.Add;
            try
            {
                var source = new Settings
                {
                    Enabled = false,
                    Equipment = Equipment.American,
                    BrakeForceFromCar = false,
                    FreightMode = Core.BrakingMode.Passenger,
                    ManualEmptyLoaded = true,
                    WidenTrainBrakeHandles = false,
                    LoadBraking = LoadBraking.EmptyLoad,
                    DirectAdmission = false,
                    EveryEngineUsesSetting = true,
                };
                var model = Sim.Net.ModelSettings.From(source);
                var target = new Settings();
                var differedBefore = !model.SameAs(Sim.Net.ModelSettings.From(target));
                model.To(target);
                var matchedAfter = model.SameAs(Sim.Net.ModelSettings.From(target));
                var mutated = model;
                mutated.EveryEngineUsesSetting = !mutated.EveryEngineUsesSetting;
                var detectsDiff = !model.SameAs(mutated);
                Check(differedBefore && matchedAfter && detectsDiff,
                    "Net.ModelSettings round-trips all host-owned model fields and detects differences");

                Check(RealisticBrakes.Main.BuildFlavor == "public", "public release reports build flavor 'public'");

                var link = new FakeNetLink { IsGuest = false };
                Sim.Net.Link = link;
                Sim.Net.Tick();
                var settings = new Settings();
                settings.AcceptLoaded();
                settingsProp.SetValue(null, settings);

                // Client-only setting change does not notify guests.
                settings.ShowOverlay = true;
                settings.OnChange();
                var afterDisplayOnly = link.SettingsNotifications;

                // Host model setting change notifies guests once.
                settings.FreightMode = Core.BrakingMode.Passenger;
                settings.OnChange();
                var afterHostChange = link.SettingsNotifications;

                // Session-wide guest lock: adopt host model settings, then mutate a host-owned field externally
                // (outside UMM Draw) and call OnChange(); host model reverts with no spurious rebuild while client-only persists.
                link.IsGuest = true;
                var ownBeforeJoin = Sim.Net.ModelSettings.From(settings);
                var hostModel = ownBeforeJoin;
                hostModel.Equipment = Equipment.American;
                hostModel.BrakeForceFromCar = false;
                var otherFlavor = RealisticBrakes.Main.BuildFlavor == "public" ? "test" : "public";
                Sim.Net.AdoptHostSettings(hostModel, "1.4.4", otherFlavor);
                var warnsAfterFirstMismatch = warnings.Count;

                // Repeated AdoptHostSettings with the same mismatch does not spam Notice.Warn again.
                var hostModel2 = hostModel;
                hostModel2.LoadBraking = LoadBraking.EmptyLoad;
                Sim.Net.AdoptHostSettings(hostModel2, "1.4.4", otherFlavor);
                var warnsAfterRepeatMismatch = warnings.Count;

                // Clear BrakeSets.stale so we can assert external guest mutation triggers no spurious rebuild.
                staleField.SetValue(null, false);
                settings.Equipment = Equipment.European;
                settings.BrakeForceFromCar = true;
                settings.ShowOverlay = false;
                settings.OnChange();
                var guestRevertedModel = hostModel2.SameAs(Sim.Net.ModelSettings.From(settings))
                    && settings.Equipment == Equipment.American && !settings.BrakeForceFromCar;
                var noSpuriousRebuild = !Sim.BrakeSets.Stale;
                var guestKeptDisplay = !settings.ShowOverlay;
                var guestDidNotNotify = link.SettingsNotifications == afterHostChange;

                // Disconnect restores guest's pre-session singleplayer model settings while keeping client-only changes,
                // and resets mismatch warning deduplication for the next session.
                Sim.Net.EndSession();
                Sim.Net.RestoreOwnSettings();
                var restoredOwn = ownBeforeJoin.SameAs(Sim.Net.ModelSettings.From(settings))
                    && settings.Equipment == Equipment.European && !settings.ShowOverlay
                    && Sim.Net.HostSettings == null && Sim.Net.OwnSettings == null;
                Sim.Net.AdoptHostSettings(hostModel, "1.4.4", otherFlavor);
                var warnsInNewSession = warnings.Count;
                Sim.Net.EndSession();
                Sim.Net.RestoreOwnSettings();

                Check(link.Ticks == 1 && afterDisplayOnly == 0 && afterHostChange == 1
                      && guestRevertedModel && noSpuriousRebuild && guestKeptDisplay && guestDidNotNotify && restoredOwn,
                    "Net.Tick reaches link, Settings.OnChange notifies guests only on host model changes, and session-wide guest lock reverts external mutations with no rebuild while disconnect restores own settings");
                Check(warnsAfterFirstMismatch == 1 && warnsAfterRepeatMismatch == 1 && warnsInNewSession == 2,
                    "Net.AdoptHostSettings deduplicates version/flavor mismatch warnings per session and resets on disconnect");
            }
            finally
            {
                Sim.Net.EndSession();
                Sim.Net.RestoreOwnSettings();
                Sim.Net.Link = prevLink;
                settingsProp.SetValue(null, prevSettings);
                staleField.SetValue(null, false);
                Notice.WarnObserver = prevWarn;
                Notice.ShowObserver = prevShow;
            }
        }

        private static void CheckMultiplayerPackets()
        {
            var dv = Environment.GetEnvironmentVariable("DV_INSTALL_DIR");
            if (string.IsNullOrEmpty(dv))
                dv = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "Steam", "steamapps", "common", "Derail Valley");
            var mpApiPath = Path.Combine(dv, "Mods", "Multiplayer", "MultiplayerAPI.dll");
            var mpModPath = Environment.GetEnvironmentVariable("MP_DLL");
            if (string.IsNullOrEmpty(mpModPath))
                mpModPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "RealisticBrakes.Multiplayer", "bin", "Release", "net48", "RealisticBrakes.Multiplayer.dll"));
            if (!File.Exists(mpApiPath) || !File.Exists(mpModPath))
            {
                Check(true, "MultiplayerAPI.dll or RealisticBrakes.Multiplayer.dll not present; skipping multiplayer packet round-trip checks");
                return;
            }

            ResolveEventHandler resolver = (_, args) =>
            {
                var name = new AssemblyName(args.Name).Name;
                if (name == "MultiplayerAPI")
                    return Assembly.LoadFrom(mpApiPath);
                if (name == "RealisticBrakes")
                    return typeof(RealisticBrakes.Main).Assembly;
                if (name == "RealisticBrakes.Core")
                    return typeof(Core.CarBrake).Assembly;
                return null;
            };
            AppDomain.CurrentDomain.AssemblyResolve += resolver;
            try
            {
                var mpAsm = Assembly.Load(File.ReadAllBytes(mpModPath));
                var linkType = mpAsm.GetType("RealisticBrakes.Sim.MultiplayerLink", true);
                var actionType = mpAsm.GetType("RealisticBrakes.Sim.ActionPacket", true);
                var joinType = mpAsm.GetType("RealisticBrakes.Sim.JoinPacket", true);
                var settingsType = mpAsm.GetType("RealisticBrakes.Sim.SettingsPacket", true);
                Check(typeof(Sim.INetLink).IsAssignableFrom(linkType), "RealisticBrakes.Multiplayer.dll loads and MultiplayerLink implements INetLink");

                // Per-holder key check: BleedHold and Adjust key per holder; Lever does not.
                var keyMethod = linkType.GetMethod("Key", BindingFlags.Static | BindingFlags.NonPublic);
                object MakeAction(ushort netId, Sim.NetActionKind kind, byte sub, byte val, byte player, uint seq)
                {
                    var p = Activator.CreateInstance(actionType);
                    actionType.GetField("NetId").SetValue(p, netId);
                    actionType.GetField("Kind").SetValue(p, (byte)kind);
                    actionType.GetField("Sub").SetValue(p, sub);
                    actionType.GetField("Value").SetValue(p, val);
                    actionType.GetField("Player").SetValue(p, player);
                    actionType.GetField("Sequence").SetValue(p, seq);
                    return p;
                }
                var kAdj1 = keyMethod.Invoke(null, new[] { MakeAction(5, Sim.NetActionKind.Adjust, 0, 1, 2, 1) });
                var kAdj2 = keyMethod.Invoke(null, new[] { MakeAction(5, Sim.NetActionKind.Adjust, 0, 1, 3, 2) });
                var kBleed1 = keyMethod.Invoke(null, new[] { MakeAction(5, Sim.NetActionKind.BleedHold, 0, 1, 2, 1) });
                var kBleed2 = keyMethod.Invoke(null, new[] { MakeAction(5, Sim.NetActionKind.BleedHold, 0, 1, 3, 2) });
                var kLev1 = keyMethod.Invoke(null, new[] { MakeAction(5, Sim.NetActionKind.Lever, 1, 0, 2, 1) });
                var kLev2 = keyMethod.Invoke(null, new[] { MakeAction(5, Sim.NetActionKind.Lever, 1, 1, 3, 2) });
                Check(!Equals(kAdj1, kAdj2) && !Equals(kBleed1, kBleed2) && Equals(kLev1, kLev2),
                    "MultiplayerLink.Key keys BleedHold and Adjust per holder and Lever per control");

                // ActionPacket round-trip.
                var actionIn = MakeAction(42, Sim.NetActionKind.Adjust, 1, 1, 7, 12345);
                var actionOut = RoundTripPacket(actionType, actionIn, out var actionAtEnd);
                Check(actionAtEnd
                      && (ushort)actionType.GetField("NetId").GetValue(actionOut) == 42
                      && (byte)actionType.GetField("Kind").GetValue(actionOut) == (byte)Sim.NetActionKind.Adjust
                      && (byte)actionType.GetField("Sub").GetValue(actionOut) == 1
                      && (byte)actionType.GetField("Value").GetValue(actionOut) == 1
                      && (byte)actionType.GetField("Player").GetValue(actionOut) == 7
                      && (uint)actionType.GetField("Sequence").GetValue(actionOut) == 12345,
                    "ActionPacket round-trips cleanly through BinaryWriter/BinaryReader");

                // JoinPacket full round-trip (with Adjust state and build flavor).
                var model = new Sim.Net.ModelSettings
                {
                    Enabled = false,
                    Equipment = Equipment.American,
                    BrakeForceFromCar = false,
                    FreightMode = Core.BrakingMode.Passenger,
                    ManualEmptyLoaded = true,
                    WidenTrainBrakeHandles = false,
                    LoadBraking = LoadBraking.EmptyLoad,
                    DirectAdmission = false,
                    EveryEngineUsesSetting = true,
                };
                var joinIn = Activator.CreateInstance(joinType);
                joinType.GetField("Version").SetValue(joinIn, "1.4.5");
                joinType.GetField("Flavor").SetValue(joinIn, RealisticBrakes.Main.BuildFlavor);
                joinType.GetField("Settings").SetValue(joinIn, model);
                joinType.GetField("Sequence").SetValue(joinIn, (uint)99);
                var statesIn = (System.Collections.IList)joinType.GetField("States").GetValue(joinIn);
                statesIn.Add(MakeAction(10, Sim.NetActionKind.BleedHold, 0, 1, 2, 0));
                statesIn.Add(MakeAction(20, Sim.NetActionKind.Adjust, 0, 1, 3, 0));
                var carsIn = (List<ushort>)joinType.GetField("Cars").GetValue(joinIn);
                carsIn.Add(10);
                carsIn.Add(20);

                var joinOut = RoundTripPacket(joinType, joinIn, out var joinAtEnd);
                var joinSettingsOut = (Sim.Net.ModelSettings)joinType.GetField("Settings").GetValue(joinOut);
                var joinStatesOut = (System.Collections.IList)joinType.GetField("States").GetValue(joinOut);
                var joinCarsOut = (List<ushort>)joinType.GetField("Cars").GetValue(joinOut);
                Check(joinAtEnd
                      && (string)joinType.GetField("Version").GetValue(joinOut) == "1.4.5"
                      && (string)joinType.GetField("Flavor").GetValue(joinOut) == RealisticBrakes.Main.BuildFlavor
                      && (uint)joinType.GetField("Sequence").GetValue(joinOut) == 99
                      && joinSettingsOut.SameAs(model)
                      && joinStatesOut.Count == 2
                      && (byte)actionType.GetField("Kind").GetValue(joinStatesOut[1]) == (byte)Sim.NetActionKind.Adjust
                      && joinCarsOut.SequenceEqual(new ushort[] { 10, 20 }),
                    "JoinPacket round-trips version, flavor, model settings, Adjust/BleedHold states, and car list");

                // Backward-compatible deserialization of older truncated JoinPacket streams:
                // 1) Pre-Cars stream (ends right after States).
                byte[] legacyPreCars;
                using (var ms = new MemoryStream())
                using (var w = new BinaryWriter(ms))
                {
                    w.Write("1.0.0");
                    w.Write(true);
                    w.Write((byte)Equipment.European);
                    w.Write(true);
                    w.Write((byte)Core.BrakingMode.Goods);
                    w.Write(false);
                    w.Write(true);
                    w.Write((uint)12);
                    w.Write(1);
                    w.Write((ushort)7);
                    w.Write((byte)Sim.NetActionKind.BleedHold);
                    w.Write((byte)0);
                    w.Write((byte)1);
                    w.Write((byte)4);
                    legacyPreCars = ms.ToArray();
                }
                var legacy1 = DeserializePacket(joinType, legacyPreCars);
                var legacy1Settings = (Sim.Net.ModelSettings)joinType.GetField("Settings").GetValue(legacy1);
                var legacy1Cars = (List<ushort>)joinType.GetField("Cars").GetValue(legacy1);
                var legacy1States = (System.Collections.IList)joinType.GetField("States").GetValue(legacy1);

                // 2) Pre-Flavor stream (ends right after EveryEngineUsesSetting).
                byte[] legacyPreFlavor;
                using (var ms = new MemoryStream())
                using (var w = new BinaryWriter(ms))
                {
                    w.Write("1.4.5");
                    w.Write(model.Enabled);
                    w.Write((byte)model.Equipment);
                    w.Write(model.BrakeForceFromCar);
                    w.Write((byte)model.FreightMode);
                    w.Write(model.ManualEmptyLoaded);
                    w.Write(model.WidenTrainBrakeHandles);
                    w.Write((uint)55);
                    w.Write(0);
                    w.Write(1);
                    w.Write((ushort)11);
                    w.Write((byte)model.LoadBraking);
                    w.Write(model.DirectAdmission);
                    w.Write(model.EveryEngineUsesSetting);
                    legacyPreFlavor = ms.ToArray();
                }
                var legacy2 = DeserializePacket(joinType, legacyPreFlavor);
                var legacy2Settings = (Sim.Net.ModelSettings)joinType.GetField("Settings").GetValue(legacy2);
                var legacy2Cars = (List<ushort>)joinType.GetField("Cars").GetValue(legacy2);
                Check((string)joinType.GetField("Version").GetValue(legacy1) == "1.0.0"
                      && (string)joinType.GetField("Flavor").GetValue(legacy1) == ""
                      && legacy1States.Count == 1 && legacy1Cars.Count == 0
                      && legacy1Settings.LoadBraking == LoadBraking.EmptyLoad
                      && legacy1Settings.DirectAdmission && legacy1Settings.EveryEngineUsesSetting
                      && (string)joinType.GetField("Version").GetValue(legacy2) == "1.4.5"
                      && (string)joinType.GetField("Flavor").GetValue(legacy2) == ""
                      && legacy2Cars.SequenceEqual(new ushort[] { 11 })
                      && legacy2Settings.SameAs(model),
                    "JoinPacket deserializes older truncated streams (pre-Cars and pre-Flavor) with backward-compatible defaults");

                // SettingsPacket round-trip (full and truncated without trailing Flavor).
                var settingsIn = Activator.CreateInstance(settingsType);
                settingsType.GetField("Version").SetValue(settingsIn, "1.4.5");
                settingsType.GetField("Flavor").SetValue(settingsIn, RealisticBrakes.Main.BuildFlavor);
                settingsType.GetField("Settings").SetValue(settingsIn, model);
                settingsType.GetField("Sequence").SetValue(settingsIn, (uint)77);
                var settingsOut = RoundTripPacket(settingsType, settingsIn, out var settingsAtEnd);
                var settingsModelOut = (Sim.Net.ModelSettings)settingsType.GetField("Settings").GetValue(settingsOut);

                byte[] settingsNoFlavor;
                using (var ms = new MemoryStream())
                using (var w = new BinaryWriter(ms))
                {
                    w.Write("1.4.5");
                    w.Write(model.Enabled);
                    w.Write((byte)model.Equipment);
                    w.Write(model.BrakeForceFromCar);
                    w.Write((byte)model.FreightMode);
                    w.Write(model.ManualEmptyLoaded);
                    w.Write(model.WidenTrainBrakeHandles);
                    w.Write((byte)model.LoadBraking);
                    w.Write(model.DirectAdmission);
                    w.Write(model.EveryEngineUsesSetting);
                    w.Write((uint)88);
                    settingsNoFlavor = ms.ToArray();
                }
                var settingsTrunc = DeserializePacket(settingsType, settingsNoFlavor);
                var settingsTruncModel = (Sim.Net.ModelSettings)settingsType.GetField("Settings").GetValue(settingsTrunc);
                Check(settingsAtEnd
                      && (string)settingsType.GetField("Version").GetValue(settingsOut) == "1.4.5"
                      && (string)settingsType.GetField("Flavor").GetValue(settingsOut) == RealisticBrakes.Main.BuildFlavor
                      && (uint)settingsType.GetField("Sequence").GetValue(settingsOut) == 77
                      && settingsModelOut.SameAs(model)
                      && (string)settingsType.GetField("Flavor").GetValue(settingsTrunc) == ""
                      && (uint)settingsType.GetField("Sequence").GetValue(settingsTrunc) == 88
                      && settingsTruncModel.SameAs(model),
                    "SettingsPacket round-trips full and flavor-omitted streams cleanly");

                // Join-snapshot timeout via Tick() without a second packet, late JoinPacket safety, same-sequence dedup,
                // and SettingsPacket peer capability gating.
                CheckMultiplayerLinkBehavior(linkType, joinType, settingsType, MakeAction, model, legacy2, joinOut);
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= resolver;
            }
        }

        private static void CheckMultiplayerLinkBehavior(
            Type linkType,
            Type joinType,
            Type settingsType,
            Func<ushort, Sim.NetActionKind, byte, byte, byte, uint, object> makeAction,
            Sim.Net.ModelSettings model,
            object legacyJoin,
            object currentJoin)
        {
            const BindingFlags priv = BindingFlags.Instance | BindingFlags.NonPublic;
            const BindingFlags anyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var settingsProp = AccessTools.Property(typeof(RealisticBrakes.Main), nameof(RealisticBrakes.Main.Settings));
            var prevSettings = settingsProp.GetValue(null);
            var prevWarn = Notice.WarnObserver;
            var prevShow = Notice.ShowObserver;
            var warnings = new List<string>();
            var notices = new List<string>();
            Notice.WarnObserver = warnings.Add;
            Notice.ShowObserver = notices.Add;
            try
            {
                var guestSettings = new Settings();
                guestSettings.AcceptLoaded();
                settingsProp.SetValue(null, guestSettings);

                var link = (Sim.INetLink)Activator.CreateInstance(linkType);
                var isReadyProp = linkType.GetProperty("IsClientReady", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var defaultIsReady = (Func<bool>)isReadyProp.GetValue(link);
                // Verify the real DefaultClientReady() across all four states:
                // 1) Main menu (no WorldStreamingInit instance): false, even though WorldStreamingInit.IsLoaded is true.
                // 2) World streaming (_instance present, isLoaded=false): false.
                // 3) DV Multiplayer SyncWorldState (_instance present, isLoaded=true, LoadingScreenManager.IsLoading=true): false.
                // 4) Ready (_instance present, isLoaded=true, LoadingScreenManager.IsLoading=false): true.
                var falseInMainMenu = !defaultIsReady();
                var wsiInstanceField = AccessTools.Field(typeof(DV.Utils.SingletonBehaviour<WorldStreamingInit>), "_instance");
                var wsiLoadedField = AccessTools.Field(typeof(WorldStreamingInit), "isLoaded");
                var lsmLoadingSetter = AccessTools.PropertySetter(typeof(LoadingScreenManager), nameof(LoadingScreenManager.IsLoading));
                var prevWsi = wsiInstanceField.GetValue(null);
                var prevWsiLoaded = (bool)wsiLoadedField.GetValue(null);
                var prevLsmLoading = LoadingScreenManager.IsLoading;
                bool defaultFalseWhileStreaming, defaultFalseWhileMpLoading, defaultTrueWhenReady;
                try
                {
                    var fakeWsi = (WorldStreamingInit)FormatterServices.GetUninitializedObject(typeof(WorldStreamingInit));
                    AccessTools.Field(typeof(UnityEngine.Object), "m_CachedPtr")?.SetValue(fakeWsi, new IntPtr(201));
                    AccessTools.Field(typeof(UnityEngine.Object), "m_InstanceID")?.SetValue(fakeWsi, 201);
                    wsiInstanceField.SetValue(null, fakeWsi);
                    wsiLoadedField.SetValue(null, false);
                    lsmLoadingSetter.Invoke(null, new object[] { true });
                    defaultFalseWhileStreaming = !defaultIsReady();

                    wsiLoadedField.SetValue(null, true);
                    lsmLoadingSetter.Invoke(null, new object[] { true });
                    defaultFalseWhileMpLoading = !defaultIsReady();

                    lsmLoadingSetter.Invoke(null, new object[] { false });
                    defaultTrueWhenReady = defaultIsReady();
                }
                finally
                {
                    wsiInstanceField.SetValue(null, prevWsi);
                    wsiLoadedField.SetValue(null, prevWsiLoaded);
                    lsmLoadingSetter.Invoke(null, new object[] { prevLsmLoading });
                }
                Check(falseInMainMenu && defaultFalseWhileStreaming && defaultFalseWhileMpLoading && defaultTrueWhenReady,
                    "MultiplayerLink.DefaultClientReady is false in main menu, during world streaming, and while LoadingScreenManager.IsLoading is true, and true once loaded and dismissed");

                double now = 100.0;
                bool worldLoaded = false;
                bool mpSyncComplete = false;
                linkType.GetProperty("Clock", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    .SetValue(link, new Func<double>(() => now));
                isReadyProp.SetValue(link, new Func<bool>(() => worldLoaded && mpSyncComplete));

                // 1) Join-snapshot timer stays unset in main menu, during world streaming, and while DV Multiplayer
                // SyncWorldState holds the loading screen open; resets if readiness drops back to false; then expires
                // 15 s after multiplayer load completes via Tick() with no second packet.
                linkType.GetMethod("BeginClientWait", priv).Invoke(link, null);
                var onClientAction = linkType.GetMethod("OnClientAction", priv);
                now = 101.0;
                onClientAction.Invoke(link, new[] { makeAction(10, Sim.NetActionKind.Adjust, 0, 1, 2, 5) });
                onClientAction.Invoke(link, new[] { makeAction(11, Sim.NetActionKind.Adjust, 0, 1, 3, 2) });

                var joinedField = linkType.GetField("joined", priv);
                var waitingSinceField = linkType.GetField("waitingSince", priv);
                var snapshotReceivedField = linkType.GetField("snapshotReceived", priv);
                var snapshotField = linkType.GetField("snapshot", priv);
                var earlyDict = (System.Collections.IDictionary)linkType.GetField("early", priv).GetValue(link);
                var fromHostDict = (System.Collections.IDictionary)linkType.GetField("fromHost", priv).GetValue(link);

                // Spend > 15 s in main menu / world streaming: NO timeout and NO warning.
                now = 120.0;
                link.Tick();
                var noTimeoutWhileStreaming = !(bool)joinedField.GetValue(link)
                    && double.IsNaN((double)waitingSinceField.GetValue(link))
                    && earlyDict.Count == 2 && fromHostDict.Count == 0 && warnings.Count == 0;

                // WorldStreamingInit finishes, but DV Multiplayer SyncWorldState is still running for > 15 s: NO timeout!
                worldLoaded = true;
                now = 140.0;
                link.Tick();
                var noTimeoutDuringMpSync = !(bool)joinedField.GetValue(link)
                    && double.IsNaN((double)waitingSinceField.GetValue(link))
                    && earlyDict.Count == 2 && fromHostDict.Count == 0 && warnings.Count == 0;

                // Readiness flickers true briefly at t=140, then drops back to false at t=142: waitingSince must reset to NaN!
                mpSyncComplete = true;
                link.Tick();
                var startedAtFlicker = !double.IsNaN((double)waitingSinceField.GetValue(link)) && warnings.Count == 0;
                mpSyncComplete = false;
                now = 142.0;
                link.Tick();
                var resetOnNotReady = double.IsNaN((double)waitingSinceField.GetValue(link)) && !(bool)joinedField.GetValue(link) && warnings.Count == 0;

                // Stay not-ready past original t=140+15 s (now=160), then become ready: first Tick() starts fresh 15 s clock without timing out.
                now = 160.0;
                mpSyncComplete = true;
                link.Tick();
                var clockStartedOnReady = !(bool)joinedField.GetValue(link) && (double)waitingSinceField.GetValue(link) == 160.0
                    && earlyDict.Count == 2 && warnings.Count == 0;

                now = 174.9;
                link.Tick();
                var stillWaiting = !(bool)joinedField.GetValue(link) && earlyDict.Count == 2 && fromHostDict.Count == 0 && warnings.Count == 0;

                // Advance past 15 s in silence and call Tick() with NO second packet delivered.
                now = 175.0;
                link.Tick();
                var flushedOnTick = (bool)joinedField.GetValue(link) && earlyDict.Count == 0 && fromHostDict.Count == 2 && warnings.Count == 1;
                var key10 = ((ushort)10, (byte)Sim.NetActionKind.Adjust, (byte)0, (byte)2);
                var key11 = ((ushort)11, (byte)Sim.NetActionKind.Adjust, (byte)0, (byte)3);
                var settledSeqs = (uint)fromHostDict[key10] == 5 && (uint)fromHostDict[key11] == 2;

                // Subsequent ticks do not warn again.
                now = 180.0;
                link.Tick();
                var warnedOnce = warnings.Count == 1;

                // Late JoinPacket at seq 4 applies host settings safely without downgrading seq 5 in fromHost.
                var lateJoin = Activator.CreateInstance(joinType);
                joinType.GetField("Version").SetValue(lateJoin, "");
                joinType.GetField("Flavor").SetValue(lateJoin, RealisticBrakes.Main.BuildFlavor);
                joinType.GetField("Settings").SetValue(lateJoin, model);
                joinType.GetField("Sequence").SetValue(lateJoin, (uint)4);
                ((System.Collections.IList)joinType.GetField("States").GetValue(lateJoin))
                    .Add(makeAction(10, Sim.NetActionKind.Adjust, 0, 0, 2, 0));
                linkType.GetMethod("OnJoin", priv).Invoke(link, new[] { lateJoin });
                var lateJoinApplied = (bool)snapshotReceivedField.GetValue(link)
                    && (uint)snapshotField.GetValue(link) == 4
                    && model.SameAs(Sim.Net.ModelSettings.From(guestSettings))
                    && (uint)fromHostDict[key10] == 5;

                // Same-sequence SettingsPacket (seq 4) is ignored as already up to date; newer (seq 5) applies.
                var onSettings = linkType.GetMethod("OnSettings", priv);
                var sameSeqPacket = Activator.CreateInstance(settingsType);
                var altModel = model;
                altModel.FreightMode = Core.BrakingMode.Goods;
                settingsType.GetField("Version").SetValue(sameSeqPacket, "");
                settingsType.GetField("Flavor").SetValue(sameSeqPacket, RealisticBrakes.Main.BuildFlavor);
                settingsType.GetField("Settings").SetValue(sameSeqPacket, altModel);
                settingsType.GetField("Sequence").SetValue(sameSeqPacket, (uint)4);
                onSettings.Invoke(link, new[] { sameSeqPacket });
                var ignoredSameSeq = model.SameAs(Sim.Net.ModelSettings.From(guestSettings));

                settingsType.GetField("Sequence").SetValue(sameSeqPacket, (uint)5);
                onSettings.Invoke(link, new[] { sameSeqPacket });
                var appliedNewerSeq = altModel.SameAs(Sim.Net.ModelSettings.From(guestSettings));

                Check(falseInMainMenu && noTimeoutWhileStreaming && noTimeoutDuringMpSync && startedAtFlicker
                      && resetOnNotReady && clockStartedOnReady && stillWaiting && flushedOnTick && settledSeqs
                      && warnedOnce && lateJoinApplied && ignoredSameSeq && appliedNewerSeq,
                    "MultiplayerLink DefaultClientReady is false in main menu, waits through world streaming and MP sync, resets waitingSince if readiness drops, expires 15 s after readiness via Tick() without a second packet, applies late JoinPacket safely, and skips same-sequence settings updates");

                // 2) SettingsPacket capability gating: older hosts aren't acked, older guests aren't sent SettingsPacket,
                // capable guests receive updates and RTT catch-up, and disconnecting guests are forgotten.
                var shouldAckJoin = linkType.GetMethod("ShouldAckJoin", anyStatic);
                var shouldSend = linkType.GetMethod("ShouldSendSettingsTo", priv);
                var tryRecordAck = linkType.GetMethod("TryRecordSettingsAck", priv);
                var forgetGuest = linkType.GetMethod("ForgetGuest", priv);
                var joinSentAt = (Dictionary<byte, uint>)linkType.GetField("joinSentAt", priv).GetValue(link);
                var settingsChangedAt = linkType.GetField("settingsChangedAt", priv);

                var skipOlderHostAck = !(bool)shouldAckJoin.Invoke(null, new[] { legacyJoin });
                var ackNewerHost = (bool)shouldAckJoin.Invoke(null, new[] { currentJoin });
                var skipOlderGuest = !(bool)shouldSend.Invoke(link, new object[] { (byte)2, false });
                var ackArgs = new object[] { (byte)3, (uint)0 };
                var noCatchUpBeforeChange = !(bool)tryRecordAck.Invoke(link, ackArgs);
                var sendToCapableGuest = (bool)shouldSend.Invoke(link, new object[] { (byte)3, false });
                var skipHostSelf = !(bool)shouldSend.Invoke(link, new object[] { (byte)3, true });

                // Mid-session settings change while player 4's join ack is in flight triggers catch-up on ack.
                joinSentAt[4] = 5;
                settingsChangedAt.SetValue(link, (uint)6);
                var catchUpArgs = new object[] { (byte)4, (uint)0 };
                var needsCatchUp = (bool)tryRecordAck.Invoke(link, catchUpArgs) && (uint)catchUpArgs[1] == 6;

                forgetGuest.Invoke(link, new object[] { (byte)3 });
                var clearedOnDisconnect = !(bool)shouldSend.Invoke(link, new object[] { (byte)3, false });

                Check(skipOlderHostAck && ackNewerHost && skipOlderGuest && noCatchUpBeforeChange
                      && sendToCapableGuest && skipHostSelf && needsCatchUp && clearedOnDisconnect,
                    "SettingsPacket is gated on join-flavor capability ack (sparing older hosts and guests), catches up mid-join changes, and clears on disconnect");
            }
            finally
            {
                Sim.Net.EndSession();
                Sim.Net.RestoreOwnSettings();
                settingsProp.SetValue(null, prevSettings);
                Notice.WarnObserver = prevWarn;
                Notice.ShowObserver = prevShow;
            }
        }

        private static object RoundTripPacket(Type packetType, object input, out bool atEnd)
        {
            byte[] bytes;
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                packetType.GetMethod("Serialize").Invoke(input, new object[] { writer });
                writer.Flush();
                bytes = ms.ToArray();
            }
            using (var ms = new MemoryStream(bytes))
            using (var reader = new BinaryReader(ms))
            {
                var output = Activator.CreateInstance(packetType);
                packetType.GetMethod("Deserialize").Invoke(output, new object[] { reader });
                atEnd = ms.Position == ms.Length;
                return output;
            }
        }

        private static object DeserializePacket(Type packetType, byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
            using (var reader = new BinaryReader(ms))
            {
                var output = Activator.CreateInstance(packetType);
                packetType.GetMethod("Deserialize").Invoke(output, new object[] { reader });
                return output;
            }
        }

        private static void Check(bool ok, string what)
        {
            Console.WriteLine((ok ? "ok   " : "FAIL ") + what);
            if (!ok)
                failures++;
        }

        // Non-engines (e.g. a slug or tender on an MU cable) cannot initiate a consist bail or multiplayer report;
        // a valid, already-released lead engine still bails MU-coupled engines and reports once.
        private static void CheckBailGuard()
        {
            var geometry = RealisticBrakes.Core.PipeGeometry.Uic();

            RealisticBrakes.Core.CarBrake MakeAppliedLoco()
            {
                var brake = new RealisticBrakes.Core.CarBrake(20, geometry, locomotive: new RealisticBrakes.Core.LocomotiveBrake(null, new RealisticBrakes.Core.DriverBrakeValve()));
                brake.ChargeTo(RealisticBrakes.Core.Air.FromGaugeBar(5.0));
                brake.Locomotive.MainReservoir.Pressure = RealisticBrakes.Core.Air.FromGaugeBar(8.0);
                brake.Locomotive.DriverValve.CutIn = false;
                brake.PipeSection.Pressure = RealisticBrakes.Core.Air.FromGaugeBar(4.0);
                for (var i = 0; i < 200; i++)
                    brake.Step(0.05);
                return brake;
            }

            var releasedLead = new RealisticBrakes.Core.CarBrake(20, geometry, locomotive: new RealisticBrakes.Core.LocomotiveBrake(null, new RealisticBrakes.Core.DriverBrakeValve()));
            releasedLead.ChargeTo(RealisticBrakes.Core.Air.FromGaugeBar(5.0));
            var trailingApplied = MakeAppliedLoco();
            var slug = new RealisticBrakes.Core.CarBrake(12, geometry);
            var wagon = new RealisticBrakes.Core.CarBrake(15, geometry, new RealisticBrakes.Core.UicDistributor());

            var slugNode = new BailTestCar(slug);
            var leadNode = new BailTestCar(releasedLead);
            var trailingNode = new BailTestCar(trailingApplied);
            bool TryGet(BailTestCar node, out RealisticBrakes.Core.CarBrake brake, out Action touch)
            {
                brake = node?.Brake;
                touch = node != null ? (Action)(() => node.Touches++) : null;
                return brake != null;
            }

            // 1) Slug MU-coupled to an applied engine: returns 0, never walks MU, preserves trailing application, 0 reports.
            var consistWalks = 0;
            var reports = new List<BailTestCar>();
            var beforeCylinder = trailingApplied.CylinderGauge;
            var slugBailed = Controls.IndependentBailClick.BailFull(
                slugNode,
                notice: false,
                TryGet,
                c => { consistWalks++; return new[] { slugNode, trailingNode }; },
                c => reports.Add(c));
            Check(beforeCylinder > 1.0 * RealisticBrakes.Core.Air.Bar
                  && slugBailed == 0
                  && consistWalks == 0
                  && trailingApplied.Locomotive.BailedOff == 0
                  && trailingApplied.CylinderGauge == beforeCylinder
                  && trailingNode.Touches == 0
                  && reports.Count == 0,
                "BailFull on a slug MU-coupled to an applied engine returns 0, skips MU traversal, preserves the engine's brake, and emits zero reports");

            // 2) Valid, already-released lead engine MU-coupled to an applied trailing engine: returns 0 for lead, bails trailing, emits one report.
            var leadBailed = Controls.IndependentBailClick.BailFull(
                leadNode,
                notice: false,
                TryGet,
                c => { consistWalks++; return new[] { leadNode, trailingNode }; },
                c => reports.Add(c));
            Check(leadBailed == 0
                  && consistWalks == 1
                  && trailingApplied.Locomotive.BailedOff > 1.0 * RealisticBrakes.Core.Air.Bar
                  && trailingNode.Touches == 1
                  && reports.Count == 1
                  && ReferenceEquals(reports[0], leadNode),
                "BailFull on a released lead engine MU-coupled to an applied trailing engine returns 0, bails the trailing engine, and emits one report");

            var spy = new SpyNetLink();
            var previousLink = Sim.Net.Link;
            try
            {
                Sim.Net.Link = spy;
                var bailed = RealisticBrakes.Api.BailOff(null);
                Check(releasedLead.CanBail && !wagon.CanBail && !slug.CanBail
                      && !Controls.IndependentBailClick.CanBailCar(null)
                      && bailed == 0f && spy.Reports == 0,
                    "Api.BailOff(null) returns 0 with zero reports and CarBrake.CanBail excludes wagons and slugs");
                var vPipe = RealisticBrakes.Core.PipeGeometry.VacuumTrainPipe();
                var vDiesel = new RealisticBrakes.Core.CarBrake(20, vPipe, vacuumEngine: new RealisticBrakes.Core.VacuumEngine(RealisticBrakes.Core.VacuumEngineSpec.Diesel()));
                vDiesel.ChargeTo(RealisticBrakes.Core.Vacuum.FromInches(21));
                vDiesel.VacuumEngine.CutIn = false;
                vDiesel.PipeSection.Pressure = RealisticBrakes.Core.Vacuum.FromInches(10);
                for (var i = 0; i < 100; i++)
                    vDiesel.Step(0.05);
                var vSteam = new RealisticBrakes.Core.CarBrake(20, vPipe, vacuumEngine: new RealisticBrakes.Core.VacuumEngine(RealisticBrakes.Core.VacuumEngineSpec.Steam()));
                var vWagon = new RealisticBrakes.Core.CarBrake(15, vPipe, new RealisticBrakes.Core.VacuumCylinder(RealisticBrakes.Core.VacuumCylinderSpec.Bogie()));

                var vSteamNode = new BailTestCar(vSteam);
                var vDieselNode = new BailTestCar(vDiesel);
                var vReports = new List<BailTestCar>();
                var vSteamBailed = Controls.IndependentBailClick.BailFull(
                    vSteamNode, notice: false, TryGet, _ => new[] { vSteamNode, vDieselNode }, c => vReports.Add(c));
                Check(vDiesel.CanBail && !vSteam.CanBail && !vWagon.CanBail
                      && vSteamBailed == 0 && vDiesel.VacuumEngine.BailedOff == 0 && vReports.Count == 0,
                    "vacuum BailFull rejects steam brakes without bailing coupled diesels or reporting");

                var vDieselBailed = Controls.IndependentBailClick.BailFull(
                    vDieselNode, notice: false, TryGet, _ => new[] { vDieselNode }, c => vReports.Add(c));
                Check(vDieselBailed > 0 && vDiesel.VacuumEngine.BailedOff > 0 && vReports.Count == 1,
                    "vacuum BailFull bails a vacuum diesel and reports once");
            }
            finally
            {
                Sim.Net.Link = previousLink;
            }
        }

        private sealed class BailTestCar
        {
            public BailTestCar(RealisticBrakes.Core.CarBrake brake) => Brake = brake;
            public RealisticBrakes.Core.CarBrake Brake { get; }
            public int Touches { get; set; }
        }

        private sealed class SpyNetLink : Sim.INetLink
        {
            public bool IsGuest => false;
            public int Reports { get; private set; }
            public void Report(Sim.NetAction action) => Reports++;
            public void NotifyHostSettingsChanged() { }
            public void Tick() { }
        }
    }
}
