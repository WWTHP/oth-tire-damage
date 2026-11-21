using System;
using System.Collections.Generic;
using System.IO;
using Il2Cpp;
using UnityEngine;

namespace TireDamage
{
    /// <summary>
    /// Multiplayer. The host runs every car's physics (logged as <c>Car physics: …</c>), so a
    /// client's own grip, radius and drag changes don't reach its car. Each client sends its
    /// <see cref="CarTargets"/> (pressure and flats, worked out from its own settings) to the host,
    /// and the host applies them to that player's car with a <see cref="CarFx"/>. Hit damage, wear and
    /// repairs stay on the driver's machine. Without the mod on the host, nothing is applied.
    /// </summary>
    internal static class Remote
    {
        private const float SendEvery = 0.25f, Heartbeat = 2f, Timeout = 8f, ResolveEvery = 1f;

        private sealed class Player
        {
            public int Id;
            public CarTargets Targets;
            public float LastHeard;
            public CarFx Fx;
            public IntPtr Vehicle;
            public TireWire.ApplyStatus Sent = (TireWire.ApplyStatus)255;
            public float NextLog;
        }

        private static readonly Dictionary<int, Player> Players = new();
        private static IntPtr _runner;
        private static bool _helloSent;
        private static float _helloAgainAt = -1f, _nextSend, _lastSend, _nextResolve;
        private static CarTargets _sent;
        private static TireWire.ApplyStatus? _hostStatus;
        private static bool _hostHasMod;

        public static void Init() => Net.OnMessage = OnMessage;

        // ------------------------------------------------------------------ frame updates

        public static void Update()
        {
            var run = Net.Runner();
            var ptr = run?.Pointer ?? IntPtr.Zero;
            if (ptr != _runner) NewSession(ptr);
            if (run == null || !Net.Ready || run.IsSinglePlayer) return;

            if (!_helloSent)
            {
                _helloSent = true;
                SendHello(false);
                _helloAgainAt = Time.unscaledTime + 5f;
            }
            else if (_helloAgainAt > 0f && Time.unscaledTime > _helloAgainAt)
            {
                _helloAgainAt = -1f;
                if (!Net.IsAuthority(run) && !_hostHasMod) SendHello(false);
            }

            if (Net.IsAuthority(run)) HostUpdate(run);
            else ClientUpdate();
        }

        /// <summary>Host: applies other players' grip and drag to their cars.</summary>
        public static void FixedUpdate()
        {
            if (Players.Count == 0 || !Settings.Enabled.Value) return;
            foreach (var p in Players.Values)
            {
                if (p.Fx == null || !p.Fx.Valid) continue;
                p.Fx.WheelDrag = true;
                p.Fx.FixedStep(Settings.LogGrip.Value);
                if (Settings.LogGrip.Value && Time.time >= p.NextLog && p.Targets.Psi < Settings.PressureRules().NormalPsi - 0.01f)
                {
                    p.NextLog = Time.time + 3f;
                    Mod.Log.Msg($"Player {p.Id} grip @{p.Targets.Psi:0} PSI:{p.Fx.Describe(p.Fx.Label)}");
                }
            }
        }

        private static void NewSession(IntPtr ptr)
        {
            _runner = ptr;
            RestoreAll();
            Players.Clear();
            Net.ClearPeers();
            _helloSent = false;
            _helloAgainAt = -1f;
            _sent = null;
            _hostStatus = null;
            _hostHasMod = false;
        }

        // ------------------------------------------------------------------ client

