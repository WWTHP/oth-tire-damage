using System;
using System.Collections.Generic;
using Il2CppVehiclePhysics;
using UnityEngine;

namespace TireDamage
{
    /// <summary>
    /// Applies <see cref="CarTargets"/> to one car's VPP physics: wheel radius (Update), friction
    /// multiplier per fixed step (depends on the ground and on cornering), and per-wheel ground drag
    /// (<c>wheelState.groundResistance</c>, scaled from VPP's own step callbacks so it works per car).
    /// Used for the local car and, on the host, for other players' cars. Every value is a factor on
    /// what the game set, re-read each time: if the game changes it, that becomes the new base.
    /// </summary>
    internal sealed class CarFx
    {
        public sealed class WheelFx
        {
            public float BaseRadius = -1f;  // the game's radius (-1 = not read yet)
            public float SetRadius = -1f;   // the radius we last set (-1 = untouched)
            public float Grip = 1f;         // our grip factor
            public float Base = 1f;         // the game's friction multiplier (tireForceMult)
            public float Written = -1f;     // the multiplier we last wrote (-1 = never)
            public float Read = 1f;
            public float Share = 0.5f;      // smoothed sideways share of the slip
            public bool Loose;              // on mud/sand/snow/ice
            public string Ground = "?";
            public float GroundGrip, GroundDrag;
            public float Drag = 1f;         // our ground drag factor
            public Vector2 ResBase;         // the game's groundResistance
            public Vector2 ResWritten = new(float.NaN, float.NaN);
            public Vector2 ResRead;
        }

        public readonly VehicleBase Vehicle;
        public readonly string Name;
        public readonly List<WheelFx> Wheels = new();

        /// <summary>Scale ground drag per wheel (false while the shared ground-type drag covers it).</summary>
        public bool WheelDrag;

        /// <summary>Times the game rewrote groundResistance since the last log, per callback.</summary>
        public int RewritesPre, RewritesIntegration, BaseChanges;

        private CarTargets _targets;
        private Il2CppSystem.Action _pre, _integration;
        private bool _hooked, _hookFailed;

        public CarFx(VehicleBase vehicle, string name)
        {
            Vehicle = vehicle;
            Name = name;
            var states = vehicle.wheelState;
            int n = states != null ? states.Length : 0;
            var local = new List<Vector3>();
            float minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                var w = new WheelFx();
                var pos = Vector3.zero;
                try
                {
                    w.BaseRadius = vehicle.GetWheelRadius(i);
                    var col = states[i]?.wheelCol;
                    if (col != null) pos = vehicle.transform.InverseTransformPoint(col.transform.position);
                }
                catch (Exception ex) { Mod.Log.Warning($"{name} wheel {i}: {ex.Message}"); }
                local.Add(pos);
                minZ = Math.Min(minZ, pos.z);
                maxZ = Math.Max(maxZ, pos.z);
                Wheels.Add(w);
            }
            foreach (var pos in local) _labels.Add(FieldRepair.WheelLabel(pos.x, pos.z, minZ, maxZ));
        }

        private readonly List<string> _labels = new();

        /// <summary>FL, FR, RL… from the wheel's position.</summary>
        public string Label(int i) => i >= 0 && i < _labels.Count ? _labels[i] : "W" + i;

        public bool Valid => Vehicle != null && !Vehicle.WasCollected;

        public CarTargets Targets => _targets;

        public void SetTargets(CarTargets t) => _targets = t;

        // ------------------------------------------------------------------ radius (Update)

        public void UpdateRadius(Func<int, string> label)
        {
            if (_targets == null || !Valid) return;
            for (int i = 0; i < Wheels.Count; i++)
            {
                var w = Wheels[i];
                float mul = _targets.WheelRadius(i);
                try
                {
                    float current = Vehicle.GetWheelRadius(i);
                    // Someone else (e.g. a wheel upgrade) changed the radius: that is the new normal.
                    bool ours = w.SetRadius > 0f && Math.Abs(current - w.SetRadius) < 1e-4f;
                    if (w.BaseRadius <= 0f) w.BaseRadius = current;
                    if (!ours && Math.Abs(current - w.BaseRadius) > 1e-4f && current > 0.01f)
                    {
                        Mod.Log.Msg($"{Name} {label(i)} wheel radius changed by the game: {w.BaseRadius:0.000} → {current:0.000}.");
                        w.BaseRadius = current;
                        w.SetRadius = -1f;
                    }
                    float want = w.BaseRadius * mul;
                    if (mul < 1f || w.SetRadius > 0f)
                    {
                        if (Math.Abs(current - want) > 1e-4f) Vehicle.SetWheelRadius(i, want);
                        w.SetRadius = mul < 1f ? want : -1f;
                    }
                }
                catch (Exception ex) { Mod.Log.Warning($"Tire effect {Name} {label(i)}: {ex.Message}"); }
            }
        }

