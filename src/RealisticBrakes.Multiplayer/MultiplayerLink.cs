using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DV.Utils;
using HarmonyLib;
using MPAPI;
using MPAPI.Interfaces;
using MPAPI.Interfaces.Packets;
using RealisticBrakes.Controls;
using RealisticBrakes.Core;

namespace RealisticBrakes.Sim
{
    /// <summary>
    /// The Multiplayer mod's session, through its API (MultiplayerAPI.dll,
    /// which that mod ships; this class is touched only when it's loaded, so
    /// the mod runs without it).
    /// <list type="bullet">
    /// <item>The host settles every state: a guest's action goes to the host,
    /// which does it, numbers it in its own order, and sends it to every
    /// guest, its sender too, so all end where the host does (a review,
    /// 2026-10-27, found two players' crossing moves leaving them apart). A
    /// bail, an event its sender has done, goes to the others only. The
    /// host's own actions go to every guest in the same order.</item>
    /// <item>The API sends reliable packets unordered, so each carries a
    /// sequence number, and a state older than one already seen for the same
    /// control is dropped: on the host, by each guest's own numbers; on a
    /// guest, by the host's. Both bleed handles and Angleichen (<c>Adjust</c>)
    /// holds are tracked per holder.</item>
    /// <item>A joining guest gets, in one <see cref="JoinPacket"/>, the host's
    /// version, build flavor (<c>public</c> or <c>test</c>, appended optionally
    /// for wire compatibility), model settings, and every car's settled states:
    /// lever settings, handles' added notches, and the whole set of each bleed
    /// and Angleichen holder so stale holds are cleared too. Until the snapshot
    /// arrives, the guest keeps what it hears, then applies what is newer than
    /// the snapshot in order (a review, 2026-10-27: a let-go heard before the
    /// car was there was lost, and the older hold heard after it stuck). If no
    /// snapshot arrives within 15 s after the guest finishes world streaming
    /// and multiplayer world sync (<see cref="WorldStreamingInit"/> present
    /// and loaded, and <see cref="LoadingScreenManager.IsLoading"/> false),
    /// the guest warns once, flushes queued early actions in sequence order,
    /// and still safely applies a late snapshot.</item>
    /// <item>Mid-session host model-setting changes propagate via
    /// <see cref="SettingsPacket"/> only to guests that acknowledged a newer
    /// host's <see cref="JoinPacket"/>, so older hosts and older guests never
    /// receive an unregistered packet type.</item>
    /// <item>A player leaving takes their hands off bleed handles, sprung
    /// filling strokes, and Angleichen keys, and the host tells its guests;
    /// their sequence numbers and capability tracking go with them, as the
    /// next player may take their ID.</item>
    /// </list>
    /// </summary>
    internal sealed class MultiplayerLink : INetLink
    {
        // The host's player ID when it has no client.
        private const byte Host = 0;

        // How long a guest waits for the host's join snapshot after world load before falling back to live updates.
        private const double JoinTimeoutSeconds = 15.0;

        private uint sequence;

        // Monotonic clock in seconds; overridden only by adapter checks to verify timeout expiry without sleeping.
        internal Func<double> Clock { get; set; } = () => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

        // Reads SingletonBehaviour<WorldStreamingInit>._instance without calling Instance (which logs a warning when null in the main menu).
        private static readonly AccessTools.FieldRef<WorldStreamingInit> worldStreamingInstance =
            AccessTools.StaticFieldRefAccess<WorldStreamingInit>(AccessTools.Field(typeof(SingletonBehaviour<WorldStreamingInit>), "_instance"));

        // Whether the guest is in the loaded world AND DV Multiplayer's loading screen has been dismissed
        // (DV Multiplayer holds LoadingScreenManager.IsLoading true through SyncWorldState until the host sends
        // ClientboundRemoveLoadingScreenPacket alongside OnPlayerReady, even if the host lacks this mod).
        // WorldStreamingInit.IsLoaded returns true in the main menu when _instance is null, so _instance != null is required.
        internal Func<bool> IsClientReady { get; set; } = DefaultClientReady;

