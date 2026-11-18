using System.Collections.Generic;
using Il2Cpp;
using Il2CppVehiclePhysics;
using UnityEngine;

namespace TireDamage
{
    /// <summary>
    /// The car the local player drives (input authority): its physics (VPP <see cref="VehicleBase"/>),
    /// damage system, and the mounted cosmetics that count as spare wheels or a tool kit. Only the
    /// local car is ever modified.
    /// </summary>
    internal static class Car
    {
        public static Rigidbody Body { get; private set; }
        public static VehicleBase Vehicle { get; private set; }
        public static CarDamageSystem Damage { get; private set; }
        public static bool HasToolKit { get; private set; }

        /// <summary>Spare wheel cosmetics in a stable order (name, then hierarchy path).</summary>
        public static readonly List<GameObject> Spares = new();

        /// <summary>Changes every time a different car becomes the local car.</summary>
        public static int Generation { get; private set; }

        public static float Speed => Valid ? Body.linearVelocity.magnitude : 0f;

        public static bool Valid =>
            _input != null && !_input.WasCollected && Body != null && !Body.WasCollected &&
            Vehicle != null && !Vehicle.WasCollected;

        private static VehicleInput _input;
        private static float _nextScan, _nextParts;
        private static string _lastParts = "";

        public static void Update()
        {
            if (Valid && Time.unscaledTime < _nextScan)
            {
                if (Time.unscaledTime >= _nextParts) ScanParts();
                return;
            }
            _nextScan = Time.unscaledTime + 1f;
            var old = _input;
            Find();
            if (_input != null && _input != old)
            {
                Generation++;
                Mod.Log.Msg($"Local car found: {_input.transform.root.name} ({Vehicle.wheelCount} wheels, " +
                            $"damage system: {(Damage != null ? "yes" : "no")}).");
                _lastParts = "";
                ScanParts();
            }
        }

        private static void Find()
        {
            _input = null; Body = null; Vehicle = null; Damage = null;
            foreach (var vi in Object.FindObjectsOfType<VehicleInput>())
            {
                if (vi == null) continue;
                try
                {
                    var obj = vi.Object;
                    if (obj == null || !obj.HasInputAuthority) continue;
                }
                catch { continue; } // not spawned on the network yet
                var root = vi.transform.root;
                var vehicle = root.GetComponentInChildren<VPVehicleController>();
                var body = vi.GetComponentInParent<Rigidbody>();
                if (body == null) body = root.GetComponentInChildren<Rigidbody>();
                if (vehicle == null || body == null) continue;
                _input = vi;
                Body = body;
                Vehicle = vehicle;
                Damage = root.GetComponentInChildren<CarDamageSystem>();
                return;
            }
        }

        /// <summary>Finds spare-wheel and tool-kit cosmetics. Logs the part list when it changes.</summary>
        private static void ScanParts()
        {
            _nextParts = Time.unscaledTime + 2f;
            if (!Valid) return;
            var names = new List<string>();
            var spares = new List<KeyValuePair<string, GameObject>>();
            var spareWords = FieldRepair.ParseKeywords(Settings.SpareKeywords.Value);
            var exclude = FieldRepair.ParseKeywords(Settings.SpareExclude.Value);
            var toolWords = FieldRepair.ParseKeywords(Settings.ToolKeywords.Value);
            bool tool = false;
            try
            {
                foreach (var part in _input.transform.root.GetComponentsInChildren<VehicleUpgradeObject>(false))
                {
                    if (part == null || !part.gameObject.activeInHierarchy) continue;
                    var so = part.VehiclePartReference;
                    var name = so != null ? so.name : part.gameObject.name;
                    names.Add(name);
                    var one = new[] { name };
                    if (FieldRepair.CountParts(one, spareWords, exclude) == 1) spares.Add(new KeyValuePair<string, GameObject>(name, part.gameObject));
                    if (FieldRepair.CountParts(one, toolWords, null) == 1) tool = true;
                }
            }
            catch (System.Exception ex)
            {
                Mod.Log.Warning("Part scan failed: " + ex.Message);
                return;
            }
            names.Sort();
            spares.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            int max = System.Math.Max(0, Settings.MaxSpares.Value);
            if (spares.Count > max) spares.RemoveRange(max, spares.Count - max);

            var joined = string.Join(", ", names);
            bool changed = joined != _lastParts || tool != HasToolKit || spares.Count != Spares.Count;
            Spares.Clear();
            foreach (var s in spares) Spares.Add(s.Value);
            HasToolKit = tool;
            if (!changed) return;
            _lastParts = joined;
            Mod.Log.Msg($"Mounted parts: [{joined}] → {Spares.Count} spare wheel(s), tool kit: {(tool ? "yes" : "no")}.");
        }
    }
}
