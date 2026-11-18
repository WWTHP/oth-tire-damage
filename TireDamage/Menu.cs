using MelonLoader;
using UnityEngine;

namespace TireDamage
{
    /// <summary>
    /// The tire menu. <see cref="DrawBody"/> is the settings hub tab (registered in Mod); without the
    /// hub, MenuKey opens the same body in a window of its own. Changes save on close / tab leave.
    /// </summary>
    internal static class Menu
    {
        public static bool IsOpen { get; private set; }

        private const float W = 560f, RowH = 26f, Pad = 12f, LabelW = 210f, ValueW = 70f;
        private static float _y, _width, _contentH = 900f;
        private static Vector2 _scroll;
        private static bool _dirty;
        private static CursorLockMode _prevLock;
        private static bool _prevVisible;

        public static void Update()
        {
            if (!HubTab.HubPresent && Settings.Pressed(Settings.MenuKey)) Toggle();
            if (IsOpen && HubTab.HubPresent) Toggle(); // the hub took over
            if (IsOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private static void Toggle()
        {
            IsOpen = !IsOpen;
            if (IsOpen)
            {
                _prevLock = Cursor.lockState;
                _prevVisible = Cursor.visible;
                return;
            }
            SaveIfDirty();
            Cursor.lockState = _prevLock;
            Cursor.visible = _prevVisible;
        }

        public static void SaveIfDirty()
        {
            if (!_dirty) return;
            Settings.Save();
            _dirty = false;
        }

        public static void ResetDefaults()
        {
            Settings.ResetToDefaults();
            _dirty = true;
        }

        /// <summary>Standalone window (no hub).</summary>
        public static void Draw()
        {
            if (!IsOpen) return;
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float viewH = Mathf.Min(Screen.height / scale - 80f, _contentH + 90f);
            var win = new Rect(Screen.width / scale - W - 40f, 40f, W, viewH);
            GUI.Box(win, "");
            GUI.Box(win, "");
            GUI.Label(new Rect(win.x + Pad, win.y + 8f, W - 2 * Pad, RowH), $"<b>Tire damage</b>   ({Settings.MenuKey.Value} to close)");
            var view = new Rect(win.x, win.y + 38f, W, viewH - 38f - 44f);
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0f, 0f, W - 24f, _contentH));
            try { _contentH = DrawBody(W - 24f); }
            finally { GUI.EndScrollView(); }
            float by = win.yMax - 36f;
            if (GUI.Button(new Rect(win.x + Pad, by, 140f, 26f), "Reset defaults")) ResetDefaults();
            if (GUI.Button(new Rect(win.xMax - Pad - 110f, by, 110f, 26f), "Close")) Toggle();
            GUI.matrix = Matrix4x4.identity;
        }

        /// <summary>Menu content at (0, 0), <paramref name="width"/> wide. Returns the height used.</summary>
        public static float DrawBody(float width)
        {
            _width = width;
            _y = 4f;

            Section("Tires");
            if (!Car.Valid) Info("No car yet.");
            for (int i = 0; i < Tires.Count; i++) TireRow(i);
            Info($"Spare wheels: {Tires.SparesLeft} of {Car.Spares.Count} left   Tool kit: {(Car.HasToolKit ? "yes" : "no")}");
            Info(Car.Speed > 1f ? "Stop the car to repair." : $"Click and hold a button (or hold {Settings.RepairKey.Value} for the worst tire).");

            Section("Test");
            if (Button("Pop a tire")) Tires.TestBreak(TireState.Popped);
            if (Button("Rupture a tire")) Tires.TestBreak(TireState.Ruptured);
            if (Button("Service (replace all tires)")) Tires.Service("menu");

            Section("Damage");
            Toggle(Settings.Enabled, "Tire damage on");
            Slider(Settings.HitThreshold, "Hit threshold (m/s)", 1f, 15f, "F1");
            Slider(Settings.DamageScale, "Damage scale", 0f, 0.5f, "F3");
            Slider(Settings.RuptureHit, "Rupture hit", 0.1f, 5f, "F2");
            Slider(Settings.FlatWearPerKm, "Flat wear per km", 0f, 10f, "F2");
            Toggle(Settings.BodyHits, "Body hits near a wheel count");
            Slider(Settings.BodyHitScale, "Body hit scale", 0f, 3f, "F2");

            Section("Flat tire effect");
            Slider(Settings.PoppedRadius, "Popped radius", 0.5f, 1f, "P0");
            Slider(Settings.PoppedGrip, "Popped grip", 0.05f, 1f, "P0");
            Slider(Settings.RupturedRadius, "Ruptured radius", 0.5f, 1f, "P0");
            Slider(Settings.RupturedGrip, "Ruptured grip", 0.05f, 1f, "P0");

            Section("Repairs");
            Slider(Settings.PatchHealth, "Patched health", 0.05f, 1f, "P0");
            Slider(Settings.PatchSeconds, "Patch time (s)", 0.5f, 60f, "F1");
            Slider(Settings.SpareSeconds, "Spare change time (s)", 0.5f, 60f, "F1");
            Slider(Settings.ServiceRadius, "Service radius (m)", 0f, 200f, "F0");
            Toggle(Settings.HideUsedSpares, "Hide used spare wheels");
            Info($"Spares: parts named '{Settings.SpareKeywords.Value}' (not '{Settings.SpareExclude.Value}'); " +
                 $"tool kit: '{Settings.ToolKeywords.Value}'. Edit in MelonPreferences.cfg.");
            Info($"Keys: hold {Settings.RepairKey.Value} to repair" +
                 (HubTab.HubPresent ? "" : $", {Settings.MenuKey.Value} menu") + ".");

            Section("Other");
            Toggle(Settings.ShowToasts, "On-screen messages");
            Toggle(Settings.LogHits, "Log every hit (tuning)");
            if (CrashGuard.DisabledStages.Count > 0)
            {
                Info("Switched off after the game crashed in it: " + string.Join(", ", CrashGuard.DisabledStages));
                if (Button("Re-enable (restart the game)")) CrashGuard.ReEnableAll();
            }
            return _y + Pad;
        }