        // ------------------------------------------------------------------ grip (FixedUpdate)

        /// <summary>Sets each wheel's friction multiplier. <paramref name="readGround"/>: also note the ground details.</summary>
        public void FixedStep(bool readGround)
        {
            if (_targets == null || !Valid) return;
            var states = Vehicle.wheelState;
            if (states == null) return;
            EnsureHooked();
            int n = Math.Min(states.Length, Wheels.Count);
            for (int i = 0; i < n; i++)
            {
                var ws = states[i];
                if (ws != null) Grip(i, ws, readGround);
            }
        }

        private void Grip(int i, VehicleBase.WheelState ws, bool readGround)
        {
            var w = Wheels[i];
            float cur = ws.tireForceMult;
            w.Read = cur;
            if (w.Written < 0f || Math.Abs(cur - w.Written) > 1e-4f)
            {
                if (w.Written >= 0f) BaseChanges++;
                if (float.IsFinite(cur) && cur > 0f) w.Base = cur;
            }
            float raw = ws.grounded ? Pressure.LateralShare(ws.slipAngle, ws.peakSlipAngle, ws.slipRatio, ws.peakSlipRatio) : 0.5f;
            w.Share += (raw - w.Share) * Math.Min(1f, Time.fixedDeltaTime * 8f);
            if (ws.grounded) ReadGround(w, ws, readGround);
            float target = _targets.WheelGrip(i, w.Loose, w.Share);
            w.Grip = target;
            w.Drag = _targets.WheelDrag(i, w.Loose);
            if (target == 1f && w.Written < 0f) return; // never touched: leave the game's value alone
            float want = w.Base * target;
            if (Math.Abs(want - cur) < 0.003f) return;
            try
            {
                Vehicle.SetWheelTireFrictionMultiplier(i, want);
                w.Written = want;
            }
            catch (Exception ex) { Mod.Log.Warning($"Tire grip {Name} wheel {i}: {ex.Message}"); }
        }

        private static void ReadGround(WheelFx w, VehicleBase.WheelState ws, bool details)
        {
            var cfg = ws.groundMaterial?.Config;
            if (cfg == null)
            {
                w.Loose = false;
                w.Ground = "none";
                return;
            }
            var label = cfg.Label;
            w.Loose = Pressure.IsLoose((int)label);
            if (!details) return;
            w.Ground = label.ToString();
            w.GroundGrip = cfg.Grip;
            w.GroundDrag = cfg.Drag;
        }

        // ------------------------------------------------------------------ ground drag (VPP callbacks)

        /// <summary>
        /// Adds our ground-drag step to VPP's onPreDynamicsStep and onBeforeIntegrationStep. Which of
        /// the two runs after the game sets groundResistance is not known, so both apply the same
        /// factor (idempotent) and count how often they find the game's value instead of ours.
        /// </summary>
        private void EnsureHooked()
        {
            if (_hooked || _hookFailed) return;
            try
            {
                _pre = (Il2CppSystem.Action)new Action(OnPre);
                _integration = (Il2CppSystem.Action)new Action(OnIntegration);
                Vehicle.onPreDynamicsStep = Combine(Vehicle.onPreDynamicsStep, _pre);
                Vehicle.onBeforeIntegrationStep = Combine(Vehicle.onBeforeIntegrationStep, _integration);
                _hooked = true;
                Mod.Log.Msg($"{Name}: per-wheel ground drag hooked into the physics step.");
            }
            catch (Exception ex)
            {
                _hookFailed = true;
                Mod.Log.Warning($"{Name}: per-wheel ground drag unavailable: {ex.Message}");
            }
        }

        private static Il2CppSystem.Action Combine(Il2CppSystem.Action a, Il2CppSystem.Action b) =>
            a == null ? b : Il2CppSystem.Delegate.Combine(a, b).Cast<Il2CppSystem.Action>();