        private static bool DefaultClientReady() =>
            worldStreamingInstance() != null
            && WorldStreamingInit.IsLoaded
            && !LoadingScreenManager.IsLoading;

        // On the host: the newest sequence number from each guest for each car's control, in that guest's numbering.
        private readonly Dictionary<(byte Sender, ushort NetId, byte Kind, byte Sub), uint> fromGuests = new Dictionary<(byte, ushort, byte, byte), uint>();

        // On the host: guests that acknowledged a JoinPacket carrying a build flavor and therefore have SettingsPacket registered.
        private readonly HashSet<byte> settingsCapable = new HashSet<byte>();

        // On the host: sequence number when JoinPacket was sent to each guest, and when host model settings last changed.
        private readonly Dictionary<byte, uint> joinSentAt = new Dictionary<byte, uint>();
        private uint settingsChangedAt;

        // On a guest: the newest of the host's sequence numbers for each car's control; a bleed or Angleichen hold's for each holder.
        private readonly Dictionary<(ushort NetId, byte Kind, byte Sub, byte Holder), uint> fromHost = new Dictionary<(ushort, byte, byte, byte), uint>();

        // The server whose players' comings and goings are followed, so a restart doesn't follow them twice.
        private IServer followed;

        // On a guest: whether the host's snapshot has come (or wait timed out), its sequence number (what it covers), and the
        // newest state heard before it for each control, so nothing newer is lost however long the wait (a review, 2026-10-27:
        // a capped list dropped the newest). A bail heard before is past.
        private bool joined;
        private bool waitingForJoin;
        private bool snapshotReceived;
        private bool joinTimedOut;
        private double waitingSince = double.NaN;
        private uint snapshot;
        private bool hasSettings;
        private uint settingsSequence;
        private readonly Dictionary<(ushort NetId, byte Kind, byte Sub, byte Holder), ActionPacket> early = new Dictionary<(ushort, byte, byte, byte), ActionPacket>();

        /// <summary>Links the mod to the Multiplayer mod's sessions, as they start and stop.</summary>
        public static void Start()
        {
            var link = new MultiplayerLink();
            Net.Link = link;
            MultiplayerAPI.ServerStarted += link.OnServerStarted;
            MultiplayerAPI.ClientStarted += link.OnClientStarted;
            MultiplayerAPI.ServerStopped += link.OnStopped;
            MultiplayerAPI.ClientStopped += link.OnStopped;
            if (MultiplayerAPI.Server != null)
                link.OnServerStarted(MultiplayerAPI.Server);
            if (MultiplayerAPI.Client != null)
                link.OnClientStarted(MultiplayerAPI.Client);
        }

        public bool IsGuest => MultiplayerAPI.Server == null && MultiplayerAPI.Client != null && MultiplayerAPI.Client.IsConnected;

        private static byte LocalPlayer => MultiplayerAPI.Client != null ? MultiplayerAPI.Client.PlayerId : Host;

        public void Report(NetAction action)
        {
            if (!TryGetNetId(action.Car, out var netId))
                return;
            var packet = new ActionPacket
            {
                NetId = netId, Kind = (byte)action.Kind, Sub = action.Sub, Value = action.Value, Player = LocalPlayer, Sequence = ++sequence,
            };
            var server = MultiplayerAPI.Server;
            var client = MultiplayerAPI.Client;
            if (server != null)
                server.SendSerializablePacketToAll(packet, true, true);
            else if (client != null && client.IsConnected)
            {
                if (!joined)
                    CheckJoinTimeout();
                client.SendSerializablePacketToServer(packet);
            }
        }

