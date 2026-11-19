using System;
using System.Collections.Generic;
using UnityEngine;

namespace TireDamage
{
    /// <summary>
    /// Tire state of the local car: detects hits (suspension contact speed per wheel, hard body
    /// impacts near a wheel), wears popped tires while driving, applies the flat and pressure effects
    /// (wheel radius in Update; grip per fixed step, since the pressure grip depends on the ground and
    /// on whether the tire is cornering) through VPP, and runs the hold-to-repair field repair.
    /// </summary>
    internal static class Tires
    {
        private const float HitCooldown = 0.3f, MaxRepairSpeed = 1f;

        private sealed class Wheel
        {
            public Tire Tire = new();
            public string Label;
            public Vector3 Local;          // wheel position in the vehicle's local space
            public float BaseRadius;       // the game's radius for this wheel
            public float SetRadius = -1f;  // the radius we last set (-1 = untouched)
            public float Grip = 1f;        // friction multiplier we last set (1 = untouched)
            public float Share = 0.5f;     // smoothed sideways share of the tire's slip
            public bool Loose;             // on mud, sand, snow or ice (last fixed step; kept while the pressure is below full)
            public float NextHit;
        }

        private static readonly List<Wheel> Wheels = new();
        private static int _generation = -1;
        private static int _sparesUsed;
        private static readonly List<Renderer> HiddenRenderers = new();
        private static float _repairProgress;
        private static int _menuTire = -1, _activeTire = -1;
        private static float _menuHeldUntil;
        private static RepairKind _activeKind;
        private static bool _wasEnabled = true;

        public static int Count => Wheels.Count;
        public static int SparesUsed => _sparesUsed;
        public static int SparesLeft => Math.Max(0, Car.Spares.Count - _sparesUsed);

        /// <summary>Progress 0-1 of a repair in progress (key held), or -1.</summary>
        public static float RepairProgress { get; private set; } = -1f;

        public static string RepairText { get; private set; }

        /// <summary>The tire being repaired right now (-1 if none).</summary>
        public static int RepairingTire => RepairProgress >= 0f ? _activeTire : -1;

        /// <summary>The menu's hold button for tire <paramref name="i"/> is held this frame.</summary>
        public static void HoldFromMenu(int i)
        {
            _menuTire = i;
            _menuHeldUntil = Time.unscaledTime + 0.15f;
        }

        /// <summary>What holding tire <paramref name="i"/>'s button would do.</summary>
        public static RepairPlan PlanFor(int i) => FieldRepair.PlanFor(TireList(), i, Car.HasToolKit, SparesLeft);

        public static string Label(int i) => Wheels[i].Label;
        public static Tire TireAt(int i) => Wheels[i].Tire;

        /// <summary>Grip multiplier on tire <paramref name="i"/> now, and whether it is on mud/sand/snow/ice.</summary>
        public static float GripAt(int i, out bool loose)
        {
            loose = Wheels[i].Loose;
            return Wheels[i].Grip;
        }

        public static bool AnyDamage()
        {
            foreach (var w in Wheels)
                if (w.Tire.State != TireState.Ok || w.Tire.Health < 0.999f) return true;
            return _sparesUsed > 0;
        }

        // ------------------------------------------------------------------ frame updates

        public static void Update()
        {
            bool enabled = Settings.Enabled.Value;
            if (!enabled)
            {
                if (_wasEnabled) RestoreAll();
                _wasEnabled = false;
                return;
            }
            _wasEnabled = true;
            if (!Car.Valid) { RepairProgress = -1f; return; }
            if (Car.Generation != _generation) Attach();

            float metres = Car.Speed * Time.deltaTime;
            var rules = Settings.Rules();
            float psi = Settings.Psi();
            var pr = Settings.PressureRules();
            for (int i = 0; i < Wheels.Count; i++)
            {
                // Low pressure only harms the tire on hard ground (Loose is kept up to date whenever
                // the pressure is below full, see UpdateGrip).
                float lowWear = metres / 1000f * Pressure.WearPerKm(psi, pr, Wheels[i].Loose);
                if (Wheels[i].Tire.Drive(metres, rules) is TireState s) Announce(i, s, "driving on a flat");
                if (Wheels[i].Tire.Abrade(lowWear) is TireState w) Announce(i, w, "pressure too low");
            }

            ApplyEffects();
            UpdateRepair();
        }