        /// <summary>One tire: name, health, and a hold-to-repair button when it is flat.</summary>
        private static void TireRow(int i)
        {
            const float RowTall = 30f, NameW = 120f, HealthW = 150f, ButtonW = 190f;
            var t = Tires.TireAt(i);
            float x = Pad + 8f, y = _y + 2f;
            var color = t.State == TireState.Ok ? (t.Health < 0.5f ? "#ffd27f" : "#c8f0c8") : "#ff8080";
            GUI.Label(new Rect(x, y, NameW, RowTall), Tires.LongName(Tires.Label(i)));
            var health = FieldRepair.Describe(t.State, t.Health);
            if (t.State == TireState.Popped) health += $"  ({t.Carcass:P0} left)";
            GUI.Label(new Rect(x + NameW, y, HealthW, RowTall), $"<size=16><color={color}><b>{health}</b></color></size>");

            var plan = Tires.PlanFor(i);
            var bx = x + NameW + HealthW + 8f;
            if (plan.Kind == RepairKind.None)
            {
                if (t.State != TireState.Ok)
                    GUI.Label(new Rect(bx, y, ButtonW + 60f, RowTall), $"<color=#aaaaaa>{plan.Reason}</color>");
            }
            else
            {
                var rect = new Rect(bx, y, ButtonW, RowTall - 4f);
                var caption = plan.Kind == RepairKind.Patch ? "Hold: patch (tool kit)" : "Hold: fit spare";
                if (GUI.RepeatButton(rect, caption)) Tires.HoldFromMenu(i);
                if (Tires.RepairingTire == i && Tires.RepairProgress > 0f)
                    GUI.Box(new Rect(rect.x, rect.yMax - 5f, rect.width * Tires.RepairProgress, 5f), "");
            }
            _y += RowTall + 2f;
        }

        private static void Section(string title)
        {
            _y += 8f;
            GUI.Label(new Rect(Pad, _y, _width - 2 * Pad, RowH), $"<b><color=#9fd3ff>{title}</color></b>");
            _y += RowH;
        }

        private static void Info(string text)
        {
            GUI.Label(new Rect(Pad + 8f, _y, _width - 2 * Pad - 8f, RowH), text);
            _y += RowH;
        }

        private static bool Button(string text)
        {
            bool hit = GUI.Button(new Rect(Pad + 8f, _y + 2f, 260f, RowH - 4f), text);
            _y += RowH;
            return hit;
        }

        private static void Toggle(MelonPreferences_Entry<bool> e, string label)
        {
            bool v = GUI.Toggle(new Rect(Pad + 8f, _y, _width - 2 * Pad, RowH), e.Value, " " + label);
            if (v != e.Value) { e.Value = v; _dirty = true; }
            _y += RowH;
        }

        private static void Slider(MelonPreferences_Entry<float> e, string label, float min, float max, string fmt)
        {
            GUI.Label(new Rect(Pad + 8f, _y, LabelW, RowH), label);
            float sliderW = _width - LabelW - ValueW - 3 * Pad - 8f;
            float v = GUI.HorizontalSlider(new Rect(Pad + 8f + LabelW, _y + 8f, sliderW, RowH - 8f), e.Value, min, max);
            GUI.Label(new Rect(_width - ValueW - Pad, _y, ValueW, RowH), e.Value.ToString(fmt));
            if (Mathf.Abs(v - e.Value) > 1e-5f) { e.Value = v; _dirty = true; }
            _y += RowH;
        }
    }
}
