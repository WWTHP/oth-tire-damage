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
        internal static MelonPreferences_Entry<string> RepairKey;

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

        internal static MelonPreferences_Entry<float> ServiceRadius;
        internal static MelonPreferences_Entry<bool> ShowToasts;
        internal static MelonPreferences_Entry<bool> LogHits;

        public static void Init()
        {
            Cat = MelonPreferences.CreateCategory("TireDamage", "Tire damage");
            Enabled = Cat.CreateEntry("Enabled", true, "Enabled", "Turn tire damage on or off (off restores every tire).");
            // Registry: docs/games/over-the-hill/README.md (F2/F3 OTHNight, F5 hub, F6-F11 our mods).
            MenuKey = Cat.CreateEntry("MenuKey", "F1", "Menu key",
                "Opens the tire menu when the settings hub isn't installed (Unity Input System key name).");
            RepairKey = Cat.CreateEntry("RepairKey", "F4", "Repair key",
                "Hold while stopped to patch or change the worst flat tire (Unity Input System key name).");

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

            ServiceRadius = Float("ServiceRadius", 25f, 0f, 200f, "Service radius (m)",
                "Stopping this close to a house or the mechanic replaces every tire (0 = only the game's repairs).");
            ShowToasts = Cat.CreateEntry("ShowToasts", true, "On-screen messages", "Show pops, repairs and services on screen.");
            LogHits = Cat.CreateEntry("LogHits", true, "Log hits", "Log every damaging hit (for tuning).");
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

        public static void Save() => Cat.SaveToFile(false);

        public static void ResetToDefaults()
        {
            foreach (var e in Cat.Entries) e.ResetToDefault();
        }

        public static bool Pressed(MelonPreferences_Entry<string> entry) => TryKey(entry, out var k) && k.wasPressedThisFrame;

        public static bool Held(MelonPreferences_Entry<string> entry) => TryKey(entry, out var k) && k.isPressed;

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