        public static void FixedUpdate()
        {
            if (!Settings.Enabled.Value || !Car.Valid || Car.Generation != _generation) return;
            var v = Car.Vehicle;
            var rules = Settings.Rules();
            var states = v.wheelState;
            if (states == null) return;
            int n = Math.Min(states.Length, Wheels.Count);
            var pr = Settings.PressureRules();
            float psi = Pressure.Clamp(Settings.TirePsi.Value, pr);
            bool soft = Pressure.Softness(psi, pr) > 0f;
            for (int i = 0; i < n; i++)
            {
                var ws = states[i];
                if (ws == null) continue;
                float speed = ws.suspensionState.contactSpeed;
                Damage(i, Tire.ImpactDamage(speed, rules), speed, "suspension", rules);
                UpdateGrip(v, i, ws, soft, psi, pr);
            }

            if (Settings.BodyHits.Value && v.isHardImpact)
            {
                var pos = v.localImpactPosition;
                float speed = v.localImpactVelocity.magnitude;
                int nearest = -1;
                float best = Settings.BodyHitRadius.Value;
                for (int i = 0; i < Wheels.Count; i++)
                {
                    float d = Vector3.Distance(pos, Wheels[i].Local);
                    if (d <= best) { best = d; nearest = i; }
                }
                if (nearest >= 0)
                    Damage(nearest, Tire.ImpactDamage(speed, rules) * Settings.BodyHitScale.Value, speed, "body", rules);
            }
        }

        /// <summary>
        /// Friction multiplier of wheel <paramref name="i"/>: flat tires a fixed value; good tires at
        /// low pressure blend extra traction (mud/sand/snow/ice only) with less cornering grip by how
        /// much of the tire's slip is sideways. Written only when it changes noticeably.
        /// </summary>
        private static void UpdateGrip(Il2CppVehiclePhysics.VehicleBase v, int i, Il2CppVehiclePhysics.VehicleBase.WheelState ws,
                                       bool soft, float psi, PressureRules pr)
        {
            var w = Wheels[i];
            float target = 1f;
            if (w.Tire.State == TireState.Popped) target = Settings.PoppedGrip.Value;
            else if (w.Tire.State == TireState.Ruptured) target = Settings.RupturedGrip.Value;
            else if (soft)
            {
                float raw = ws.grounded ? Pressure.LateralShare(ws.slipAngle, ws.peakSlipAngle, ws.slipRatio, ws.peakSlipRatio) : 0.5f;
                w.Share += (raw - w.Share) * Math.Min(1f, Time.fixedDeltaTime * 8f);
                w.Loose = ws.grounded ? IsLoose(ws) : w.Loose;
                target = Pressure.WheelGrip(psi, pr, w.Loose, w.Share);
            }
            if (Math.Abs(target - w.Grip) < 0.003f && (target != 1f || w.Grip == 1f)) return;
            try
            {
                v.SetWheelTireFrictionMultiplier(i, target);
                w.Grip = target;
            }
            catch (Exception ex) { Mod.Log.Warning($"Tire grip {w.Label}: {ex.Message}"); }
        }

        private static bool IsLoose(Il2CppVehiclePhysics.VehicleBase.WheelState ws)
        {
            var cfg = ws.groundMaterial?.Config;
            return cfg != null && Pressure.IsLoose((int)cfg.Label);
        }

        private static void Damage(int i, float damage, float speed, string source, TireRules rules)
        {
            if (damage <= 0f) return;
            var w = Wheels[i];
            if (Time.time < w.NextHit) return;
            if (w.Tire.State == TireState.Ok) damage *= Pressure.HitFactor(Settings.Psi(), Settings.PressureRules(), w.Loose);
            w.NextHit = Time.time + HitCooldown;
            var change = w.Tire.Hit(damage, rules);
            if (Settings.LogHits.Value)
                Mod.Log.Msg($"Hit {w.Label} ({source}) {Math.Abs(speed):0.0} m/s → -{damage:0.00} = " +
                            $"{FieldRepair.Describe(w.Tire.State, w.Tire.Health)}" +
                            (w.Tire.State == TireState.Popped ? $" (carcass {w.Tire.Carcass:0.00})" : ""));
            if (change is TireState s) Announce(i, s, source + " hit");
        }