        public void NotifyHostSettingsChanged()
        {
            var server = MultiplayerAPI.Server;
            if (server == null || Main.Settings == null)
                return;
            var packet = SettingsPacket.Local(++sequence);
            settingsChangedAt = packet.Sequence;
            if (settingsCapable.Count == 0)
                return;
            foreach (var player in server.Players)
                if (player != null && ShouldSendSettingsTo(player.PlayerId, player.IsHost))
                    server.SendSerializablePacketToPlayer(packet, player, true);
        }

        public void Tick() => CheckJoinTimeout();

        internal bool ShouldSendSettingsTo(byte playerId, bool isHost) =>
            !isHost && settingsCapable.Contains(playerId);

        internal static bool ShouldAckJoin(JoinPacket packet) =>
            packet != null && !string.IsNullOrEmpty(packet.Flavor);

        internal bool TryRecordSettingsAck(byte playerId, out uint catchUpSequence)
        {
            settingsCapable.Add(playerId);
            if (settingsChangedAt > 0 && (!joinSentAt.TryGetValue(playerId, out var sentAt) || settingsChangedAt > sentAt))
            {
                catchUpSequence = settingsChangedAt;
                return true;
            }
            catchUpSequence = 0;
            return false;
        }

        internal void ForgetGuest(byte player)
        {
            settingsCapable.Remove(player);
            joinSentAt.Remove(player);
            foreach (var key in fromGuests.Keys.Where(k => k.Sender == player).ToList())
                fromGuests.Remove(key);
        }

        private void OnServerStarted(IServer server)
        {
            fromGuests.Clear();
            settingsCapable.Clear();
            joinSentAt.Clear();
            settingsChangedAt = 0;
            if (ReferenceEquals(server, followed))
                return;
            followed = server;
            server.RegisterSerializablePacket<ActionPacket>(OnServerAction);
            server.RegisterSerializablePacket<SettingsPacket>(OnServerSettingsAck);
            server.OnPlayerReady += player =>
            {
                var joinSeq = sequence;
                joinSentAt[player.PlayerId] = joinSeq;
                server.SendSerializablePacketToPlayer(JoinPacket.Local(joinSeq, LocalPlayer), player);
            };
            server.OnPlayerDisconnected += player => OnPlayerLeft(server, player.PlayerId);
        }

        // A newer guest's reply after receiving JoinPacket with a build flavor: it has SettingsPacket registered.
        private void OnServerSettingsAck(SettingsPacket packet, IPlayer sender)
        {
            if (sender == null)
                return;
            if (TryRecordSettingsAck(sender.PlayerId, out var catchUp))
                MultiplayerAPI.Server?.SendSerializablePacketToPlayer(SettingsPacket.Local(catchUp), sender, true);
        }

        private void OnPlayerLeft(IServer server, byte player)
        {
            ForgetGuest(player);
            // The guests hear the hands come off too.
            foreach (var action in Net.ReleaseHands(player))
                if (TryGetNetId(action.Car, out var netId))
                    server.SendSerializablePacketToAll(new ActionPacket
                    {
                        NetId = netId, Kind = (byte)action.Kind, Sub = action.Sub, Value = action.Value, Player = player, Sequence = ++sequence,
                    }, true, true);
        }

        private void OnClientStarted(IClient client)
        {
            BeginClientWait();
            client.RegisterSerializablePacket<ActionPacket>(OnClientAction);
            client.RegisterSerializablePacket<JoinPacket>(OnJoin);
            client.RegisterSerializablePacket<SettingsPacket>(OnSettings);
        }

        internal void BeginClientWait()
        {
            fromHost.Clear();
            joined = false;
            waitingForJoin = true;
            snapshotReceived = false;
            joinTimedOut = false;
            waitingSince = double.NaN;
            snapshot = 0;
            hasSettings = false;
            settingsSequence = 0;
            early.Clear();
        }

