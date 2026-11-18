using System;
using System.Collections.Generic;

namespace TireDamage
{
    internal enum RepairKind
    {
        None = 0,
        Patch = 1,
        Spare = 2,
    }

    /// <summary>What a field repair would do: which tire and how, or why nothing can be done.</summary>
    internal sealed class RepairPlan
    {
        public RepairPlan(int tire, RepairKind kind, string reason)
        {
            Tire = tire;
            Kind = kind;
            Reason = reason;
        }

        public int Tire { get; }
        public RepairKind Kind { get; }

        /// <summary>Why nothing can be repaired (Kind == None), else null.</summary>
        public string Reason { get; }
    }

    /// <summary>
    /// Field repairs: the tool kit (reusable) patches a popped tire; a spare wheel (one use each until
    /// the next service) replaces a popped or ruptured one. Ruptured tires come first, then popped.
    /// A popped tire is patched when a tool kit is mounted, so spares are kept for ruptures.
    /// </summary>
    internal static class FieldRepair
    {
        public static RepairPlan Plan(IReadOnlyList<Tire> tires, bool hasToolKit, int sparesLeft)
        {
            int ruptured = -1, popped = -1;
            for (int i = 0; i < tires.Count; i++)
            {
                if (tires[i].State == TireState.Ruptured && ruptured < 0) ruptured = i;
                if (tires[i].State == TireState.Popped && popped < 0) popped = i;
            }
            if (ruptured < 0 && popped < 0) return new RepairPlan(-1, RepairKind.None, "No flat tires.");
            if (ruptured >= 0 && sparesLeft > 0) return new RepairPlan(ruptured, RepairKind.Spare, null);
            if (popped >= 0 && hasToolKit) return new RepairPlan(popped, RepairKind.Patch, null);
            if (popped >= 0 && sparesLeft > 0) return new RepairPlan(popped, RepairKind.Spare, null);
            return new RepairPlan(-1, RepairKind.None, ruptured >= 0
                ? "Ruptured tire: needs a spare wheel (none left)."
                : "Popped tire: needs a tool kit (pipe wrench or hi-lift jack) or a spare wheel.");
        }

        /// <summary>Counts parts whose name contains any keyword and no excluded word (case-insensitive).</summary>
        public static int CountParts(IEnumerable<string> partNames, IReadOnlyList<string> keywords, IReadOnlyList<string> exclude)
        {
            int n = 0;
            if (partNames == null || keywords == null || keywords.Count == 0) return 0;
            foreach (var name in partNames)
            {
                if (string.IsNullOrEmpty(name)) continue;
                if (!ContainsAny(name, keywords)) continue;
                if (exclude != null && ContainsAny(name, exclude)) continue;
                n++;
            }
            return n;
        }

        private static bool ContainsAny(string name, IReadOnlyList<string> words)
        {
            foreach (var w in words)
                if (name.Contains(w, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Comma-separated keywords → trimmed, non-empty list.</summary>
        public static List<string> ParseKeywords(string csv)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(csv)) return list;
            foreach (var part in csv.Split(','))
            {
                var t = part.Trim();
                if (t.Length > 0) list.Add(t);
            }
            return list;
        }

        /// <summary>
        /// "FL"/"FR"/"RL"/"RR" from a wheel's position in the car's local space (x right, z forward),
        /// with a middle axle ("ML"/"MR") when the car has more than two axles' worth of spread.
        /// </summary>
        public static string WheelLabel(float localX, float localZ, float minZ, float maxZ)
        {
            string side = localX < 0f ? "L" : "R";
            float span = maxZ - minZ;
            if (!(span > 0.01f)) return "F" + side;
            float t = (localZ - minZ) / span;
            string axle = t >= 0.67f ? "F" : t <= 0.33f ? "R" : "M";
            return axle + side;
        }

        public static string Describe(TireState state, float health) => state switch
        {
            TireState.Popped => "POPPED",
            TireState.Ruptured => "RUPTURED",
            _ => $"{Math.Round(health * 100f)}%",
        };
    }
}
