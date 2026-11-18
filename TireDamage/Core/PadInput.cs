using System;
using System.Collections.Generic;

namespace TireDamage
{
    /// <summary>Gamepad buttons the mod can bind (Input System names; aliases in <see cref="PadInput"/>).</summary>
    internal enum PadButton
    {
        South, East, West, North,
        LeftShoulder, RightShoulder, LeftTrigger, RightTrigger,
        LeftStickPress, RightStickPress, Start, Select,
        DpadUp, DpadDown, DpadLeft, DpadRight,
    }

    internal enum NavDir
    {
        Up, Down, Left, Right,
    }

    /// <summary>
    /// Controller support for the tire screen: parsing the open chord setting, and moving the
    /// selection between tire tiles laid out like the car (left/right columns, front/middle/rear).
    /// </summary>
    internal static class PadInput
    {
        public const int MaxChord = 3;

        private static readonly Dictionary<string, PadButton> Names = new(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = PadButton.South,
            ["Cross"] = PadButton.South,
            ["buttonSouth"] = PadButton.South,
            ["B"] = PadButton.East,
            ["Circle"] = PadButton.East,
            ["buttonEast"] = PadButton.East,
            ["X"] = PadButton.West,
            ["Square"] = PadButton.West,
            ["buttonWest"] = PadButton.West,
            ["Y"] = PadButton.North,
            ["Triangle"] = PadButton.North,
            ["buttonNorth"] = PadButton.North,
            ["LB"] = PadButton.LeftShoulder,
            ["L1"] = PadButton.LeftShoulder,
            ["leftShoulder"] = PadButton.LeftShoulder,
            ["RB"] = PadButton.RightShoulder,
            ["R1"] = PadButton.RightShoulder,
            ["rightShoulder"] = PadButton.RightShoulder,
            ["LT"] = PadButton.LeftTrigger,
            ["L2"] = PadButton.LeftTrigger,
            ["leftTrigger"] = PadButton.LeftTrigger,
            ["RT"] = PadButton.RightTrigger,
            ["R2"] = PadButton.RightTrigger,
            ["rightTrigger"] = PadButton.RightTrigger,
            ["L3"] = PadButton.LeftStickPress,
            ["leftStickPress"] = PadButton.LeftStickPress,
            ["leftStickButton"] = PadButton.LeftStickPress,
            ["R3"] = PadButton.RightStickPress,
            ["rightStickPress"] = PadButton.RightStickPress,
            ["rightStickButton"] = PadButton.RightStickPress,
            ["Start"] = PadButton.Start,
            ["Menu"] = PadButton.Start,
            ["Options"] = PadButton.Start,
            ["Select"] = PadButton.Select,
            ["View"] = PadButton.Select,
            ["Back"] = PadButton.Select,
            ["Share"] = PadButton.Select,
            ["DpadUp"] = PadButton.DpadUp,
            ["DpadDown"] = PadButton.DpadDown,
            ["DpadLeft"] = PadButton.DpadLeft,
            ["DpadRight"] = PadButton.DpadRight,
        };

        /// <summary>
        /// "L3+R3" → the buttons to hold together. Empty or "None" = no chord (empty list). Null when
        /// a name is unknown, repeated, or there are more than <see cref="MaxChord"/> buttons.
        /// </summary>
        public static List<PadButton> ParseChord(string text)
        {
            var result = new List<PadButton>();
            if (string.IsNullOrWhiteSpace(text) || text.Trim().Equals("None", StringComparison.OrdinalIgnoreCase)) return result;
            foreach (var part in text.Split('+'))
            {
                var name = part.Trim();
                if (!Names.TryGetValue(name, out var b) || result.Contains(b)) return null;
                result.Add(b);
            }
            return result.Count <= MaxChord ? result : null;
        }

        /// <summary>
        /// The tile to select after moving <paramref name="dir"/> from <paramref name="current"/>.
        /// Tiles are placed by their labels ("FL", "RR", "ML"…): left/right switches column on the
        /// same axle; up/down goes to the next axle towards the front/rear in the same column.
        /// Stays put when there's nothing that way.
        /// </summary>
        public static int Move(IReadOnlyList<string> labels, int current, NavDir dir)
        {
            if (labels == null || labels.Count == 0) return -1;
            if (current < 0 || current >= labels.Count) return 0;
            int row = Row(labels[current]), col = Col(labels[current]);
            int best = current, bestDist = int.MaxValue;
            for (int i = 0; i < labels.Count; i++)
            {
                if (i == current) continue;
                int r = Row(labels[i]), c = Col(labels[i]);
                bool ok = dir switch
                {
                    NavDir.Left => r == row && c < col,
                    NavDir.Right => r == row && c > col,
                    NavDir.Up => c == col && r < row,
                    _ => c == col && r > row,
                };
                int d = Math.Abs(r - row) + Math.Abs(c - col);
                if (ok && d < bestDist) { best = i; bestDist = d; }
            }
            return best;
        }

        private static int Row(string label) =>
            label != null && label.Length > 0 ? label[0] switch { 'F' => 0, 'M' => 1, _ => 2 } : 2;

        private static int Col(string label) => label != null && label.EndsWith('L') ? 0 : 1;
    }
}