        private static void Announce(int i, TireState s, string why)
        {
            string what = s == TireState.Ruptured ? "ruptured" : "popped";
            Mod.Log.Msg($"{Wheels[i].Label} tire {what} ({why}).");
            Toast.Show($"{LongName(Wheels[i].Label)} tire {what}!");
        }

        // ------------------------------------------------------------------ car changes

        /// <summary>Fresh tires for a newly found car; reads wheel positions and radii.</summary>
        private static void Attach()
        {
            RestoreAll();
            Wheels.Clear();
            UnhideSpares();
            _sparesUsed = 0;
            _generation = Car.Generation;
            var v = Car.Vehicle;
            var states = v.wheelState;
            int n = states != null ? states.Length : 0;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                var w = new Wheel();
                try
                {
                    var col = states[i]?.wheelCol;
                    if (col != null) w.Local = v.transform.InverseTransformPoint(col.transform.position);
                    w.BaseRadius = v.GetWheelRadius(i);
                }
                catch (Exception ex) { Mod.Log.Warning($"Wheel {i}: {ex.Message}"); }
                minZ = Math.Min(minZ, w.Local.z);
                maxZ = Math.Max(maxZ, w.Local.z);
                Wheels.Add(w);
            }
            var labels = new List<string>();
            foreach (var w in Wheels)
            {
                w.Label = FieldRepair.WheelLabel(w.Local.x, w.Local.z, minZ, maxZ);
                labels.Add($"{w.Label} r={w.BaseRadius:0.00}");
            }
            Mod.Log.Msg($"Tires: {string.Join(", ", labels)}.");
        }

        // ------------------------------------------------------------------ effects

        private static void ApplyEffects()
        {
            var v = Car.Vehicle;
            var pr = Settings.PressureRules();
            float psi = Settings.Psi();
            float psiRadius = Pressure.RadiusMul(psi, pr);
            for (int i = 0; i < Wheels.Count; i++)
            {
                var w = Wheels[i];
                // A good tire runs at the set pressure (full pressure = exactly 1, untouched).
                float radiusMul = psiRadius;
                if (w.Tire.State == TireState.Popped) radiusMul = Settings.PoppedRadius.Value;
                else if (w.Tire.State == TireState.Ruptured) radiusMul = Settings.RupturedRadius.Value;
                try
                {
                    float current = v.GetWheelRadius(i);
                    // Someone else (e.g. a wheel upgrade) changed the radius: that is the new normal.
                    bool ours = w.SetRadius > 0f && Math.Abs(current - w.SetRadius) < 1e-4f;
                    if (!ours && Math.Abs(current - w.BaseRadius) > 1e-4f && current > 0.01f)
                    {
                        Mod.Log.Msg($"{w.Label} wheel radius changed by the game: {w.BaseRadius:0.000} → {current:0.000}.");
                        w.BaseRadius = current;
                        w.SetRadius = -1f;
                    }
                    float want = w.BaseRadius * radiusMul;
                    if (radiusMul < 1f || w.SetRadius > 0f)
                    {
                        if (Math.Abs(current - want) > 1e-4f) v.SetWheelRadius(i, want);
                        w.SetRadius = radiusMul < 1f ? want : -1f;
                    }
                }
                catch (Exception ex) { Mod.Log.Warning($"Tire effect {w.Label}: {ex.Message}"); }
            }
        }

        /// <summary>Gives every wheel its normal radius and grip back (mod disabled or car gone).</summary>
        private static void RestoreAll()
        {
            var v = Car.Vehicle;
            bool valid = v != null && !v.WasCollected;
            for (int i = 0; i < Wheels.Count; i++)
            {
                var w = Wheels[i];
                if (valid)
                {
                    try
                    {
                        if (w.SetRadius > 0f) v.SetWheelRadius(i, w.BaseRadius);
                        if (w.Grip != 1f) v.SetWheelTireFrictionMultiplier(i, 1f);
                    }
                    catch { /* car is being destroyed */ }
                }
                w.SetRadius = -1f;
                w.Grip = 1f;
            }
            RepairProgress = -1f;
        }