        private void OnStopped()
        {
            fromGuests.Clear();
            fromHost.Clear();
            settingsCapable.Clear();
            joinSentAt.Clear();
            settingsChangedAt = 0;
            joined = false;
            waitingForJoin = false;
            snapshotReceived = false;
            joinTimedOut = false;
            waitingSince = double.NaN;
            snapshot = 0;
            hasSettings = false;
            settingsSequence = 0;
            early.Clear();
            Net.EndSession();
            Net.RestoreOwnSettings();
        }

        // A guest's action: done here, the physics' side, and sent to every guest in the host's order.
        private void OnServerAction(ActionPacket packet, IPlayer sender)
        {
            var server = MultiplayerAPI.Server;
            if (server == null || !TryGetCar(packet.NetId, out var car) || !FreshFromGuest(sender.PlayerId, packet))
                return;
            Net.Apply(new NetAction(car, (NetActionKind)packet.Kind, packet.Sub, packet.Value), sender.PlayerId);
            packet.Player = sender.PlayerId;
            packet.Sequence = ++sequence;
            if (packet.Kind == (byte)NetActionKind.Bail)
                server.SendSerializablePacketToAll(packet, true, true, sender);
            else
                server.SendSerializablePacketToAll(packet, true, true);
        }

        // A state the host settled (the host's own client has done it already), kept until the host's snapshot has come.
        private void OnClientAction(ActionPacket packet)
        {
            if (MultiplayerAPI.Server != null)
                return;
            if (!joined)
                CheckJoinTimeout();
            if (joined)
                Settle(packet);
            else if (packet.Kind != (byte)NetActionKind.Bail)
            {
                var key = Key(packet);
                if (!early.TryGetValue(key, out var heard) || heard.Sequence < packet.Sequence)
                    early[key] = packet;
            }
        }

        // Mid-session host model settings update, for a guest only.
        private void OnSettings(SettingsPacket packet)
        {
            if (MultiplayerAPI.Server != null)
                return;
            ApplyHostSettings(packet.Settings, packet.Version, packet.Flavor, packet.Sequence);
            if (!joined)
                CheckJoinTimeout();
        }

        private void ApplyHostSettings(Net.ModelSettings settings, string version, string flavor, uint seq)
        {
            if (hasSettings && seq <= settingsSequence)
                return;
            hasSettings = true;
            settingsSequence = seq;
            Net.AdoptHostSettings(settings, version, flavor);
        }

        private static bool ClientConnected => MultiplayerAPI.Client == null || MultiplayerAPI.Client.IsConnected;

        internal void CheckJoinTimeout()
        {
            if (MultiplayerAPI.Server != null || joined || !waitingForJoin || !ClientConnected)
            {
                waitingSince = double.NaN;
                return;
            }
            if (!IsClientReady())
            {
                waitingSince = double.NaN;
                return;
            }
            var now = Clock();
            if (double.IsNaN(waitingSince))
            {
                waitingSince = now;
                return;
            }
            if (now - waitingSince >= JoinTimeoutSeconds)
                ExpireJoinWait();
        }

        private void ExpireJoinWait()
        {
            if (joined)
                return;
            joined = true;
            waitingForJoin = false;
            if (!joinTimedOut)
            {
                joinTimedOut = true;
                Notice.Warn("Realistic Brakes: no initial brake state from the host after 15 s; applying live updates without it", 8f);
            }
            FlushEarly();
        }

        private void FlushEarly()
        {
            if (early.Count == 0)
                return;
            var heard = new List<ActionPacket>(early.Values);
            early.Clear();
            heard.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
            foreach (var state in heard)
                Settle(state);
        }

