using System;
using MelonLoader;
using MelonLoader.Preferences;
using UnityEngine.InputSystem;

namespace TireDamage
{
    /// <summary>User settings, stored in UserData/MelonPreferences.cfg [TireDamage].</summary>
    internal static class Settings
    {
        internal static MelonPreferences_Category Cat;
        internal static MelonPreferences_Entry<bool> Enabled;
        internal static MelonPreferences_Entry<string> MenuKey;
        internal static MelonPreferences_Entry<string> TireKey;
        internal static MelonPreferences_Entry<string> TirePadButtons;

        internal static MelonPreferences_Entry<float> HitThreshold;
        internal static MelonPreferences_Entry<float> DamageScale;
        internal static MelonPreferences_Entry<float> RuptureHit;
        internal static MelonPreferences_Entry<float> FlatWearPerKm;
        internal static MelonPreferences_Entry<bool> BodyHits;
        internal static MelonPreferences_Entry<float> BodyHitScale;
        internal static MelonPreferences_Entry<float> BodyHitRadius;

        internal static MelonPreferences_Entry<float> PoppedRadius;
        internal static MelonPreferences_Entry<float> RupturedRadius;
        internal static MelonPreferences_Entry<float> PoppedGrip;
        internal static MelonPreferences_Entry<float> RupturedGrip;

        internal static MelonPreferences_Entry<float> PatchHealth;
        internal static MelonPreferences_Entry<float> PatchSeconds;
        internal static MelonPreferences_Entry<float> SpareSeconds;
        internal static MelonPreferences_Entry<string> SpareKeywords;
        internal static MelonPreferences_Entry<string> SpareExclude;
        internal static MelonPreferences_Entry<string> ToolKeywords;
        internal static MelonPreferences_Entry<int> MaxSpares;
        internal static MelonPreferences_Entry<bool> HideUsedSpares;

        internal static MelonPreferences_Entry<float> TirePsi;
        internal static MelonPreferences_Entry<float> NormalPsi;
        internal static MelonPreferences_Entry<float> MinPsi;
        internal static MelonPreferences_Entry<float> SafePsi;
        internal static MelonPreferences_Entry<float> LowPsiGrip;
        internal static MelonPreferences_Entry<float> LowPsiCornerGrip;
        internal static MelonPreferences_Entry<float> LowPsiDrag;
        internal static MelonPreferences_Entry<float> LowPsiRadius;
        internal static MelonPreferences_Entry<float> LowPsiWearPerKm;
        internal static MelonPreferences_Entry<float> LowPsiHitFactor;

        internal static MelonPreferences_Entry<float> ServiceRadius;
        internal static MelonPreferences_Entry<bool> ShowToasts;
        internal static MelonPreferences_Entry<bool> LogHits;
        internal static MelonPreferences_Entry<bool> LogGrip;
        internal static MelonPreferences_Entry<bool> Multiplayer;
        internal static MelonPreferences_Entry<bool> WheelDragAlways;

