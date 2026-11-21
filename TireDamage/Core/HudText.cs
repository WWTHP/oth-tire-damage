using System;
using System.Collections.Generic;

namespace TireDamage
{
    /// <summary>Screen corner of the tire HUD.</summary>
    internal enum HudCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
    }

    /// <summary>Text and choices for the always-on tire HUD (<c>TireHud</c>), kept free of Unity.</summary>
    internal static class HudText
    {
        /// <summary>Surface of a wheel that is in the air.</summary>
        public const int Airborne = -1;

        /// <summary>
        /// Readable name of a ground label (the game's <c>MaterialLabel</c> flags). Several flags:
        /// the lowest one. <see cref="Airborne"/>: "Air".
        /// </summary>
        public static string SurfaceName(int label)
        {
            if (label == Airborne) return "Air";
            if (label <= 0) return "Ground";
            int bit = label & -label;
            return bit switch
            {
                0x2 => "Grass",
                0x4 => "Gravel",
                0x8 => "Mud",
                0x10 => "Deep mud",
                0x20 => "Dirt",
                0x40 => "Water",
                0x80 => "Rock",
                0x100 => "Loose rock",
                0x200 => "Wood",
                0x400 => "Log",
                0x800 => "Snow",
                0x1000 => "Deep snow",
                0x2000 => "Ice",
                0x4000 => "Sand",
                0x8000 => "Deep sand",
                0x10000 => "Quicksand",
                0x20000 => "Bricks",
                0x40000 => "Light snow",
                _ => "Ground",
            };
        }

        /// <summary>
        /// The surface the car is on: the most common one under its wheels, or the two most common
        /// ("Snow / Gravel") when the wheels disagree. Wheels in the air only count when all are.
        /// </summary>
        public static string MainSurface(IReadOnlyList<int> labels)
        {
            var counts = new List<(string Name, int Count)>();
            foreach (int l in labels)
            {
                if (l == Airborne) continue;
                string name = SurfaceName(l);
                int i = counts.FindIndex(c => c.Name == name);
                if (i < 0) counts.Add((name, 1));
                else counts[i] = (name, counts[i].Count + 1);
            }
            if (counts.Count == 0) return labels.Count > 0 ? SurfaceName(Airborne) : "";
            // Stable: ties keep wheel order (front wheels first).
            var sorted = new List<(string Name, int Count)>(counts);
            sorted.Sort((a, b) => b.Count != a.Count ? b.Count.CompareTo(a.Count) : counts.IndexOf(a).CompareTo(counts.IndexOf(b)));
            return sorted.Count == 1 ? sorted[0].Name : $"{sorted[0].Name} / {sorted[1].Name}";
        }

        /// <summary>Traction as a percentage of normal, e.g. "130%".</summary>
        public static string Traction(float grip) =>
            float.IsFinite(grip) ? $"{Math.Round(Math.Clamp(grip, 0f, 9.99f) * 100f):0}%" : "—";

        /// <summary>Traction compared with normal: -1 less, 0 normal, 1 more (within half a percent counts as normal).</summary>
        public static int TractionTrend(float grip) =>
            !float.IsFinite(grip) || Math.Abs(grip - 1f) < 0.005f ? 0 : grip > 1f ? 1 : -1;

        /// <summary>Parses the HudCorner setting (case and spaces/dashes ignored). Unknown text: false.</summary>
        public static bool TryParseCorner(string text, out HudCorner corner)
        {
            corner = HudCorner.TopLeft;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string s = text.Replace(" ", "").Replace("-", "").Replace("_", "");
            foreach (var c in Enum.GetValues<HudCorner>())
                if (string.Equals(c.ToString(), s, StringComparison.OrdinalIgnoreCase))
                {
                    corner = c;
                    return true;
                }
            return false;
        }

        /// <summary>Wheel rows from front to back (F, M, R first letters; anything else goes in the middle).</summary>
        public static int Row(string label) =>
            string.IsNullOrEmpty(label) ? 1 : label[0] == 'F' ? 0 : label[0] == 'R' ? 2 : 1;
    }
}