        // Does a state the host settled, if it's the newest for its control and newer than the snapshot, which covers
        // everything before it (a review, 2026-10-27: a hold sent before the snapshot, heard after, outlived its let-go);
        // this game's player's own, confirmed. Its number counts even if its car isn't here, so an older state can't follow it.
        private void Settle(ActionPacket packet)
        {
            if (packet.Sequence <= snapshot && packet.Kind != (byte)NetActionKind.Bail)
                return;
            if (FreshFromHost(packet, packet.Sequence) && TryGetCar(packet.NetId, out var car))
                Net.Apply(new NetAction(car, (NetActionKind)packet.Kind, packet.Sub, packet.Value), packet.Player, packet.Player == LocalPlayer);
        }

        // The host's settings and its cars' states, for a guest only (the host's own client hears them too: a review, 2026-10-27);
        // then what was heard before it, in the host's order. A late snapshot arriving after timeout still applies host settings safely.
        private void OnJoin(JoinPacket packet)
        {
            if (MultiplayerAPI.Server != null || snapshotReceived)
                return;
            snapshotReceived = true;
            try
            {
                Join(packet);
            }
            finally
            {
                // Whatever went wrong, the guest goes on hearing the host.
                joined = true;
                waitingForJoin = false;
                FlushEarly();
                var client = MultiplayerAPI.Client;
                if (ShouldAckJoin(packet) && client != null && client.IsConnected)
                    client.SendSerializablePacketToServer(SettingsPacket.Ack());
            }
        }

        private void Join(JoinPacket packet)
        {
            if (packet.Sequence > snapshot)
                snapshot = packet.Sequence;
            ApplyHostSettings(packet.Settings, packet.Version, packet.Flavor, packet.Sequence);
            var held = new HashSet<(ushort, byte)>();
            var adjustHeld = new HashSet<(ushort, byte)>();
            foreach (var state in packet.States)
            {
                if (state.Kind == (byte)NetActionKind.BleedHold)
                    held.Add((state.NetId, state.Player));
                else if (state.Kind == (byte)NetActionKind.Adjust)
                    adjustHeld.Add((state.NetId, state.Player));
                // A state set since the snapshot was taken keeps its newer setting.
                if (FreshFromHost(state, packet.Sequence) && TryGetCar(state.NetId, out var car))
                    Net.Apply(new NetAction(car, (NetActionKind)state.Kind, state.Sub, state.Value), state.Player, state.Player == LocalPlayer);
            }
            // The snapshot names every holder of each car's bleed and Angleichen key: any other hold heard is over, unless heard since.
            foreach (var netId in packet.Cars)
            {
                if (!TryGetCar(netId, out var car))
                    continue;
                foreach (var holder in Net.HoldersOf(car))
                {
                    var release = new ActionPacket { NetId = netId, Kind = (byte)NetActionKind.BleedHold, Value = 0, Player = holder };
                    if (!held.Contains((netId, holder)) && FreshFromHost(release, packet.Sequence))
                        Net.Apply(new NetAction(car, NetActionKind.BleedHold, 0, 0), holder);
                }
                foreach (var holder in AdjustKey.HoldersOf(car))
                {
                    var release = new ActionPacket { NetId = netId, Kind = (byte)NetActionKind.Adjust, Value = 0, Player = holder };
                    if (!adjustHeld.Contains((netId, holder)) && FreshFromHost(release, packet.Sequence))
                        Net.Apply(new NetAction(car, NetActionKind.Adjust, 0, 0), holder);
                }
            }
        }

        // Whether a guest's state is newer than any seen from it for its car's control; a bail is an event, always new.
        private bool FreshFromGuest(byte sender, ActionPacket packet)
        {
            if (packet.Kind == (byte)NetActionKind.Bail)
                return true;
            var key = (sender, packet.NetId, packet.Kind, packet.Sub);
            if (fromGuests.TryGetValue(key, out var last) && last >= packet.Sequence)
                return false;
            fromGuests[key] = packet.Sequence;
            return true;
        }