        public static void Init()
        {
            Cat = MelonPreferences.CreateCategory("TireDamage", "Tire damage");
            Enabled = Cat.CreateEntry("Enabled", true, "Enabled", "Turn tire damage on or off (off restores every tire).");
            // Registry: docs/games/over-the-hill/README.md (F2/F3 OTHNight, F5 hub, F6-F11 our mods).
            MenuKey = Cat.CreateEntry("MenuKey", "F1", "Menu key",
                "Opens the tire menu when the settings hub isn't installed (Unity Input System key name).");
            TireKey = Cat.CreateEntry("TireKey", "F4", "Tire menu key",
                "Opens the tire repair screen (Unity Input System key name).", oldIdentifier: "RepairKey");
            TirePadButtons = Cat.CreateEntry("TirePadButtons", "L3+R3", "Tire screen controller buttons",
                "Controller buttons pressed together to open/close the tire screen, e.g. L3+R3, LB+RB, View. Empty = none.");

            HitThreshold = Float("HitThreshold", 4f, 1f, 15f, "Hit threshold (m/s)",
                "Suspension or body impacts slower than this don't hurt a tire.");
            DamageScale = Float("DamageScale", 0.04f, 0f, 0.5f, "Damage scale",
                "Damage per hit = scale x (impact speed - threshold)^2. A new tire has 1.0 health.");
            RuptureHit = Float("RuptureHit", 0.9f, 0.1f, 5f, "Rupture hit",
                "A hit at least this big that empties a tire ruptures it instead of popping it.");
            FlatWearPerKm = Float("FlatWearPerKm", 0.5f, 0f, 10f, "Flat wear per km",
                "Driving on a popped tire wears its carcass by this much per km; at 1.0 it ruptures.");
            BodyHits = Cat.CreateEntry("BodyHits", true, "Body hits",
                "Hard body impacts near a wheel (rocks, walls) also damage that tire.");
            BodyHitScale = Float("BodyHitScale", 0.5f, 0f, 3f, "Body hit scale", "Body impact damage relative to suspension hits.");
            BodyHitRadius = Float("BodyHitRadius", 0.8f, 0.1f, 3f, "Body hit radius (m)",
                "A body impact this close to a wheel counts against that tire.");

            PoppedRadius = Float("PoppedRadius", 0.85f, 0.5f, 1f, "Popped tire radius", "Wheel radius of a popped tire (x normal).");
            RupturedRadius = Float("RupturedRadius", 0.75f, 0.5f, 1f, "Ruptured tire radius", "Wheel radius of a ruptured tire (x normal).");
            PoppedGrip = Float("PoppedGrip", 0.6f, 0.05f, 1f, "Popped tire grip", "Tire grip of a popped tire (x normal).");
            RupturedGrip = Float("RupturedGrip", 0.35f, 0.05f, 1f, "Ruptured tire grip", "Tire grip of a ruptured tire (x normal).");

            PatchHealth = Float("PatchHealth", 0.5f, 0.05f, 1f, "Patched health", "Health of a tire patched with the tool kit.");
            PatchSeconds = Float("PatchSeconds", 6f, 0.5f, 60f, "Patch time (s)", "How long to hold the repair key to patch a tire.");
            SpareSeconds = Float("SpareSeconds", 10f, 0.5f, 60f, "Spare change time (s)", "How long to hold the repair key to fit a spare.");
            SpareKeywords = Cat.CreateEntry("SpareKeywords", "sparewheel", "Spare wheel parts",
                "Mounted parts whose name contains one of these words are spare wheels (one tire each).");
            SpareExclude = Cat.CreateEntry("SpareExclude", "wreath", "Not spare wheels",
                "Parts containing one of these words never count as spares (decorations).");
            ToolKeywords = Cat.CreateEntry("ToolKeywords", "pipewrench,hiliftjack", "Tool kit parts",
                "A mounted part containing one of these words is a tool kit (patches popped tires, reusable).");
            MaxSpares = Cat.CreateEntry("MaxSpares", 4, "Max spares", "Most spare wheels counted on one car.",
                validator: new ValueRange<int>(0, 8));
            HideUsedSpares = Cat.CreateEntry("HideUsedSpares", true, "Hide used spares",
                "Hide a spare wheel cosmetic once it is fitted (back after a service).");

            TirePsi = Float("TirePsi", 32f, 1f, 60f, "Tire pressure (PSI)",
                "Pressure of all tires. Full (NormalPsi) = the game's normal tires; lower = more traction on mud/sand/snow/ice, worse cornering, slower, and below SafePsi the tires wear on hard ground.");
            NormalPsi = Float("NormalPsi", 32f, 20f, 60f, "Full pressure (PSI)", "Full pressure: exactly the game's normal tire behaviour.");
            MinPsi = Float("MinPsi", 8f, 1f, 30f, "Lowest pressure (PSI)", "The lowest pressure you can set.");
            SafePsi = Float("SafePsi", 16f, 1f, 60f, "Safe pressure (PSI)",
                "Below this, driving wears the tires and hits damage them more (pinch flats).");
            LowPsiGrip = Float("LowPsiGrip", 1.3f, 1f, 2f, "Mud/sand/snow/ice traction at lowest pressure",
                "Traction on mud, sand, snow and ice at the lowest pressure (x normal). Other ground: unchanged.");
            LowPsiCornerGrip = Float("LowPsiCornerGrip", 0.85f, 0.3f, 1f, "Cornering grip at lowest pressure",
                "Cornering grip at the lowest pressure, on any ground (x normal): soft sidewalls roll over.");
            LowPsiDrag = Float("LowPsiDrag", 0.5f, 0f, 1f, "Mud/sand/snow/ice drag at lowest pressure",
                "Ground drag of mud, sand, snow and ice at the lowest pressure (x normal): the tire floats instead of digging in. " +
                "Only applied while this machine runs your car's physics and no other player's car.");
            LowPsiRadius = Float("LowPsiRadius", 0.92f, 0.7f, 1f, "Radius at lowest pressure",
                "Wheel radius at the lowest pressure (x normal); smaller = lower top speed.");
            LowPsiWearPerKm = Float("LowPsiWearPerKm", 0.5f, 0f, 5f, "Wear per km at lowest pressure",
                "Tire health lost per km driven on hard ground at the lowest pressure (none at the safe pressure, none on mud/sand/snow/ice).");
            LowPsiHitFactor = Float("LowPsiHitFactor", 1.5f, 1f, 4f, "Hit damage at lowest pressure",
                "Hit damage multiplier on hard ground at the lowest pressure (x1 at the safe pressure and on mud/sand/snow/ice).");

            ServiceRadius = Float("ServiceRadius", 25f, 0f, 200f, "Service radius (m)",
                "Stopping this close to a house or the mechanic replaces every tire (0 = only the game's repairs).");
            ShowToasts = Cat.CreateEntry("ShowToasts", true, "On-screen messages", "Show pops, repairs and services on screen.");
            LogHits = Cat.CreateEntry("LogHits", true, "Log hits", "Log every damaging hit (for tuning).");
            // New identifier (was "LogGrip", on by default in 0.6.0) so existing installs switch it off too.
            LogGrip = Cat.CreateEntry("LogGripSamples", false, "Log grip", "Every 3 s while driving below full pressure: ground and grip per tire (for tuning).");
            Multiplayer = Cat.CreateEntry("Multiplayer", true, "Multiplayer",
                "Send your tire pressure and flats to the host, and as host apply other players' tires to their cars " +
                "(the host runs every car's physics). Needs the mod on the host.");
            WheelDragAlways = Cat.CreateEntry("WheelDragAlways", false, "Per-wheel drag in single player (test)",
                "Use the per-wheel ground drag (as in multiplayer) instead of changing the ground types, to test it alone.");
        }

