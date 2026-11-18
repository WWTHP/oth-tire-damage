using System;

namespace TireDamage
{
    // Core/ = pure C#: no UnityEngine, Il2Cpp*, MelonLoader or HarmonyLib here.

    internal enum TireState
    {
        Ok = 0,
        Popped = 1,
        Ruptured = 2,
    }

    /// <summary>Tunables of the damage model (from the settings).</summary>
    internal sealed class TireRules
    {
        /// <summary>Impacts below this speed (m/s) do nothing.</summary>
        public float HitThreshold = 4f;

        /// <summary>Damage = Scale × (speed − threshold)².</summary>
        public float DamageScale = 0.04f;

        /// <summary>A single hit at least this big pops straight into a rupture.</summary>
        public float RuptureHit = 0.9f;

        /// <summary>A popped tire's carcass loses this much per km driven (it ruptures at 0).</summary>
        public float FlatWearPerKm = 0.5f;

        /// <summary>A hit on a popped tire costs the carcass this many times its damage.</summary>
        public float FlatHitFactor = 2f;

        /// <summary>Health of a tire patched with the tool kit.</summary>
        public float PatchHealth = 0.5f;
    }

    /// <summary>
    /// One tire. Health 1 → 0 by hits; at 0 it pops (or ruptures on a big enough hit). A popped tire
    /// keeps a carcass (1 → 0) that driving and hits wear down until it ruptures.
    /// </summary>
    internal sealed class Tire
    {
        public float Health { get; private set; } = 1f;
        public float Carcass { get; private set; } = 1f;
        public TireState State { get; private set; } = TireState.Ok;

        /// <summary>Impact damage for a hit at <paramref name="speed"/> m/s (0 under the threshold or for bad input).</summary>
        public static float ImpactDamage(float speed, TireRules r)
        {
            if (!float.IsFinite(speed) || r == null) return 0f;
            float excess = Math.Abs(speed) - r.HitThreshold;
            if (excess <= 0f) return 0f;
            return r.DamageScale * excess * excess;
        }

        /// <summary>Applies a hit. Returns the state change it caused (null if none).</summary>
        public TireState? Hit(float damage, TireRules r)
        {
            if (!float.IsFinite(damage) || damage <= 0f || State == TireState.Ruptured) return null;
            if (State == TireState.Popped) return Wear(damage * r.FlatHitFactor);
            Health -= damage;
            if (Health > 0f) return null;
            Health = 0f;
            State = damage >= r.RuptureHit ? TireState.Ruptured : TireState.Popped;
            if (State == TireState.Ruptured) Carcass = 0f;
            return State;
        }

        /// <summary>Driving <paramref name="metres"/> on a popped tire. Returns Ruptured when it gives out.</summary>
        public TireState? Drive(float metres, TireRules r)
        {
            if (State != TireState.Popped || !float.IsFinite(metres) || metres <= 0f) return null;
            return Wear(metres / 1000f * r.FlatWearPerKm);
        }

        private TireState? Wear(float amount)
        {
            Carcass -= amount;
            if (Carcass > 0f) return null;
            Carcass = 0f;
            State = TireState.Ruptured;
            return State;
        }

        /// <summary>Tool kit patch: only a popped tire, back to <see cref="TireRules.PatchHealth"/>.</summary>
        public bool Patch(TireRules r)
        {
            if (State != TireState.Popped) return false;
            State = TireState.Ok;
            Health = Math.Clamp(r.PatchHealth, 0.05f, 1f);
            Carcass = 1f;
            return true;
        }

        /// <summary>A new tire (spare wheel or service).</summary>
        public void Replace()
        {
            State = TireState.Ok;
            Health = 1f;
            Carcass = 1f;
        }

        /// <summary>For tests and the menu's test buttons.</summary>
        public void Force(TireState state, float health = 0f)
        {
            State = state;
            Health = state == TireState.Ok ? Math.Clamp(health, 0f, 1f) : 0f;
            Carcass = state == TireState.Ruptured ? 0f : 1f;
        }
    }
}
