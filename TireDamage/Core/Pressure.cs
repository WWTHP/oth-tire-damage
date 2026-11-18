using System;

namespace TireDamage
{
    /// <summary>Tunables of the tire pressure model (from the settings).</summary>
    internal sealed class PressureRules
    {
        /// <summary>Full pressure: the game's normal tire (no change at all).</summary>
        public float NormalPsi = 32f;

        /// <summary>Lowest pressure the player can set.</summary>
        public float MinPsi = 8f;

        /// <summary>Below this the tires wear while driving and suffer more from hits (pinch flats).</summary>
        public float SafePsi = 16f;

        /// <summary>Grip multiplier at <see cref="MinPsi"/> (1 at normal, linear in between).</summary>
        public float GripAtMin = 1.3f;

        /// <summary>Wheel radius multiplier at <see cref="MinPsi"/> (the tire squats: lower top speed).</summary>
        public float RadiusAtMin = 0.92f;

        /// <summary>Health lost per km at <see cref="MinPsi"/> (0 at <see cref="SafePsi"/>, linear between).</summary>
        public float WearPerKmAtMin = 0.5f;

        /// <summary>Hit damage multiplier at <see cref="MinPsi"/> (1 at <see cref="SafePsi"/> and above).</summary>
        public float HitFactorAtMin = 1.5f;
    }

    /// <summary>
    /// Tire pressure, one value for all tires. Full pressure is the game's own tire: every effect is
    /// exactly neutral there. Lower pressure gives more grip and a smaller rolling radius (slower);
    /// below the safe pressure, driving wears the tires and hits hurt them more.
    /// Rules are sanitised so odd settings (min above normal, safe outside the range) stay usable.
    /// </summary>
    internal static class Pressure
    {
        public const float Step = 1f;

        /// <summary>Normal and min with min &lt; normal (at least 1 PSI apart), safe inside [min, normal].</summary>
        public static void Bounds(PressureRules r, out float min, out float normal, out float safe)
        {
            normal = Finite(r?.NormalPsi ?? 32f, 32f);
            if (normal < 2f) normal = 2f;
            min = Math.Clamp(Finite(r?.MinPsi ?? 8f, 8f), 1f, normal - 1f);
            safe = Math.Clamp(Finite(r?.SafePsi ?? 16f, 16f), min, normal);
        }

        /// <summary>A PSI value inside the allowed range (NaN → normal).</summary>
        public static float Clamp(float psi, PressureRules r)
        {
            Bounds(r, out var min, out var normal, out _);
            return float.IsFinite(psi) ? Math.Clamp(psi, min, normal) : normal;
        }

        /// <summary>One step up or down, snapped to whole PSI and clamped.</summary>
        public static float Change(float psi, int steps, PressureRules r) =>
            Clamp((float)Math.Round(Clamp(psi, r) + steps * Step), r);

        /// <summary>How far below normal, 0 (normal) → 1 (min).</summary>
        public static float Softness(float psi, PressureRules r)
        {
            Bounds(r, out var min, out var normal, out _);
            return (normal - Clamp(psi, r)) / (normal - min);
        }

        public static float Grip(float psi, PressureRules r) =>
            Lerp(1f, Finite(r.GripAtMin, 1f), Softness(psi, r));

        public static float RadiusMul(float psi, PressureRules r) =>
            Lerp(1f, Math.Clamp(Finite(r.RadiusAtMin, 1f), 0.5f, 1f), Softness(psi, r));

        /// <summary>0 at or above safe, 1 at min.</summary>
        public static float Danger(float psi, PressureRules r)
        {
            Bounds(r, out var min, out _, out var safe);
            float p = Clamp(psi, r);
            if (p >= safe || safe <= min) return 0f;
            return (safe - p) / (safe - min);
        }

        public static float WearPerKm(float psi, PressureRules r) =>
            Math.Max(0f, Finite(r.WearPerKmAtMin, 0f)) * Danger(psi, r);

        public static float HitFactor(float psi, PressureRules r) =>
            Lerp(1f, Math.Max(1f, Finite(r.HitFactorAtMin, 1f)), Danger(psi, r));

        /// <summary>Short effect summary for the UI, e.g. "Grip +15%  ·  Speed -4%  ·  Wearing".</summary>
        public static string Describe(float psi, PressureRules r)
        {
            if (Softness(psi, r) <= 0f) return "Full pressure: normal grip and speed";
            int grip = (int)Math.Round((Grip(psi, r) - 1f) * 100f);
            int speed = (int)Math.Round((RadiusMul(psi, r) - 1f) * 100f);
            var text = $"Grip {grip:+0;-0;0}%  ·  Speed {speed:+0;-0;0}%";
            return Danger(psi, r) > 0f ? text + "  ·  TOO LOW: tires wearing" : text;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);

        private static float Finite(float v, float fallback) => float.IsFinite(v) ? v : fallback;
    }
}