        // Whether a state, as of the host's <paramref name="sequence"/>, is newer than any seen for its car's control
        // (a bleed handle's, for its holder); a bail is an event, always new. A state already seen at the same number is
        // the same state, done again harmlessly.
        private bool FreshFromHost(ActionPacket state, uint sequence)
        {
            if (state.Kind == (byte)NetActionKind.Bail)
                return true;
            var key = Key(state);
            if (fromHost.TryGetValue(key, out var last) && last > sequence)
                return false;
            fromHost[key] = sequence;
            return true;
        }

        // A control's key: a bleed's or Angleichen's hold is one per holder.
        private static (ushort, byte, byte, byte) Key(ActionPacket state) =>
            (state.NetId, state.Kind, state.Sub,
                state.Kind == (byte)NetActionKind.BleedHold || state.Kind == (byte)NetActionKind.Adjust ? state.Player : (byte)0);

        private static bool TryGetNetId(TrainCar car, out ushort netId)
        {
            netId = 0;
            return car != null && MultiplayerAPI.Instance != null && MultiplayerAPI.Instance.TryGetNetId(car, out netId);
        }

        internal static bool TryGetCar(ushort netId, out TrainCar car)
        {
            car = null;
            return MultiplayerAPI.Instance != null && MultiplayerAPI.Instance.TryGetObjectFromNetId(netId, out car) && car != null;
        }
    }

    /// <summary>One player's action on a car, numbered by its sender (a guest's own numbers to the host, the host's to its guests).</summary>
    internal sealed class ActionPacket : ISerializablePacket
    {
        public ushort NetId;
        public byte Kind;
        public byte Sub;
        public byte Value;
        public byte Player;
        public uint Sequence;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(NetId);
            writer.Write(Kind);
            writer.Write(Sub);
            writer.Write(Value);
            writer.Write(Player);
            writer.Write(Sequence);
        }