        private static void ClientUpdate()
        {
            if (!_hostHasMod || !Settings.Multiplayer.Value) return;
            if (Time.unscaledTime < _nextSend) return;
            _nextSend = Time.unscaledTime + SendEvery;
            // Mod off or no car: send neutral targets so the host puts the car back to normal.
            var t = Settings.Enabled.Value && Tires.Targets != null ? Tires.Targets : new CarTargets();
            if (t.SameAs(_sent) && Time.unscaledTime - _lastSend < Heartbeat) return;
            var w = Net.Begin(Net.Msg.CarState, out var ms);
            t.Write(w);
            Net.SendTo(-1, ms);
            if (_sent == null || !t.SameAs(_sent))
                Mod.Log.Msg($"Sent your tires to the host: {Summary(t)}.");
            _sent = t;
            _lastSend = Time.unscaledTime;
        }

        // ------------------------------------------------------------------ host

        private static void HostUpdate(Il2CppFusion.NetworkRunner run)
        {
            List<int> gone = null;
            foreach (var p in Players.Values)
                if (Time.unscaledTime - p.LastHeard > Timeout) (gone ??= new List<int>()).Add(p.Id);
            if (gone != null)
                foreach (var id in gone)
                {
                    Mod.Log.Msg($"Player {id}: no tire updates for {Timeout:0} s; their car is back to normal.");
                    Players[id].Fx?.Restore();
                    Players.Remove(id);
                }

            if (Time.unscaledTime >= _nextResolve)
            {
                _nextResolve = Time.unscaledTime + ResolveEvery;
                foreach (var p in Players.Values) Resolve(p);
            }
            if (!Settings.Enabled.Value) return;
            foreach (var p in Players.Values)
                if (p.Fx != null && p.Fx.Valid) p.Fx.UpdateRadius(p.Fx.Label);
        }

        /// <summary>Finds the player's car, if its physics runs here; tells the player what happens.</summary>
        private static void Resolve(Player p)
        {
            VehicleInput found = null;
            Il2CppVehiclePhysics.VehicleBase vehicle = null;
            if (Settings.Enabled.Value && Settings.Multiplayer.Value)
            {
                try
                {
                    foreach (var vi in UnityEngine.Object.FindObjectsOfType<VehicleInput>())
                    {
                        if (vi == null) continue;
                        var obj = vi.Object;
                        if (obj == null || obj.HasInputAuthority || obj.InputAuthority.Raw != p.Id) continue;
                        var body = vi.GetComponentInParent<Rigidbody>() ?? vi.transform.root.GetComponentInChildren<Rigidbody>();
                        var v = vi.transform.root.GetComponentInChildren<Il2CppVehiclePhysics.VPVehicleController>();
                        if (body == null || body.isKinematic || v == null) continue;
                        found = vi;
                        vehicle = v;
                        break;
                    }
                }
                catch (Exception ex) { Mod.Log.Warning($"Player {p.Id}'s car: {ex.Message}"); }
            }

            var ptr = vehicle != null ? vehicle.Pointer : IntPtr.Zero;
            if (ptr != p.Vehicle)
            {
                p.Fx?.Restore();
                p.Fx = null;
                p.Vehicle = ptr;
                if (vehicle != null)
                {
                    p.Fx = new CarFx(vehicle, $"Player {p.Id}'s car");
                    p.Fx.SetTargets(p.Targets);
                    Mod.Log.Msg($"Player {p.Id}'s car {found.transform.root.name} ({p.Fx.Wheels.Count} wheels) runs here: " +
                                $"applying their tires ({Summary(p.Targets)}).");
                }
                else
                    Mod.Log.Msg($"Player {p.Id}: no car of theirs runs its physics here; their tires are not applied.");
            }

            var status = !Settings.Enabled.Value ? TireWire.ApplyStatus.HostOff
                : p.Fx != null ? TireWire.ApplyStatus.Applying : TireWire.ApplyStatus.NoCar;
            if (status == p.Sent) return;
            p.Sent = status;
            var w = Net.Begin(Net.Msg.Applied, out var ms);
            TireWire.WriteApplied(w, status, p.Fx?.Wheels.Count ?? 0);
            Net.SendTo(p.Id, ms);
        }

