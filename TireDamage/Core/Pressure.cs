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

        /// <summary>Below this the tires wear while driving on hard ground and suffer more from hits there (pinch flats).</summary>
        public float SafePsi = 16f;

        /// <summary>Traction multiplier on loose ground (mud, sand, snow, ice) at <see cref="MinPsi"/> (1 at normal, linear in between).</summary>
        public float GripAtMin = 1.3f;

        /// <summary>Cornering grip multiplier at <see cref="MinPsi"/>, on any ground (the sidewalls flex).</summary>
        public float CornerGripAtMin = 0.85f;

        /// <summary>Ground drag multiplier on loose ground at <see cref="MinPsi"/> (the tire floats instead of digging in).</summary>
        public float DragAtMin = 0.5f;

        /// <summary>Wheel radius multiplier at <see cref="MinPsi"/> (the tire squats: lower top speed).</summary>
        public float RadiusAtMin = 0.92f;

        /// <summary>Health lost per km at <see cref="MinPsi"/> (0 at <see cref="SafePsi"/>, linear between).</summary>
        public float WearPerKmAtMin = 0.5f;

        /// <summary>Hit damage multiplier at <see cref="MinPsi"/> (1 at <see cref="SafePsi"/> and above).</summary>
        public float HitFactorAtMin = 1.5f;
    }

    /// <summary>
    /// Tire pressure, one value for all tires. Full pressure is the game's own tire: every effect is
    /// exactly neutral there. Lower pressure gives more traction on loose ground (mud, sand, snow, ice),
    /// less cornering grip everywhere, and a smaller rolling radius (slower);
    /// below the safe pressure, driving on hard ground wears the tires and hits there hurt them more
    /// (loose ground, where low pressure helps, does them no harm).
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

        /// <summary>
        /// Ground labels (the game's <c>MaterialLabel</c> flags) where low pressure adds traction and
        /// does no damage: Mud 0x8, DeepMud 0x10, Snow 0x800, DeepSnow 0x1000, Ice 0x2000, Sand 0x4000,
        /// DeepSand 0x8000, QuickSand 0x10000, ShallowSnow 0x40000.
        /// </summary>
        public const int LooseLabels = 0x8 | 0x10 | 0x800 | 0x1000 | 0x2000 | 0x4000 | 0x8000 | 0x10000 | 0x40000;

        public static bool IsLoose(int label) => (label & LooseLabels) != 0;

        /// <summary>Straight-line traction on loose ground (≥ 1).</summary>
        public static float Grip(float psi, PressureRules r) =>
            Lerp(1f, Math.Max(1f, Finite(r.GripAtMin, 1f)), Softness(psi, r));

        /// <summary>Cornering grip (≤ 1).</summary>
        public static float CornerGrip(float psi, PressureRules r) =>
            Lerp(1f, Math.Clamp(Finite(r.CornerGripAtMin, 1f), 0.3f, 1f), Softness(psi, r));

        /// <summary>
        /// How much of a tire's slip is sideways (cornering), 0 (all along the tire: traction,
        /// braking) → 1 (all sideways). Each slip is measured against its peak so the two compare;
        /// <paramref name="idle"/> when the tire hardly slips at all or the peaks are unknown.
        /// </summary>
        public static float LateralShare(float slipAngle, float peakAngle, float slipRatio, float peakRatio, float idle = 0.5f)
        {
            if (!(peakAngle > 0f) || !(peakRatio > 0f) || !float.IsFinite(slipAngle) || !float.IsFinite(slipRatio)) return idle;
            float lat = Math.Abs(slipAngle) / peakAngle, lon = Math.Abs(slipRatio) / peakRatio;
            if (lat + lon < 0.05f) return idle;
            return lat / (lat + lon);
        }

        /// <summary>
        /// The friction multiplier for a good tire: traction (loose ground only) blended with
        /// cornering grip by the tire's <paramref name="lateralShare"/>. Exactly 1 at full pressure.
        /// </summary>
        public static float WheelGrip(float psi, PressureRules r, bool loose, float lateralShare)
        {
            float traction = loose ? Grip(psi, r) : 1f;
            float t = float.IsFinite(lateralShare) ? lateralShare : 0.5f;
            return Lerp(traction, CornerGrip(psi, r), t);
        }

        public static float RadiusMul(float psi, PressureRules r) =>
            Lerp(1f, Math.Clamp(Finite(r.RadiusAtMin, 1f), 0.5f, 1f), Softness(psi, r));

        /// <summary>Drag of loose ground (≤ 1): lower pressure floats on mud, sand and snow.</summary>
        public static float DragMul(float psi, PressureRules r) =>
            Lerp(1f, Math.Clamp(Finite(r.DragAtMin, 1f), 0f, 1f), Softness(psi, r));

        /// <summary>
        /// Ground drag is a setting of the ground type, shared by every car this machine simulates.
        /// Change it only when this machine runs the local car's physics and no other player's car.
        /// </summary>
        public static bool MayChangeGroundDrag(bool ownCarSimulatedHere, int otherPlayersCarsSimulatedHere) =>
            ownCarSimulatedHere && otherPlayersCarsSimulatedHere == 0;

        /// <summary>0 at or above safe, 1 at min.</summary>
        public static float Danger(float psi, PressureRules r)
        {
            Bounds(r, out var min, out _, out var safe);
            float p = Clamp(psi, r);
            if (p >= safe || safe <= min) return 0f;
            return (safe - p) / (safe - min);
        }

        /// <summary>Health lost per km driven; none on <paramref name="loose"/> ground.</summary>
        public static float WearPerKm(float psi, PressureRules r, bool loose = false) =>
            loose ? 0f : Math.Max(0f, Finite(r.WearPerKmAtMin, 0f)) * Danger(psi, r);

        /// <summary>Hit damage multiplier; 1 on <paramref name="loose"/> ground.</summary>
        public static float HitFactor(float psi, PressureRules r, bool loose = false) =>
            loose ? 1f : Lerp(1f, Math.Max(1f, Finite(r.HitFactorAtMin, 1f)), Danger(psi, r));

        /// <summary>Short effect summary for the UI, e.g. "Loose ground +15%  ·  Corners -8%  ·  Speed -4%".</summary>
        public static string Describe(float psi, PressureRules r)
        {
            if (Softness(psi, r) <= 0f) return "Full pressure: normal grip and speed";
            int grip = Percent(Grip(psi, r)), corner = Percent(CornerGrip(psi, r)), speed = Percent(RadiusMul(psi, r));
            var text = $"Loose ground {grip:+0;-0;0}% grip, {Percent(DragMul(psi, r)):+0;-0;0}% drag  ·  Corners {corner:+0;-0;0}%  ·  Speed {speed:+0;-0;0}%";
            return Danger(psi, r) > 0f ? text + "  ·  TOO LOW: wears on hard ground" : text;
        }

        private static int Percent(float mul) => (int)Math.Round((mul - 1f) * 100f, MidpointRounding.AwayFromZero);

        private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);

        private static float Finite(float v, float fallback) => float.IsFinite(v) ? v : fallback;
    }
}
