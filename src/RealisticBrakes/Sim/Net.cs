using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RealisticBrakes.Controls;
using RealisticBrakes.Core;
using UnityEngine;

namespace RealisticBrakes.Sim
{
    /// <summary>What one player did to a car that the game's own multiplayer sync doesn't carry.</summary>
    internal enum NetActionKind : byte
    {
        /// <summary>A car lever moved: <see cref="NetAction.Sub"/> the <see cref="CarLeverKind"/>, the value its notch.</summary>
        Lever = 1,

        /// <summary>A bleed handle held past a short pull, or let go: the value 1 or 0. (A short pull is the game's own, synced already.)</summary>
        BleedHold = 2,

        /// <summary>An engine bailed off by its independent lever (with its multiple-unit consist).</summary>
        Bail = 3,

        /// <summary>A widened train brake handle's added notches: the value the <see cref="HandleZone"/>.</summary>
        HandleZone = 4,
    }

    /// <summary>One player's action on a car, to be done the same everywhere.</summary>
    internal readonly struct NetAction
    {
        public NetAction(TrainCar car, NetActionKind kind, byte sub, byte value)
        {
            Car = car;
            Kind = kind;
            Sub = sub;
            Value = value;
        }

        public TrainCar Car { get; }

        public NetActionKind Kind { get; }

        public byte Sub { get; }

        public byte Value { get; }
    }

    /// <summary>A multiplayer session, as the mod sees it.</summary>
    internal interface INetLink
    {
        /// <summary>Whether this game is a guest in another's session, whose physics the host runs.</summary>
        bool IsGuest { get; }

        /// <summary>Sends this game's player's <paramref name="action"/> on, already done here: a guest's to the host, the host's to its guests.</summary>
        void Report(NetAction action);
    }

 internal static class Net
    {
        /// <summary>The session's link, or null playing alone.</summary>
        public static INetLink Link { get; set; }

        /// <summary>Whether this game is a guest in another's session, whose physics the host runs.</summary>
        public static bool IsGuest => Link != null && Link.IsGuest;

        // Set while doing an action from the network, so it isn't sent back.
        private static bool applying;

        /// <summary>Sends this game's player's <paramref name="action"/> on; nothing playing alone, or while doing another's.</summary>
        public static void Report(NetAction action)
        {
            if (!applying && Link != null && action.Car != null)
                Link.Report(action);
        }

        /// <summary>
        /// Does a player's (<paramref name="player"/>) <paramref name="action"/>
        /// here, as the host settled it; <paramref name="own"/> when it is this
        /// game's player's own, confirmed by the host (its hold is already its
        /// own, and its handle's zone stays its own).
        /// </summary>
        public static void Apply(NetAction action, byte player, bool own = false)
        {
            var car = action.Car;
            if (car == null || car.brakeSystem == null)
                return;
            applying = true;
            try
            {
                switch (action.Kind)
                {
                    case NetActionKind.Lever:
                        SetLever(car, (CarLeverKind)action.Sub, action.Value);
                        break;
                    case NetActionKind.BleedHold:
                        if (!own)
                            SetHold(car, player, action.Value != 0);
                        break;
                    case NetActionKind.Bail:
                        // An E-T engine's graduated bail (its value in units, 0 for still held, with a hold for the network's
                        // delays) and its let-go; anything else is a full bail. An older build bails in full on any of them.
                        if (!Main.Active)
                            break;
                        if (action.Sub == IndependentBailClick.BailLetGo)
                            IndependentBailClick.LetGoConsist(car);
                        else if (action.Sub == IndependentBailClick.GraduatedBail)
                            IndependentBailClick.BailConsist(car, false, action.Value * IndependentBailClick.NetUnit, IndependentBailClick.RemoteHold);
                        else
                            IndependentBailClick.BailConsist(car, false);
                        break;
                    case NetActionKind.HandleZone:
                        TrainBrakeHandle.SetRemoteZone(car, (HandleZone)action.Value, own ? TrainBrakeHandle.LocalHand : player);
                        break;
                }
            }
            finally
            {
                applying = false;
            }
        }

