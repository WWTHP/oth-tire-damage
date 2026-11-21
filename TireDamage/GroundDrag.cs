using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppVehiclePhysics;
using UnityEngine;

namespace TireDamage
{
    /// <summary>
    /// Low pressure floats on loose ground: scales the drag of the mud/sand/snow/ice ground types.
    /// Drag lives on the game's <see cref="GroundMaterialConfig"/> assets, shared by every car this
    /// machine simulates, so it is only changed while this machine runs the local car's physics and
    /// no other player's car (<see cref="Pressure.MayChangeGroundDrag"/>); otherwise, and when the
    /// pressure is full or the mod is off, the original values are restored and <see cref="CarFx"/>
    /// scales drag per wheel instead. Which machine runs each car's physics is logged whenever it changes.
    /// </summary>
    internal static class GroundDrag
    {
        private sealed class Original
        {
            public GroundMaterialConfig Config;
            public float Drag;     // the game's value
            public float Written;  // what we set (to notice the game changing it)
        }

        private static readonly Dictionary<IntPtr, Original> Changed = new();
        private static float _applied = 1f, _nextScan;
        private static bool _allowed;
        private static int _othersHere;
        private static string _lastSim = "";

        public static float Applied => _applied;
        public static bool Allowed => _allowed;

        public static void Update()
        {
            if (Time.unscaledTime >= _nextScan) Scan();
            float want = 1f;
            if (Settings.Enabled.Value && Car.Valid && _allowed)
                want = Pressure.DragMul(Settings.Psi(), Settings.PressureRules());
            if (Math.Abs(want - _applied) < 0.001f) return;
            if (want >= 1f) Restore();
            else Apply(want);
        }

        /// <summary>Which machine runs each car's physics (kinematic body = a copy from the network).</summary>
        private static void Scan()
        {
            _nextScan = Time.unscaledTime + 2f;
            bool own = false;
            int others = 0;
            var parts = new List<string>();
            try
            {
                foreach (var vi in UnityEngine.Object.FindObjectsOfType<VehicleInput>())
                {
                    if (vi == null) continue;
                    var obj = vi.Object;
                    if (obj == null) continue;
                    var body = vi.GetComponentInParent<Rigidbody>() ?? vi.transform.root.GetComponentInChildren<Rigidbody>();
                    bool here = body != null && !body.isKinematic;
                    bool mine = obj.HasInputAuthority;
                    bool player = mine || obj.InputAuthority.IsRealPlayer;
                    if (mine && Car.Valid && body == Car.Body) own = here;
                    else if (player && !mine && here) others++;
                    string who = mine ? "mine" : player ? $"player {obj.InputAuthority.Raw}" : "no driver";
                    parts.Add($"{vi.transform.root.name} ({who}, physics {(here ? "here" : "remote")}, state authority {(obj.HasStateAuthority ? "here" : "remote")})");
                }
            }
            catch (Exception ex) { parts.Add("scan failed: " + ex.Message); }
            _othersHere = others;
            _allowed = !Settings.WheelDragAlways.Value && Pressure.MayChangeGroundDrag(own, others);
            var line = string.Join("; ", parts);
            if (line != _lastSim)
            {
                _lastSim = line;
                Mod.Log.Msg($"Car physics: {line}. Ground drag change {(_allowed ? "allowed" : "off")}.");
            }
        }

        private static void Apply(float mul)
        {
            var configs = Configs();
            int n = 0;
            foreach (var cfg in configs)
            {
                if (cfg == null || !Pressure.IsLoose((int)cfg.Label)) continue;
                var key = cfg.Pointer;
                if (!Changed.TryGetValue(key, out var o))
                {
                    o = new Original { Config = cfg, Drag = cfg.Drag, Written = float.NaN };
                    Changed[key] = o;
                }
                else if (Math.Abs(cfg.Drag - o.Written) > 1e-5f) o.Drag = cfg.Drag; // the game changed it
                o.Written = o.Drag * mul;
                cfg.Drag = o.Written;
                n++;
            }
            if (Math.Abs(mul - _applied) > 0.05f || _applied >= 1f)
                Mod.Log.Msg($"Loose-ground drag x{mul:0.00} on {n} ground type(s).");
            _applied = mul;
        }

        /// <summary>Puts the game's drag values back.</summary>
        public static void Restore()
        {
            foreach (var o in Changed.Values)
            {
                try
                {
                    if (o.Config != null && !o.Config.WasCollected) o.Config.Drag = o.Drag;
                }
                catch { /* unloading */ }
            }
            if (Changed.Count > 0) Mod.Log.Msg($"Loose-ground drag restored ({(_allowed ? "full pressure" : "not allowed now")}).");
            Changed.Clear();
            _applied = 1f;
        }

        private static List<GroundMaterialConfig> Configs()
        {
            var list = new List<GroundMaterialConfig>();
            try
            {
                var fromCar = Car.Vehicle?.m_groundMaterialIndexToConfig;
                if (fromCar != null)
                    foreach (var c in fromCar) list.Add(c);
            }
            catch (Exception ex) { Mod.Log.Warning($"Ground types from the car: {ex.Message}"); }
            if (list.Count > 0) return list;
            foreach (var c in Resources.FindObjectsOfTypeAll<GroundMaterialConfig>()) list.Add(c);
            return list;
        }

        /// <summary>For the hub tab.</summary>
        public static string Describe() =>
            _allowed ? $"Loose-ground drag x{_applied:0.00} on the ground types (only this car's physics runs here)"
            : Settings.WheelDragAlways.Value ? "Loose-ground drag: per wheel (test setting on)"
            : _othersHere > 0 ? "Loose-ground drag: per wheel (this machine also runs other players' cars)"
            : "Loose-ground drag: per wheel (this car's physics runs elsewhere: the host applies your tires if it has the mod)";
    }
}
