using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TireDamage
{
    /// <summary>
    /// The tire repair screen (TireKey, default F4): a panel in the game's HUD style showing the car
    /// from above with a tile per tire (health %, state, what holding it does). Click and hold a
    /// flat tire to patch it or fit a spare; the tile fills up while you hold. Controller: the
    /// TirePadButtons chord (L3+R3) opens/closes it, D-pad / left stick picks a tire, hold A to
    /// repair, B closes.
    ///
    /// Built in the game's own HUD canvas: the background is a clone of the tilt dial's blurred
    /// glass (<c>Mask/BlurredBackground</c>, a TranslucentImage) with its round sprite removed, text
    /// uses the HUD font (NunitoSans-Bold SDF). If anything is missing the IMGUI fallback is drawn
    /// instead (<see cref="Menu.DrawRepairRows"/>).
    /// </summary>
    internal static class TirePanel
    {
        private const string Stage = "panel";
        private const float W = 480f, H = 430f, PsiY = -125f, PsiBoxX = 92f, TileW = 186f, TileH = 84f, TextW = 136f;
        private static readonly Color Good = new(0.78f, 0.94f, 0.78f, 1f);
        private static readonly Color Worn = new(1f, 0.82f, 0.45f, 1f);
        private static readonly Color Flat = new(1f, 0.38f, 0.32f, 1f);
        private static readonly Color Dim = new(1f, 1f, 1f, 0.55f);

        public static bool IsOpen { get; private set; }

        /// <summary>The uGUI panel couldn't be built: draw the IMGUI fallback.</summary>
        public static bool UseFallback => _failed;

        private sealed class TileUi
        {
            public RectTransform Rect;
            public Image Tire, Fill, Frame;
            public TextMeshProUGUI Name, Health, Action;
            public string LastHealth, LastAction;
        }

        private static GameObject _root;
        private static readonly System.Collections.Generic.List<TileUi> Tiles = new();
        private static TextMeshProUGUI _footer, _status, _psi, _psiEffect;
        private static RectTransform _minus, _plus;
        private static Image _minusImg, _plusImg;
        private static string _lastPsi, _lastPsiEffect;
        private static float _psiRepeatAt;
        private static bool _psiDirty;
        private static string _lastFooter, _lastStatus;
        private static int _builtFor = -1;
        private static bool _failed;
        private static CursorLockMode _prevLock;
        private static bool _prevVisible;
        private static Vector2 _mouse;
        private static int _selected = -1;
        private static string _chordText;
        private static System.Collections.Generic.List<PadButton> _chord = new();

        // ------------------------------------------------------------------ open / close

        public static void Update()
        {
            var pad = Gamepad.current;
            if (Settings.Pressed(Settings.TireKey) || ChordPressed(pad)) Toggle();
            if (!IsOpen) return;
            if (pad != null) UpdatePad(pad);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (_failed) return;
            if (_root == null || _root.WasCollected || _builtFor != Car.Generation || Tiles.Count != Tires.Count) Build();
            if (_root == null) return;
            if (!_root.activeSelf) _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            Refresh();
        }

        public static void Toggle()
        {
            IsOpen = !IsOpen;
            if (IsOpen)
            {
                _selected = -1; // controller selection starts on the first flat tire
                _prevLock = Cursor.lockState;
                _prevVisible = Cursor.visible;
                return;
            }
            if (_root != null && !_root.WasCollected) _root.SetActive(false);
            if (_psiDirty)
            {
                Settings.Save();
                _psiDirty = false;
                Mod.Log.Msg($"Tire pressure set to {Settings.Psi():0} PSI.");
            }
            Cursor.lockState = _prevLock;
            Cursor.visible = _prevVisible;
        }

        // ------------------------------------------------------------------ controller

        /// <summary>All chord buttons held, at least one of them pressed this frame.</summary>
        private static bool ChordPressed(Gamepad pad)
        {
            if (pad == null) return false;
            if (_chordText != Settings.TirePadButtons.Value)
            {
                _chordText = Settings.TirePadButtons.Value;
                _chord = PadInput.ParseChord(_chordText);
                if (_chord == null) Mod.Log.Warning($"TirePadButtons '{_chordText}' isn't a valid button combination (e.g. L3+R3); controller can't open the tire screen.");
            }
            if (_chord == null || _chord.Count == 0) return false;
            bool any = false;
            foreach (var b in _chord)
            {
                var c = Control(pad, b);
                if (c == null || !c.isPressed) return false;
                any |= c.wasPressedThisFrame;
            }
            return any;
        }

        /// <summary>D-pad / left stick moves the selection, A held repairs it, B closes.</summary>
        private static void UpdatePad(Gamepad pad)
        {
            if (Tires.Count == 0) return;
            if (_selected < 0 || _selected >= Tires.Count) _selected = FirstFlat();
            var labels = new System.Collections.Generic.List<string>();
            for (int i = 0; i < Tires.Count; i++) labels.Add(Tires.Label(i));
            if (pad.dpad.up.wasPressedThisFrame || pad.leftStick.up.wasPressedThisFrame) _selected = PadInput.Move(labels, _selected, NavDir.Up);
            if (pad.dpad.down.wasPressedThisFrame || pad.leftStick.down.wasPressedThisFrame) _selected = PadInput.Move(labels, _selected, NavDir.Down);
            if (pad.dpad.left.wasPressedThisFrame || pad.leftStick.left.wasPressedThisFrame) _selected = PadInput.Move(labels, _selected, NavDir.Left);
            if (pad.dpad.right.wasPressedThisFrame || pad.leftStick.right.wasPressedThisFrame) _selected = PadInput.Move(labels, _selected, NavDir.Right);
            if (pad.buttonSouth.isPressed && Car.Speed <= 1f && Tires.PlanFor(_selected).Kind != RepairKind.None)
                Tires.HoldFromMenu(_selected);
            if (pad.buttonEast.wasPressedThisFrame && IsOpen) Toggle();
        }

        private static int FirstFlat()
        {
            for (int i = 0; i < Tires.Count; i++)
                if (Tires.TireAt(i).State != TireState.Ok) return i;
            return 0;
        }

        private static UnityEngine.InputSystem.Controls.ButtonControl Control(Gamepad pad, PadButton b) => b switch
        {
            PadButton.South => pad.buttonSouth,
            PadButton.East => pad.buttonEast,
            PadButton.West => pad.buttonWest,
            PadButton.North => pad.buttonNorth,
            PadButton.LeftShoulder => pad.leftShoulder,
            PadButton.RightShoulder => pad.rightShoulder,
            PadButton.LeftTrigger => pad.leftTrigger,
            PadButton.RightTrigger => pad.rightTrigger,
            PadButton.LeftStickPress => pad.leftStickButton,
            PadButton.RightStickPress => pad.rightStickButton,
            PadButton.Start => pad.startButton,
            PadButton.Select => pad.selectButton,
            PadButton.DpadUp => pad.dpad.up,
            PadButton.DpadDown => pad.dpad.down,
            PadButton.DpadLeft => pad.dpad.left,
            _ => pad.dpad.right,
        };

        /// <summary>OnGUI: remember the mouse (IMGUI coordinates, y down) for hit tests.</summary>
        public static void TrackMouse()
        {
            var e = Event.current;
            if (e != null) _mouse = new Vector2(e.mousePosition.x, Screen.height - e.mousePosition.y);
        }

        // ------------------------------------------------------------------ build

        private static void Build()
        {
            Destroy();
            if (!Car.Valid || Tires.Count == 0) return;
            var dials = Object.FindObjectsOfType<ContextUIInclineIndicator>();
            if (dials.Length == 0) return; // HUD not up yet; retry next frame
            if (!CrashGuard.Enter(Stage)) { Fail("disabled after a crash"); return; }
            try
            {
                var dial = dials[0];
                var canvas = dial.GetComponentInParent<Canvas>();
                if (canvas == null) throw new System.InvalidOperationException("tilt dial has no canvas");
                var font = FindFont(canvas);

                _root = new GameObject("TireRepairPanel");
                var rt = _root.AddComponent<RectTransform>();
                rt.SetParent(canvas.rootCanvas.transform, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(W, H);
                rt.anchoredPosition = Vector2.zero;

                // Blurred glass like the stock dials, as a plain rectangle.
                var glassSrc = dial.transform.Find("Mask/BlurredBackground");
                if (glassSrc != null)
                {
                    var glass = Object.Instantiate(glassSrc.gameObject, rt, false);
                    glass.name = "Glass";
                    Stretch(glass.GetComponent<RectTransform>());
                    var img = glass.GetComponent<Image>();
                    if (img != null) { img.sprite = null; img.raycastTarget = false; }
                }
                var shade = Panel("Shade", rt, new Color(0.04f, 0.05f, 0.06f, 0.42f));
                Stretch(shade.rectTransform);

                var title = Text("Title", rt, font, 20f, Color.white, new Vector2(0f, H / 2f - 26f), W, "TIRES");
                title.characterSpacing = 18f;
                var rule = Panel("Rule", rt, new Color(1f, 1f, 1f, 0.15f));
                rule.rectTransform.sizeDelta = new Vector2(W - 64f, 1f);
                rule.rectTransform.anchoredPosition = new Vector2(0f, H / 2f - 44f);
                _status = Text("Status", rt, font, 14f, Worn, new Vector2(0f, H / 2f - 60f), W, "");

                // Car body from above, tiles at each wheel.
                var body = Panel("Body", rt, new Color(1f, 1f, 1f, 0.05f));
                body.rectTransform.sizeDelta = new Vector2(84f, 176f);
                body.rectTransform.anchoredPosition = new Vector2(0f, 32f);
                var nose = Panel("Nose", rt, new Color(1f, 1f, 1f, 0.3f));
                nose.rectTransform.sizeDelta = new Vector2(56f, 2f);
                nose.rectTransform.anchoredPosition = new Vector2(0f, 118f);

                Tiles.Clear();
                for (int i = 0; i < Tires.Count; i++) Tiles.Add(Tile(rt, font, i));

                // Pressure: − [24 PSI] +, effect summary below.
                var cap = Text("PsiCaption", rt, font, 12f, Dim, new Vector2(0f, PsiY + 30f), W, "TIRE PRESSURE");
                cap.characterSpacing = 6f;
                _psi = Text("Psi", rt, font, 24f, Color.white, new Vector2(0f, PsiY), 140f, "");
                _minusImg = Panel("PsiMinus", rt, new Color(1f, 1f, 1f, 0.08f));
                _minus = _minusImg.rectTransform;
                _minus.sizeDelta = new Vector2(44f, 30f);
                _minus.anchoredPosition = new Vector2(-PsiBoxX, PsiY);
                Text("Label", _minus, font, 22f, Color.white, Vector2.zero, 44f, "−");
                _plusImg = Panel("PsiPlus", rt, new Color(1f, 1f, 1f, 0.08f));
                _plus = _plusImg.rectTransform;
                _plus.sizeDelta = new Vector2(44f, 30f);
                _plus.anchoredPosition = new Vector2(PsiBoxX, PsiY);
                Text("Label", _plus, font, 22f, Color.white, Vector2.zero, 44f, "+");
                _psiEffect = Text("PsiEffect", rt, font, 12f, Dim, new Vector2(0f, PsiY - 28f), W, "");
                _lastPsi = _lastPsiEffect = null;

                _footer = Text("Footer", rt, font, 13f, Dim, new Vector2(0f, -H / 2f + 20f), W, "");
                _footer.characterSpacing = 2f;
                _lastFooter = _lastStatus = null;
                _builtFor = Car.Generation;
                Mod.Log.Msg($"Tire panel built on canvas '{canvas.rootCanvas.name}' ({canvas.rootCanvas.renderMode}, font: {(font != null ? font.name : "TMP default")}, " +
                            $"glass: {(glassSrc != null ? "cloned" : "none")}).");
            }
            catch (System.Exception ex)
            {
                Destroy();
                Fail(ex.Message);
            }
            finally { CrashGuard.Exit(Stage); }
        }

        private static TileUi Tile(RectTransform parent, TMP_FontAsset font, int i)
        {
            var label = Tires.Label(i);
            bool left = label.EndsWith('L');
            float y = label.StartsWith('F') ? 90f : label.StartsWith('R') ? -26f : 32f;
            var t = new TileUi();
            var go = new GameObject("Tile" + label);
            t.Rect = go.AddComponent<RectTransform>();
            t.Rect.SetParent(parent, false);
            t.Rect.anchorMin = t.Rect.anchorMax = t.Rect.pivot = new Vector2(0.5f, 0.5f);
            t.Rect.sizeDelta = new Vector2(TileW, TileH);
            t.Rect.anchoredPosition = new Vector2(left ? -136f : 136f, y);

            t.Frame = Panel("Frame", t.Rect, new Color(1f, 1f, 1f, 0f));
            Stretch(t.Frame.rectTransform);
            t.Fill = Panel("Fill", t.Rect, new Color(1f, 1f, 1f, 0.14f));
            t.Fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            t.Fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            t.Fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            t.Fill.rectTransform.sizeDelta = new Vector2(0f, 0f);

            // The tire itself sits on the car's side of the tile.
            t.Tire = Panel("Tire", t.Rect, Good);
            t.Tire.rectTransform.sizeDelta = new Vector2(14f, 60f);
            t.Tire.rectTransform.anchoredPosition = new Vector2(left ? 78f : -78f, 0f);

            float tx = left ? -12f : 12f;
            t.Name = Text("Name", t.Rect, font, 12f, Dim, new Vector2(tx, 27f), TextW, Tires.LongName(label).ToUpperInvariant());
            t.Name.characterSpacing = 6f;
            // Auto-size: "RUPTURED" shrinks to fit the tile instead of spilling out of it.
            t.Health = Text("Health", t.Rect, font, 26f, Color.white, new Vector2(tx, 1f), TextW, "");
            t.Health.enableAutoSizing = true;
            t.Health.fontSizeMin = 12f;
            t.Health.fontSizeMax = 26f;
            t.Health.overflowMode = TextOverflowModes.Truncate;
            t.Action = Text("Action", t.Rect, font, 12f, Dim, new Vector2(tx, -27f), TextW, "");
            t.Action.enableAutoSizing = true;
            t.Action.fontSizeMin = 9f;
            t.Action.fontSizeMax = 12f;
            return t;
        }

        private static void Fail(string why)
        {
            _failed = true;
            Mod.Log.Warning($"Couldn't build the tire panel in the HUD ({why}); using the drawn panel.");
        }

        private static void Destroy()
        {
            if (_root != null && !_root.WasCollected) Object.Destroy(_root);
            _root = null;
            Tiles.Clear();
        }

        // ------------------------------------------------------------------ refresh

        private static void Refresh()
        {
            var mouse = Mouse.current;
            bool down = mouse != null && mouse.leftButton.isPressed;
            bool moving = Car.Speed > 1f;
            for (int i = 0; i < Tiles.Count && i < Tires.Count; i++)
            {
                var ui = Tiles[i];
                var tire = Tires.TireAt(i);
                var plan = Tires.PlanFor(i);
                bool hover = RectTransformUtility.RectangleContainsScreenPoint(ui.Rect, _mouse);
                if (hover && down && plan.Kind != RepairKind.None && !moving) Tires.HoldFromMenu(i);

                var color = tire.State != TireState.Ok ? Flat : tire.Health < 0.5f ? Worn : Good;
                ui.Tire.color = color;
                ui.Health.color = color;
                bool selected = i == _selected && Gamepad.current != null;
                ui.Frame.color = new Color(1f, 1f, 1f, selected ? 0.14f : hover && plan.Kind != RepairKind.None ? 0.10f : 0f);

                var health = FieldRepair.Describe(tire.State, tire.Health);
                if (health != ui.LastHealth) { ui.LastHealth = health; ui.Health.text = health; }
                string action = plan.Kind switch
                {
                    RepairKind.Patch => "Hold to patch",
                    RepairKind.Spare => "Hold to fit spare",
                    _ => tire.State == TireState.Ok ? (tire.Health < 0.999f ? "worn" : "") : plan.Reason,
                };
                if (tire.State == TireState.Popped && plan.Kind != RepairKind.None)
                    action += $"  ·  {tire.Carcass:P0} left";
                if (action != ui.LastAction) { ui.LastAction = action; ui.Action.text = action; }

                float p = Tires.RepairingTire == i ? Mathf.Max(0f, Tires.RepairProgress) : 0f;
                ui.Fill.rectTransform.sizeDelta = new Vector2(ui.Rect.rect.width * p, 0f);
            }

            RefreshPressure(down, moving);

            string status = !Car.Valid ? "No car" : moving ? "Stop the car to work on the tires" : Tires.RepairText ?? "";
            if (status != _lastStatus) { _lastStatus = status; _status.text = status; }
            string close = Gamepad.current != null ? "A repair   ·   LB/RB pressure   ·   B close" : $"{Settings.TireKey.Value} to close";
            string footer = $"Spares {Tires.SparesLeft}/{Car.Spares.Count}   ·   Tool kit {(Car.HasToolKit ? "yes" : "no")}   ·   {close}";
            if (footer != _lastFooter) { _lastFooter = footer; _footer.text = footer; }
        }

        /// <summary>
        /// The −/+ boxes (mouse) and LB/RB (controller) change the pressure of all tires by 1 PSI,
        /// repeating while held. Only with the car stopped. Saved when the screen closes.
        /// </summary>
        private static void RefreshPressure(bool mouseDown, bool moving)
        {
            var pr = Settings.PressureRules();
            float psi = Settings.Psi();
            bool overMinus = RectTransformUtility.RectangleContainsScreenPoint(_minus, _mouse);
            bool overPlus = RectTransformUtility.RectangleContainsScreenPoint(_plus, _mouse);
            var pad = Gamepad.current;
            int dir = 0;
            if (mouseDown && overMinus || pad != null && pad.leftShoulder.isPressed) dir = -1;
            else if (mouseDown && overPlus || pad != null && pad.rightShoulder.isPressed) dir = 1;

            if (dir == 0 || moving) _psiRepeatAt = 0f;
            else if (Time.unscaledTime >= _psiRepeatAt)
            {
                // First step at once, then repeat after a short pause.
                _psiRepeatAt = Time.unscaledTime + (_psiRepeatAt == 0f ? 0.4f : 0.08f);
                float next = Pressure.Change(psi, dir, pr);
                if (next != psi)
                {
                    Settings.TirePsi.Value = next;
                    _psiDirty = true;
                    psi = next;
                }
            }

            _minusImg.color = new Color(1f, 1f, 1f, overMinus && !moving ? 0.18f : 0.08f);
            _plusImg.color = new Color(1f, 1f, 1f, overPlus && !moving ? 0.18f : 0.08f);
            float danger = Pressure.Danger(psi, pr);
            _psi.color = danger > 0f ? Flat : Pressure.Softness(psi, pr) > 0f ? Worn : Color.white;
            _psiEffect.color = danger > 0f ? Flat : Dim;
            var text = $"{psi:0} PSI";
            if (text != _lastPsi) { _lastPsi = text; _psi.text = text; }
            var effect = Pressure.Describe(psi, pr);
            if (effect != _lastPsiEffect) { _lastPsiEffect = effect; _psiEffect.text = effect; }
        }

        // ------------------------------------------------------------------ helpers

        private static Image Panel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static void Stretch(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size, Color color,
            Vector2 pos, float width, string text)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, size * 1.4f);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        /// <summary>The HUD font: from the RPM display, else any TMP text on the canvas.</summary>
        private static TMP_FontAsset FindFont(Canvas canvas)
        {
            foreach (var rpm in Object.FindObjectsOfType<ContextUIEngineRPMDisplay>())
            {
                var t = rpm != null ? rpm.GetComponentInChildren<TMP_Text>(true) : null;
                if (t != null && t.font != null) return t.font;
            }
            foreach (var t in canvas.rootCanvas.GetComponentsInChildren<TMP_Text>(true))
                if (t != null && t.font != null) return t.font;
            return null;
        }
    }
}