        /// <summary>
        /// A lever setting, remembered for the session: the car's model takes it
        /// now if it can, and again whenever it is rebuilt (a joining guest's
        /// cars, another equipment taken from the host). A review, 2026-10-27,
        /// found settings lost to a rebuild, or rejected by a valve of the
        /// other family and never retried.
        /// </summary>
        private static void SetLever(TrainCar car, CarLeverKind kind, int notch)
        {
            levers.GetOrCreateValue(car)[kind] = (byte)notch;
            if (Main.Active && !BrakeSets.Stale && BrakeSets.TryGet(car.brakeSystem, out var air))
                CarLever.SetLever(air, kind, notch);
        }

        /// <summary>This game's player moved <paramref name="car"/>'s <paramref name="kind"/> lever to <paramref name="notch"/>: remembered for the session as any setting is.</summary>
        public static void RememberLever(TrainCar car, CarLeverKind kind, int notch)
        {
            if (Link != null && car != null)
                levers.GetOrCreateValue(car)[kind] = (byte)notch;
        }

        /// <summary>Who holds each car's bleed handle from elsewhere, for a joining guest.</summary>
        public static IEnumerable<(TrainCar Car, byte Player)> Holds()
        {
            foreach (var pair in holders)
                foreach (var player in pair.Value)
                    yield return (pair.Key, player);
        }

        // Who holds each car's bleed handle from elsewhere: it drains while anyone does (a review, 2026-10-27: one shared flag
        // let either of two holders' let-go end the other's hold, and a holder leaving the session left it draining).
        private static readonly Dictionary<TrainCar, HashSet<byte>> holders = new Dictionary<TrainCar, HashSet<byte>>();

        private static void SetHold(TrainCar car, byte player, bool held)
        {
            if (!holders.TryGetValue(car, out var set))
                holders[car] = set = new HashSet<byte>();
            if (held)
                set.Add(player);
            else
                set.Remove(player);
            if (set.Count == 0)
                holders.Remove(car);
            if (car != null && car.brakeSystem != null && BrakeSets.TryGet(car.brakeSystem, out var air))
            {
                air.RemoteReleaseHeld = set.Count > 0;
                air.Touch();
            }
        }

        /// <summary>
        /// A player leaves the session (<paramref name="player"/>; everyone,
        /// when null): their hands come off the bleed handles and the sprung
        /// filling strokes. Returns what ended, for the host to tell its guests.
        /// </summary>
        public static List<NetAction> ReleaseHands(byte? player)
        {
            var ended = new List<NetAction>();
            foreach (var pair in new List<KeyValuePair<TrainCar, HashSet<byte>>>(holders))
                foreach (var holder in new List<byte>(pair.Value))
                    if (player == null || holder == player)
                    {
                        SetHold(pair.Key, holder, false);
                        ended.Add(new NetAction(pair.Key, NetActionKind.BleedHold, 0, 0));
                    }
            foreach (var car in TrainBrakeHandle.ReleaseHands(player))
                ended.Add(new NetAction(car, NetActionKind.HandleZone, 0, (byte)HandleZone.Travel));
            return ended;
        }

        /// <summary>
        /// The game resets <paramref name="system"/>'s car for reuse: what the
        /// session held for it goes, so the next car it becomes starts afresh
        /// (a review, 2026-10-27: a reused car took a cut-out setting from the
        /// car it had been, over its own saved one).
        /// </summary>
        public static void Forget(DV.Simulation.Brake.BrakeSystem system)
        {
            var car = system != null ? system.GetComponent<TrainCar>() : null;
            if (car != null)
            {
                levers.Remove(car);
                holders.Remove(car);
            }
            TrainBrakeHandle.Forget(system);
        }

        /// <summary>Who holds <paramref name="car"/>'s bleed handle from elsewhere.</summary>
        public static IEnumerable<byte> HoldersOf(TrainCar car) =>
            car != null && holders.TryGetValue(car, out var set) ? new List<byte>(set) : new List<byte>();

        /// <summary>The session ends: what it held is let go.</summary>
        public static void EndSession()
        {
            ReleaseHands(null);
            levers = new ConditionalWeakTable<TrainCar, Dictionary<CarLeverKind, byte>>();
        }