        private static void RestoreAll()
        {
            foreach (var p in Players.Values) p.Fx?.Restore();
        }

        // ------------------------------------------------------------------ messages

        private static void SendHello(bool reply, int to = -2)
        {
            var w = Net.Begin(Net.Msg.Hello, out var ms);
            TireWire.WriteHello(w, reply);
            if (to == -2) Net.Broadcast(ms);
            else Net.SendTo(to, ms);
        }

        private static void OnMessage(Net.Msg type, int sender, BinaryReader r)
        {
            var run = Net.Runner();
            bool host = Net.IsAuthority(run);
            switch (type)
            {
                case Net.Msg.Hello:
                    if (!TireWire.TryReadHello(r, out bool reply)) { Bad(type, sender); return; }
                    if (host && !reply) SendHello(true, sender);
                    if (!host) _hostHasMod = true; // a client only hears from the host
                    Mod.Log.Msg($"Player {sender} has the tire mod{(reply ? " (host)" : "")}.");
                    break;
                case Net.Msg.CarState:
                    if (!host) return; // only the host runs other players' cars
                    if (!CarTargets.TryRead(r, out var t)) { Bad(type, sender); return; }
                    if (!Players.TryGetValue(sender, out var p))
                    {
                        p = new Player { Id = sender };
                        Players[sender] = p;
                        _nextResolve = 0f;
                    }
                    if (p.Targets == null || !t.SameAs(p.Targets))
                        Mod.Log.Msg($"Player {sender}'s tires: {Summary(t)}.");
                    p.Targets = t;
                    p.LastHeard = Time.unscaledTime;
                    p.Fx?.SetTargets(t);
                    break;
                case Net.Msg.Applied:
                    if (host) return;
                    if (!TireWire.TryReadApplied(r, out var status, out int wheels)) { Bad(type, sender); return; }
                    _hostHasMod = true;
                    if (_hostStatus == status) return;
                    _hostStatus = status;
                    Mod.Log.Msg(status switch
                    {
                        TireWire.ApplyStatus.Applying => $"The host applies your tire pressure and flats to your car ({wheels} wheels).",
                        TireWire.ApplyStatus.HostOff => "The host has the tire mod switched off: your pressure and flats don't change how your car drives.",
                        _ => "The host hasn't found your car (or doesn't run its physics): your pressure and flats aren't applied yet.",
                    });
                    break;
                default:
                    Bad(type, sender);
                    break;
            }
        }

        private static void Bad(Net.Msg type, int sender) => Mod.Log.Warning($"Ignored a bad tire message ({type}) from player {sender}.");

        private static string Summary(CarTargets t)
        {
            int flats = 0;
            for (int i = 0; i < t.Wheels; i++)
                if (t.FlatGrip[i] > 0f) flats++;
            return $"{t.Psi:0} PSI, traction x{t.LooseGrip:0.00}, corners x{t.CornerGrip:0.00}, drag x{t.DragMul:0.00}, {flats} flat(s)";
        }

        /// <summary>For the hub tab.</summary>
        public static string Describe()
        {
            var run = Net.Runner();
            if (!Settings.Multiplayer.Value) return "Multiplayer: off";
            if (run == null || run.IsSinglePlayer) return $"Multiplayer: {Net.Describe()}";
            if (Net.IsAuthority(run))
            {
                int applied = 0;
                foreach (var p in Players.Values)
                    if (p.Fx != null) applied++;
                return $"Multiplayer (host): {Net.Describe()}; applying {applied} other player(s)' tires";
            }
            string s = !_hostHasMod ? "the host doesn't have the tire mod: your pressure and flats don't change how your car drives"
                : _hostStatus == TireWire.ApplyStatus.Applying ? "the host applies your tires"
                : _hostStatus == TireWire.ApplyStatus.HostOff ? "the host has the mod switched off"
                : "waiting for the host to find your car";
            return $"Multiplayer (client): {s}";
        }
    }
}