        // ------------------------------------------------------------------ repairs

        private static void UpdateRepair()
        {
            RepairPlan active;
            if (_menuTire >= 0 && Time.unscaledTime <= _menuHeldUntil) active = PlanFor(_menuTire);
            else
            {
                _menuTire = -1;
                StopRepair(null);
                return;
            }
            if (active.Kind == RepairKind.None) { StopRepair(active.Reason); RepairProgress = 0f; return; }
            if (Car.Speed > MaxRepairSpeed) { StopRepair("Stop the car to repair."); RepairProgress = 0f; return; }
            if (active.Tire != _activeTire || active.Kind != _activeKind) _repairProgress = 0f; // switched target
            _activeTire = active.Tire;
            _activeKind = active.Kind;

            float need = active.Kind == RepairKind.Patch ? Settings.PatchSeconds.Value : Settings.SpareSeconds.Value;
            _repairProgress += Time.deltaTime;
            RepairProgress = Mathf.Clamp01(_repairProgress / Mathf.Max(0.1f, need));
            var name = LongName(Wheels[active.Tire].Label);
            RepairText = active.Kind == RepairKind.Patch ? $"Patching {name} tire…" : $"Fitting a spare to {name}…";
            if (RepairProgress < 1f) return;

            DoRepair(active);
            StopRepair(null);
        }

        private static void StopRepair(string text)
        {
            _repairProgress = 0f;
            RepairProgress = -1f;
            _activeTire = -1;
            _activeKind = RepairKind.None;
            RepairText = text;
        }

        private static void DoRepair(RepairPlan plan)
        {
            var w = Wheels[plan.Tire];
            if (plan.Kind == RepairKind.Patch)
            {
                w.Tire.Patch(Settings.Rules());
                Toast.Show($"{LongName(w.Label)} tire patched ({w.Tire.Health:P0}).");
            }
            else
            {
                w.Tire.Replace();
                if (Settings.HideUsedSpares.Value && _sparesUsed < Car.Spares.Count) HideSpare(Car.Spares[_sparesUsed]);
                _sparesUsed++;
                Toast.Show($"Spare fitted to {LongName(w.Label)} ({SparesLeft} spare(s) left).");
            }
            Mod.Log.Msg($"Field repair: {w.Label} {plan.Kind}; spares used {_sparesUsed}/{Car.Spares.Count}.");
        }

        /// <summary>Every tire new and every spare back (house, mechanic, camp).</summary>
        public static void Service(string where)
        {
            bool any = AnyDamage();
            foreach (var w in Wheels) w.Tire.Replace();
            _sparesUsed = 0;
            UnhideSpares();
            if (!any) return;
            Mod.Log.Msg($"Service ({where}): all tires replaced, spares restocked.");
            Toast.Show("All tires replaced.");
        }

        /// <summary>Test helper from the menu: pops (or ruptures) a random good tire.</summary>
        public static void TestBreak(TireState state)
        {
            var ok = new List<int>();
            for (int i = 0; i < Wheels.Count; i++)
                if (Wheels[i].Tire.State == TireState.Ok) ok.Add(i);
            if (ok.Count == 0) return;
            int pick = ok[UnityEngine.Random.Range(0, ok.Count)];
            Wheels[pick].Tire.Force(state);
            Announce(pick, state, "test button");
        }

        private static List<Tire> TireList()
        {
            var list = new List<Tire>(Wheels.Count);
            foreach (var w in Wheels) list.Add(w.Tire);
            return list;
        }

        private static void HideSpare(GameObject go)
        {
            if (go == null || go.WasCollected) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(false))
            {
                if (r == null || !r.enabled) continue;
                r.enabled = false;
                HiddenRenderers.Add(r); // only ours come back on
            }
        }

        private static void UnhideSpares()
        {
            foreach (var r in HiddenRenderers)
                if (r != null && !r.WasCollected) r.enabled = true;
            HiddenRenderers.Clear();
        }

        public static string LongName(string label) => label switch
        {
            "FL" => "front-left",
            "FR" => "front-right",
            "RL" => "rear-left",
            "RR" => "rear-right",
            "ML" => "middle-left",
            "MR" => "middle-right",
            _ => label,
        };
    }
}
