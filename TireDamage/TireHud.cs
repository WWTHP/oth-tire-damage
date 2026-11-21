using System.Collections.Generic;
using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TireDamage
{
    /// <summary>
    /// Always-on tire HUD in a screen corner (HudCorner, default top left): pressure, the surface
    /// under the car, and a tile per tire with its health and traction (x normal, from pressure,
    /// flats and the ground). Built in the game's HUD canvas like <see cref="TirePanel"/>: the tilt
    /// dial's blurred glass and the HUD font. Shown while the game's HUD is (it mirrors the tilt
    /// dial), hidden while the tire screen is open.
    /// </summary>
    internal static class TireHud
    {
        private const string Stage = "hud";
        private const float CellW = 104f, CellH = 44f, Gap = 8f, HeadH = 44f;
        private static readonly Color More = new(0.55f, 0.85f, 1f, 1f);

        private sealed class Cell
        {
            public Image Tire;
            public TextMeshProUGUI Health, Grip;
            public string LastHealth, LastGrip;
        }

        private static GameObject _root;
        private static RectTransform _rect;
        private static ContextUIInclineIndicator _dial;
        private static TextMeshProUGUI _psi, _surface;
        private static string _lastPsi, _lastSurface, _lastCorner, _badCorner;
        private static readonly List<Cell> Cells = new();
        private static readonly List<int> Surfaces = new();
        private static int _builtFor = -1;
        private static float _nextTry;
        private static bool _failed;

        /// <summary>The HudCorner setting (top left if it isn't a corner name).</summary>
        public static HudCorner Corner
        {
            get
            {
                var text = Settings.HudCorner.Value;
                if (HudText.TryParseCorner(text, out var c)) return c;
                if (_badCorner != text)
                {
                    _badCorner = text;
                    Mod.Log.Warning($"HudCorner '{text}' isn't TopLeft, TopRight, BottomLeft or BottomRight; using TopLeft.");
                }
                return HudCorner.TopLeft;
            }
        }

        public static void Update()
        {
            bool want = Settings.Enabled.Value && Settings.ShowHud.Value && Car.Valid && Tires.Count > 0 && !TirePanel.IsOpen;
            if (!want || _failed)
            {
                Hide();
                return;
            }
            if (_root == null || _root.WasCollected || _builtFor != Car.Generation || Cells.Count != Tires.Count)
            {
                if (Time.unscaledTime < _nextTry) return;
                _nextTry = Time.unscaledTime + 1f; // HUD not up yet: retry once a second
                Build();
                if (_root == null) return;
            }
            // Follow the game's HUD: hidden with it (menus, photo mode, map).
            bool hudShown = _dial != null && !_dial.WasCollected && _dial.gameObject.activeInHierarchy;
            if (_root.activeSelf != hudShown) _root.SetActive(hudShown);
            if (!hudShown) return;
            Place();
            Refresh();
        }

        private static void Hide()
        {
            if (_root != null && !_root.WasCollected && _root.activeSelf) _root.SetActive(false);
        }

        // ------------------------------------------------------------------ build

        private static void Build()
        {
            Destroy();
            var dials = Object.FindObjectsOfType<ContextUIInclineIndicator>();
            if (dials.Length == 0) return;
            if (!CrashGuard.Enter(Stage)) { Fail("disabled after a crash"); return; }
            try
            {
                _dial = dials[0];
                var canvas = _dial.GetComponentInParent<Canvas>();
                if (canvas == null) throw new System.InvalidOperationException("tilt dial has no canvas");
                var font = HudUi.FindFont(canvas);

                // Rows front to back (F, M, R), left wheel in the left column.
                var rows = new SortedSet<int>();
                for (int i = 0; i < Tires.Count; i++) rows.Add(HudText.Row(Tires.Label(i)));
                var rowIndex = new List<int>(rows);
                float w = CellW * 2f + Gap * 3f;
                float h = HeadH + rowIndex.Count * (CellH + Gap) + Gap;

                _root = new GameObject("TireHud");
                _rect = _root.AddComponent<RectTransform>();
                _rect.SetParent(canvas.rootCanvas.transform, false);
                _rect.sizeDelta = new Vector2(w, h);
                _lastCorner = null;

                bool glassed = HudUi.Glass(_dial, _rect);
                HudUi.Stretch(HudUi.Panel("Shade", _rect, new Color(0.04f, 0.05f, 0.06f, 0.42f)).rectTransform);

                var title = HudUi.Text("Title", _rect, font, 11f, HudUi.Dim, At(w, h, Gap + 2f, 14f, 60f), 60f, "TIRES");
                title.characterSpacing = 10f;
                title.alignment = TextAlignmentOptions.Left;
                _surface = HudUi.Text("Surface", _rect, font, 14f, Color.white, At(w, h, Gap + 2f, 32f, w - 90f), w - 90f, "");
                _surface.alignment = TextAlignmentOptions.Left;
                _surface.enableAutoSizing = true;
                _surface.fontSizeMin = 9f;
                _surface.fontSizeMax = 14f;
                _psi = HudUi.Text("Psi", _rect, font, 22f, Color.white, At(w, h, w - Gap - 2f - 70f, 23f, 70f), 70f, "");
                _psi.alignment = TextAlignmentOptions.Right;
                var rule = HudUi.Panel("Rule", _rect, new Color(1f, 1f, 1f, 0.15f));
                rule.rectTransform.sizeDelta = new Vector2(w - Gap * 2f, 1f);
                rule.rectTransform.anchoredPosition = new Vector2(0f, h / 2f - HeadH);

                Cells.Clear();
                for (int i = 0; i < Tires.Count; i++)
                {
                    var label = Tires.Label(i);
                    bool left = !label.EndsWith('R');
                    float x = left ? Gap : Gap * 2f + CellW;
                    float y = HeadH + Gap + rowIndex.IndexOf(HudText.Row(label)) * (CellH + Gap);
                    Cells.Add(MakeCell(font, w, h, x, y, label, left));
                }
                _lastPsi = _lastSurface = null;
                _builtFor = Car.Generation;
                Mod.Log.Msg($"Tire HUD built ({Tires.Count} tires, {Corner}, glass: {(glassed ? "cloned" : "none")}, " +
                            $"font: {(font != null ? font.name : "TMP default")}).");
            }
            catch (System.Exception ex)
            {
                Destroy();
                Fail(ex.Message);
            }
            finally { CrashGuard.Exit(Stage); }
        }

        /// <summary>A tire tile: the tire as a coloured bar on the outside, label, health and traction.</summary>
        private static Cell MakeCell(TMP_FontAsset font, float w, float h, float x, float y, string label, bool left)
        {
            var c = new Cell();
            var bg = HudUi.Panel("Cell" + label, _rect, new Color(1f, 1f, 1f, 0.05f));
            bg.rectTransform.sizeDelta = new Vector2(CellW, CellH);
            bg.rectTransform.anchoredPosition = At(w, h, x, y + CellH / 2f, CellW);

            c.Tire = HudUi.Panel("Tire", bg.rectTransform, HudUi.Good);
            c.Tire.rectTransform.sizeDelta = new Vector2(6f, CellH - 12f);
            c.Tire.rectTransform.anchoredPosition = new Vector2(left ? -CellW / 2f + 8f : CellW / 2f - 8f, 0f);

            float tx = left ? 6f : -6f; // text centre, away from the tire bar
            const float TextW = CellW - 24f;
            var name = HudUi.Text("Name", bg.rectTransform, font, 10f, HudUi.Dim, new Vector2(tx, 12f), TextW, label);
            name.characterSpacing = 6f;
            c.Health = HudUi.Text("Health", bg.rectTransform, font, 16f, Color.white, new Vector2(tx, -1f), TextW, "");
            c.Health.enableAutoSizing = true; // "RUPTURED" shrinks to fit
            c.Health.fontSizeMin = 9f;
            c.Health.fontSizeMax = 16f;
            c.Grip = HudUi.Text("Grip", bg.rectTransform, font, 10f, HudUi.Dim, new Vector2(tx, -14f), TextW, "");
            return c;
        }

        /// <summary>Centre of a box at (x, y) from the panel's top left, width <paramref name="bw"/>, in anchored coordinates.</summary>
        private static Vector2 At(float w, float h, float x, float y, float bw) => new(x + bw / 2f - w / 2f, h / 2f - y);

        private static void Fail(string why)
        {
            _failed = true;
            Mod.Log.Warning($"Couldn't build the tire HUD ({why}); it stays off.");
        }

        private static void Destroy()
        {
            if (_root != null && !_root.WasCollected) Object.Destroy(_root);
            _root = null;
            _rect = null;
            Cells.Clear();
        }

        // ------------------------------------------------------------------ refresh

        private static void Place()
        {
            var corner = Corner;
            string key = $"{corner}/{Settings.HudScale.Value}/{Settings.HudOffsetX.Value}/{Settings.HudOffsetY.Value}";
            if (key == _lastCorner) return;
            _lastCorner = key;
            bool right = corner == HudCorner.TopRight || corner == HudCorner.BottomRight;
            bool top = corner == HudCorner.TopLeft || corner == HudCorner.TopRight;
            var a = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
            _rect.anchorMin = _rect.anchorMax = _rect.pivot = a;
            _rect.anchoredPosition = new Vector2(right ? -Settings.HudOffsetX.Value : Settings.HudOffsetX.Value,
                                                 top ? -Settings.HudOffsetY.Value : Settings.HudOffsetY.Value);
            float s = Mathf.Clamp(Settings.HudScale.Value, 0.5f, 2f);
            _rect.localScale = new Vector3(s, s, 1f);
        }

        private static void Refresh()
        {
            var pr = Settings.PressureRules();
            float psi = Settings.Psi();
            _psi.color = Pressure.Danger(psi, pr) > 0f ? HudUi.Flat : Pressure.Softness(psi, pr) > 0f ? HudUi.Worn : Color.white;
            var text = $"{psi:0} PSI";
            if (text != _lastPsi) { _lastPsi = text; _psi.text = text; }

            Surfaces.Clear();
            for (int i = 0; i < Tires.Count; i++) Surfaces.Add(Tires.SurfaceAt(i));
            var surface = HudText.MainSurface(Surfaces);
            if (surface != _lastSurface) { _lastSurface = surface; _surface.text = surface; }

            for (int i = 0; i < Cells.Count && i < Tires.Count; i++)
            {
                var c = Cells[i];
                var tire = Tires.TireAt(i);
                var color = tire.State != TireState.Ok ? HudUi.Flat : tire.Health < 0.5f ? HudUi.Worn : HudUi.Good;
                c.Tire.color = color;
                c.Health.color = color;
                var health = FieldRepair.Describe(tire.State, tire.Health);
                if (health != c.LastHealth) { c.LastHealth = health; c.Health.text = health; }

                float g = Tires.GripAt(i, out _);
                int trend = HudText.TractionTrend(g);
                c.Grip.color = trend > 0 ? More : trend < 0 ? HudUi.Worn : HudUi.Dim;
                var grip = "grip " + HudText.Traction(g);
                if (grip != c.LastGrip) { c.LastGrip = grip; c.Grip.text = grip; }
            }
        }
    }
}