        public void Deserialize(BinaryReader reader)
        {
            NetId = reader.ReadUInt16();
            Kind = reader.ReadByte();
            Sub = reader.ReadByte();
            Value = reader.ReadByte();
            Player = reader.ReadByte();
            Sequence = reader.ReadUInt32();
        }
    }

    /// <summary>
    /// What a joining guest needs, in one packet so its order can't split it:
    /// the host's mod version and model settings, and every car's settled
    /// states (its cars come from the host's world, not a save), as of the
    /// host's sequence number when sent.
    /// </summary>
    internal sealed class JoinPacket : ISerializablePacket
    {
        private static readonly CarLeverKind[] Kinds = { CarLeverKind.BrakingMode, CarLeverKind.EmptyLoaded, CarLeverKind.IsolatingCock, CarLeverKind.Retainer };

        public string Version = "";
        public string Flavor = "";
        public Net.ModelSettings Settings;
        public uint Sequence;

        /// <summary>
        /// Each car's states (their sequence numbers unused): its levers'
        /// notches, an engine's handle's added notch (none too), and every
        /// holder of its bleed or Angleichen key.
        /// </summary>
        public readonly List<ActionPacket> States = new List<ActionPacket>();

        /// <summary>The cars whose states are all here: a bleed or Angleichen holder of theirs not named has let go.</summary>
        public readonly List<ushort> Cars = new List<ushort>();

        /// <summary>The host's states, as of its <paramref name="sequence"/>; its own hands as <paramref name="host"/>'s.</summary>
        public static JoinPacket Local(uint sequence, byte host)
        {
            var packet = new JoinPacket
            {
                Version = Main.Mod?.Info.Version ?? "",
                Flavor = Main.BuildFlavor,
                Settings = Net.ModelSettings.From(Main.Settings),
                Sequence = sequence,
            };
            foreach (var set in Trainset.allSets)
                foreach (var car in set.cars)
                {
                    if (car == null || car.brakeSystem == null || !MultiplayerAPI.Instance.TryGetNetId(car, out ushort netId))
                        continue;
                    packet.Cars.Add(netId);
                    if (BrakeSets.TryGet(car.brakeSystem, out var air))
                    {
                        foreach (var kind in Kinds)
                        {
                            var notch = CarLever.LeverNotch(air, kind);
                            if (notch >= 0)
                                packet.Add(netId, NetActionKind.Lever, (byte)kind, (byte)notch, host);
                        }
                        if (air.LocalReleaseHeld)
                            packet.Add(netId, NetActionKind.BleedHold, 0, 1, host);
                    }
                    var caboose = CabooseValve.NotchOf(car);
                    if (caboose != 0)
                        packet.Add(netId, NetActionKind.CabooseValve, 0, (byte)caboose, host);
                    if (TrainBrakeHandle.TryGetZone(car.brakeSystem, out var zone, out var hand) || car.IsLoco)
                        packet.Add(netId, NetActionKind.HandleZone, 0, (byte)zone, hand == TrainBrakeHandle.LocalHand ? host : hand);
                }
            foreach (var (car, player) in Net.Holds())
                if (MultiplayerAPI.Instance.TryGetNetId(car, out ushort netId))
                    packet.Add(netId, NetActionKind.BleedHold, 0, 1, player);
            foreach (var (car, player) in AdjustKey.Holds())
                if (MultiplayerAPI.Instance.TryGetNetId(car, out ushort netId))
                    packet.Add(netId, NetActionKind.Adjust, 0, 1, player == TrainBrakeHandle.LocalHand ? host : player);
            return packet;
        }

        private void Add(ushort netId, NetActionKind kind, byte sub, byte value, byte player) =>
            States.Add(new ActionPacket { NetId = netId, Kind = (byte)kind, Sub = sub, Value = value, Player = player });

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Version ?? "");
            writer.Write(Settings.Enabled);
            writer.Write((byte)Settings.Equipment);
            writer.Write(Settings.BrakeForceFromCar);
            writer.Write((byte)Settings.FreightMode);
            writer.Write(Settings.ManualEmptyLoaded);
            writer.Write(Settings.WidenTrainBrakeHandles);
            writer.Write(Sequence);
            writer.Write(States.Count);
            foreach (var state in States)
            {
                writer.Write(state.NetId);
                writer.Write(state.Kind);
                writer.Write(state.Sub);
                writer.Write(state.Value);
                writer.Write(state.Player);
            }
            writer.Write(Cars.Count);
            foreach (var netId in Cars)
                writer.Write(netId);
            // After the cars, so an older guest reads what it knows and stops.
            writer.Write((byte)Settings.LoadBraking);
            writer.Write(Settings.DirectAdmission);
            writer.Write(Settings.EveryEngineUsesSetting);
            writer.Write(Flavor ?? "");
            // After the flavor, so an older guest stops before it.
            writer.Write(Settings.ChargingPressure);
        }

        public void Deserialize(BinaryReader reader)
        {
            Version = reader.ReadString();
            Flavor = "";
            Settings = new Net.ModelSettings
            {
                Enabled = reader.ReadBoolean(),
                Equipment = (Equipment)reader.ReadByte(),
                BrakeForceFromCar = reader.ReadBoolean(),
                FreightMode = (BrakingMode)reader.ReadByte(),
                ManualEmptyLoaded = reader.ReadBoolean(),
                WidenTrainBrakeHandles = reader.ReadBoolean(),
                // An older host has no load braking setting: its American cars have automatic empty/load.
                LoadBraking = LoadBraking.EmptyLoad,
                DirectAdmission = true,
                // Nor engines' own standards: every engine used the setting.
                EveryEngineUsesSetting = true,
                // Nor Russian equipment's charging pressure.
                ChargingPressure = 5.0f,
            };
            Sequence = reader.ReadUInt32();
            States.Clear();
            var count = reader.ReadInt32();
            for (var i = 0; i < count; i++)
                States.Add(new ActionPacket
                {
                    NetId = reader.ReadUInt16(), Kind = reader.ReadByte(), Sub = reader.ReadByte(), Value = reader.ReadByte(), Player = reader.ReadByte(),
                });
            Cars.Clear();
            // An older build's snapshot ends here (a review, 2026-10-27: reading on past its end left the guest waiting).
            if (reader.BaseStream.CanSeek && reader.BaseStream.Position >= reader.BaseStream.Length)
                return;
            count = reader.ReadInt32();
            for (var i = 0; i < count; i++)
                Cars.Add(reader.ReadUInt16());
            if (!reader.BaseStream.CanSeek || reader.BaseStream.Position < reader.BaseStream.Length)
                Settings.LoadBraking = (LoadBraking)reader.ReadByte();
            if (!reader.BaseStream.CanSeek || reader.BaseStream.Position < reader.BaseStream.Length)
                Settings.DirectAdmission = reader.ReadBoolean();
            if (!reader.BaseStream.CanSeek || reader.BaseStream.Position < reader.BaseStream.Length)
                Settings.EveryEngineUsesSetting = reader.ReadBoolean();
            if (reader.BaseStream.CanSeek && reader.BaseStream.Position < reader.BaseStream.Length)
                Flavor = reader.ReadString();
            if (reader.BaseStream.CanSeek && reader.BaseStream.Position < reader.BaseStream.Length)
                Settings.ChargingPressure = reader.ReadSingle();
        }
    }

    /// <summary>The host's updated model settings, broadcast to connected guests when the host changes them mid-session.</summary>
    internal sealed class SettingsPacket : ISerializablePacket
    {
        public string Version = "";
        public string Flavor = "";
        public Net.ModelSettings Settings;
        public uint Sequence;

        public static SettingsPacket Local(uint sequence) => new SettingsPacket
        {
            Version = Main.Mod?.Info.Version ?? "",
            Flavor = Main.BuildFlavor,
            Settings = Net.ModelSettings.From(Main.Settings),
            Sequence = sequence,
        };

        public static SettingsPacket Ack() => new SettingsPacket
        {
            Version = Main.Mod?.Info.Version ?? "",
            Flavor = Main.BuildFlavor,
        };

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Version ?? "");
            writer.Write(Settings.Enabled);
            writer.Write((byte)Settings.Equipment);
            writer.Write(Settings.BrakeForceFromCar);
            writer.Write((byte)Settings.FreightMode);
            writer.Write(Settings.ManualEmptyLoaded);
            writer.Write(Settings.WidenTrainBrakeHandles);
            writer.Write((byte)Settings.LoadBraking);
            writer.Write(Settings.DirectAdmission);
            writer.Write(Settings.EveryEngineUsesSetting);
            writer.Write(Sequence);
            writer.Write(Flavor ?? "");
            writer.Write(Settings.ChargingPressure);
        }

        public void Deserialize(BinaryReader reader)
        {
            Version = reader.ReadString();
            Flavor = "";
            Settings = new Net.ModelSettings
            {
                Enabled = reader.ReadBoolean(),
                Equipment = (Equipment)reader.ReadByte(),
                BrakeForceFromCar = reader.ReadBoolean(),
                FreightMode = (BrakingMode)reader.ReadByte(),
                ManualEmptyLoaded = reader.ReadBoolean(),
                WidenTrainBrakeHandles = reader.ReadBoolean(),
                LoadBraking = (LoadBraking)reader.ReadByte(),
                DirectAdmission = reader.ReadBoolean(),
                EveryEngineUsesSetting = reader.ReadBoolean(),
                ChargingPressure = 5.0f,
            };
            Sequence = reader.ReadUInt32();
            if (reader.BaseStream.CanSeek && reader.BaseStream.Position < reader.BaseStream.Length)
                Flavor = reader.ReadString();
            if (reader.BaseStream.CanSeek && reader.BaseStream.Position < reader.BaseStream.Length)
                Settings.ChargingPressure = reader.ReadSingle();
        }
    }
}