        // Each car's lever settings in the session, as last set by anyone.
        private static ConditionalWeakTable<TrainCar, Dictionary<CarLeverKind, byte>> levers = new ConditionalWeakTable<TrainCar, Dictionary<CarLeverKind, byte>>();

        /// <summary>Gives <paramref name="air"/>'s car, as its air is built, the session's lever settings and any hold on its bleed.</summary>
        public static void ApplyPending(CarAir air)
        {
            if (air?.Car == null)
                return;
            if (holders.TryGetValue(air.Car, out var held))
                air.RemoteReleaseHeld = held.Count > 0;
            if (!levers.TryGetValue(air.Car, out var settings))
                return;
            foreach (var pair in settings)
                CarLever.SetLever(air, pair.Key, pair.Value);
        }

        /// <summary>The model settings a host gives its guests: those that change how the air runs, not one player's view of it.</summary>
        internal struct ModelSettings
        {
            public bool Enabled;
            public Equipment Equipment;
            public bool BrakeForceFromCar;
            public BrakingMode FreightMode;
            public bool ManualEmptyLoaded;
            public bool WidenTrainBrakeHandles;
            public LoadBraking LoadBraking;

            public static ModelSettings From(Settings s) => new ModelSettings
            {
                Enabled = s.Enabled,
                Equipment = s.Equipment,
                BrakeForceFromCar = s.BrakeForceFromCar,
                FreightMode = s.FreightMode,
                ManualEmptyLoaded = s.ManualEmptyLoaded,
                WidenTrainBrakeHandles = s.WidenTrainBrakeHandles,
                LoadBraking = s.LoadBraking,
            };

            public void To(Settings s)
            {
                s.Enabled = Enabled;
                s.Equipment = Equipment;
                s.BrakeForceFromCar = BrakeForceFromCar;
                s.FreightMode = FreightMode;
                s.ManualEmptyLoaded = ManualEmptyLoaded;
                s.WidenTrainBrakeHandles = WidenTrainBrakeHandles;
                s.LoadBraking = LoadBraking;
            }

            public bool SameAs(ModelSettings o) =>
                Enabled == o.Enabled && Equipment == o.Equipment && BrakeForceFromCar == o.BrakeForceFromCar && FreightMode == o.FreightMode
                && ManualEmptyLoaded == o.ManualEmptyLoaded && WidenTrainBrakeHandles == o.WidenTrainBrakeHandles && LoadBraking == o.LoadBraking;
        }

        /// <summary>This player's own model settings while a host's are in use; null otherwise.</summary>
        internal static ModelSettings? OwnSettings { get; private set; }

        /// <summary>A guest takes its host's model settings for the session, keeping its own to give back.</summary>
        public static void AdoptHostSettings(ModelSettings host, string hostVersion)
        {
            var settings = Main.Settings;
            if (settings == null)
                return;
            var mine = ModelSettings.From(settings);
            if (OwnSettings == null)
                OwnSettings = mine;
            var equipment = settings.Equipment;
            host.To(settings);
            settings.AcceptLoaded();
            if (equipment != settings.Equipment)
                BrakeSets.Invalidate();
            // The cars' lever rows for the host's equipment and empty/loaded setting.
            CarLevers.RefitAll();
            var version = Main.Mod?.Info.Version;
            if (hostVersion != version)
                Notice.Show($"The host runs Realistic Brakes {hostVersion}, you {version}: brakes may differ");
            else if (!mine.SameAs(host))
                Notice.Show("Realistic Brakes: using the host's brake settings for this session");
        }

        /// <summary>Gives back this player's own model settings as the session ends.</summary>
        public static void RestoreOwnSettings()
        {
            var settings = Main.Settings;
            if (settings == null || OwnSettings == null)
                return;
            var equipment = settings.Equipment;
            OwnSettings.Value.To(settings);
            OwnSettings = null;
            settings.AcceptLoaded();
            if (equipment != settings.Equipment)
                BrakeSets.Invalidate();
            CarLevers.RefitAll();
        }
    }
}