        private static MelonPreferences_Entry<float> Float(string id, float def, float min, float max, string name, string desc) =>
            Cat.CreateEntry(id, def, name, desc, validator: new ValueRange<float>(min, max));

        public static TireRules Rules() => new()
        {
            HitThreshold = HitThreshold.Value,
            DamageScale = DamageScale.Value,
            RuptureHit = RuptureHit.Value,
            FlatWearPerKm = FlatWearPerKm.Value,
            PatchHealth = PatchHealth.Value,
        };

        public static PressureRules PressureRules() => new()
        {
            NormalPsi = NormalPsi.Value,
            MinPsi = MinPsi.Value,
            SafePsi = SafePsi.Value,
            GripAtMin = LowPsiGrip.Value,
            CornerGripAtMin = LowPsiCornerGrip.Value,
            DragAtMin = LowPsiDrag.Value,
            RadiusAtMin = LowPsiRadius.Value,
            WearPerKmAtMin = LowPsiWearPerKm.Value,
            HitFactorAtMin = LowPsiHitFactor.Value,
        };

        /// <summary>The current pressure, clamped to the configured range.</summary>
        public static float Psi() => Pressure.Clamp(TirePsi.Value, PressureRules());

        public static void Save() => Cat.SaveToFile(false);

        public static void ResetToDefaults()
        {
            foreach (var e in Cat.Entries) e.ResetToDefault();
        }

        public static bool Pressed(MelonPreferences_Entry<string> entry) => TryKey(entry, out var k) && k.wasPressedThisFrame;

        private static bool TryKey(MelonPreferences_Entry<string> entry, out UnityEngine.InputSystem.Controls.KeyControl control)
        {
            control = null;
            var kb = Keyboard.current;
            if (kb == null || !Enum.TryParse(entry.Value, true, out Key key) || key == UnityEngine.InputSystem.Key.None) return false;
            control = kb[key];
            return control != null;
        }
    }
}