        private void Unhook()
        {
            if (!_hooked) return;
            _hooked = false;
            try
            {
                if (!Valid) return;
                var p = Il2CppSystem.Delegate.Remove(Vehicle.onPreDynamicsStep, _pre);
                Vehicle.onPreDynamicsStep = p?.Cast<Il2CppSystem.Action>();
                var q = Il2CppSystem.Delegate.Remove(Vehicle.onBeforeIntegrationStep, _integration);
                Vehicle.onBeforeIntegrationStep = q?.Cast<Il2CppSystem.Action>();
            }
            catch { /* car is being destroyed */ }
        }

        private int _dragErrors;

        private void OnPre() => DragStep(ref RewritesPre);

        private void OnIntegration() => DragStep(ref RewritesIntegration);

        // Runs inside VPP's step: must never throw.
        private void DragStep(ref int rewrites)
        {
            try
            {
                if (!Valid) return;
                var states = Vehicle.wheelState;
                if (states == null) return;
                int n = Math.Min(states.Length, Wheels.Count);
                for (int i = 0; i < n; i++)
                {
                    var ws = states[i];
                    if (ws == null) continue;
                    var w = Wheels[i];
                    var cur = ws.groundResistance;
                    w.ResRead = cur;
                    bool ours = !float.IsNaN(w.ResWritten.x) && Near(cur, w.ResWritten);
                    if (!ours)
                    {
                        if (!float.IsNaN(w.ResWritten.x)) rewrites++;
                        w.ResBase = cur;
                        w.ResWritten = new Vector2(float.NaN, float.NaN);
                    }
                    float mul = WheelDrag && Settings.Enabled.Value ? w.Drag : 1f;
                    if (mul >= 1f && float.IsNaN(w.ResWritten.x)) continue; // untouched
                    var want = w.ResBase * mul;
                    if (Near(cur, want)) { w.ResWritten = want; continue; }
                    ws.groundResistance = want;
                    w.ResWritten = want;
                }
            }
            catch (Exception ex)
            {
                if (_dragErrors++ < 3) Mod.Log.Warning($"{Name} ground drag step: {ex.Message}");
            }
        }

        private static bool Near(Vector2 a, Vector2 b) => Math.Abs(a.x - b.x) < 1e-5f && Math.Abs(a.y - b.y) < 1e-5f;

        // ------------------------------------------------------------------ restore

        /// <summary>Gives every wheel the game's radius, grip and drag back and leaves VPP's callbacks.</summary>
        public void Restore()
        {
            Unhook();
            bool valid = Valid;
            var states = valid ? Vehicle.wheelState : null;
            for (int i = 0; i < Wheels.Count; i++)
            {
                var w = Wheels[i];
                if (valid)
                {
                    try
                    {
                        if (w.SetRadius > 0f) Vehicle.SetWheelRadius(i, w.BaseRadius);
                        if (w.Written >= 0f) Vehicle.SetWheelTireFrictionMultiplier(i, w.Base);
                        if (!float.IsNaN(w.ResWritten.x) && states != null && i < states.Length && states[i] != null)
                            states[i].groundResistance = w.ResBase;
                    }
                    catch { /* car is being destroyed */ }
                }
                w.SetRadius = -1f;
                w.Grip = 1f;
                w.Drag = 1f;
                w.Written = -1f;
                w.ResWritten = new Vector2(float.NaN, float.NaN);
            }
        }

        /// <summary>One diagnostics line: ground, grip and drag per wheel.</summary>
        public string Describe(Func<int, string> label)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Wheels.Count; i++)
            {
                var w = Wheels[i];
                sb.Append($" {label(i)} {w.Ground}{(w.Loose ? "*" : "")} (ground grip {w.GroundGrip:0.00} drag {w.GroundDrag:0.00})" +
                          $" side {w.Share:0.00} x{w.Grip:0.00} base {w.Base:0.00} read {w.Read:0.00}" +
                          $" res {w.ResBase.x:0.000}/{w.ResBase.y:0.000}→{w.ResRead.x:0.000}/{w.ResRead.y:0.000} drag x{(WheelDrag ? w.Drag : 1f):0.00};");
            }
            if (BaseChanges > 0) sb.Append($" game rewrote the multiplier {BaseChanges}x");
            sb.Append($" groundResistance rewritten by the game: {RewritesPre}x before dynamics, {RewritesIntegration}x before integration");
            BaseChanges = RewritesPre = RewritesIntegration = 0;
            return sb.ToString();
        }
    }
}
