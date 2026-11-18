using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace TireDamage
{
    /// <summary>
    /// Where tires are replaced for free: houses (Cabin), the mechanic (Merchant) and camp.
    /// Two ways in, so one missing doesn't break it:
    /// 1. The game's own car repairs: postfixes on parameterless CarDamageSystem/CampManager methods
    ///    and on PerformLocalPlayerHealingOperations(reason) (an enum, not a struct: rule 1 holds).
    ///    Installed once, late (first local car), under CrashGuard stage "hooks".
    /// 2. Proximity: stopped within ServiceRadius of a house or mechanic.
    /// </summary>
    internal static class Service
    {
        private static readonly Dictionary<long, Vector3> Stations = new();
        private static float _nextScan;
        private static bool _hooked, _inside;
        private static int _lastCount = -1;
        private static string _pending;

        public static void Update(HarmonyLib.Harmony harmony)
        {
            if (!Car.Valid) return;
            if (!_hooked) Hook(harmony);
            if (_pending != null)
            {
                Tires.Service(_pending);
                _pending = null;
            }
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 5f;
                try { Scan(); }
                catch (Exception ex) { Mod.Log.Warning("Station scan failed: " + ex.Message); }
            }
            Proximity();
        }

        private static void Scan()
        {
            foreach (var poi in UnityEngine.Object.FindObjectsOfType<PointOfInterest>())
            {
                if (poi == null) continue;
                string type;
                try { type = poi.Info != null ? poi.Info.Type.ToString() : ""; }
                catch { type = ""; }
                bool cabin = type == "Cabin" || poi.TryCast<Cabin>() != null;
                if (!cabin && type != "Merchant") continue;
                var p = poi.transform.position;
                long key = ((long)Mathf.RoundToInt(p.x) << 32) ^ (uint)Mathf.RoundToInt(p.z);
                Stations[key] = p;
            }
            if (Stations.Count == _lastCount) return;
            _lastCount = Stations.Count;
            Mod.Log.Msg($"Service stations known (houses + mechanic): {Stations.Count}.");
        }

        private static void Proximity()
        {
            float radius = Settings.ServiceRadius.Value;
            if (radius <= 0f || Stations.Count == 0) return;
            var me = Car.Body.transform.position;
            float best = float.PositiveInfinity;
            foreach (var s in Stations.Values) best = Math.Min(best, Vector3.Distance(me, s));
            if (_inside && best > radius * 1.5f) _inside = false; // left: next visit services again
            if (_inside || best > radius || Car.Speed > 1f) return;
            _inside = true;
            Tires.Service($"stopped {best:0} m from a house/mechanic");
        }

        /// <summary>Called from the game hooks (any thread-safe context: only sets a flag).</summary>
        private static void Request(string where) => _pending ??= where;

        // ------------------------------------------------------------------ game hooks

        private static void Hook(HarmonyLib.Harmony harmony)
        {
            _hooked = true;
            if (!CrashGuard.Enter("hooks")) { Mod.Log.Warning("Repair hooks disabled after a crash; proximity only."); return; }
            try
            {
                Patch(harmony, typeof(CarDamageSystem), "HealOnCabinArrival", nameof(AfterCabin));
                Patch(harmony, typeof(CarDamageSystem), "HealOnPointOfInterestArrival", nameof(AfterPoi));
                Patch(harmony, typeof(CarDamageSystem), "PerformLocalPlayerHealingOperations", nameof(AfterHealing));
                Patch(harmony, typeof(CampManager), "RepairPartyCars", nameof(AfterCamp));
                Patch(harmony, typeof(CampManager), "RPC_EndCamping", nameof(AfterCamp));
            }
            finally { CrashGuard.Exit("hooks"); }
        }

        private static void Patch(HarmonyLib.Harmony harmony, Type type, string method, string postfix)
        {
            try
            {
                var target = AccessTools.Method(type, method);
                if (target == null) { Mod.Log.Warning($"Hook: {type.Name}.{method} not found."); return; }
                harmony.Patch(target, postfix: new HarmonyMethod(typeof(Service), postfix));
                Mod.Log.Msg($"Hooked {type.Name}.{method}.");
            }
            catch (Exception ex) { Mod.Log.Warning($"Hook {type.Name}.{method} failed: {ex.Message}"); }
        }

        private static bool IsLocal(CarDamageSystem cds)
        {
            try { return cds != null && Car.Damage != null && cds.Pointer == Car.Damage.Pointer; }
            catch { return false; }
        }

        private static void AfterCabin(CarDamageSystem __instance)
        {
            try { if (IsLocal(__instance)) Request("house (game repair)"); }
            catch { /* never let an exception reach the game */ }
        }

        private static void AfterPoi(CarDamageSystem __instance)
        {
            try { if (IsLocal(__instance)) Request("point of interest (game repair)"); }
            catch { }
        }

        private static void AfterHealing(CarDamageSystem __instance, CarDamageSystem.HealthChangingReason reason)
        {
            try
            {
                if (!IsLocal(__instance)) return;
                Mod.Log.Msg($"Game healed the car: {reason}.");
                if (reason == CarDamageSystem.HealthChangingReason.HealedByPOI ||
                    reason == CarDamageSystem.HealthChangingReason.HealedByCamping)
                    Request($"game repair ({reason})");
            }
            catch { }
        }

        private static void AfterCamp()
        {
            try { Request("camp"); }
            catch { }
        }
    }
}
