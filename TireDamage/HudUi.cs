using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TireDamage
{
    /// <summary>uGUI helpers for our panels in the game's HUD canvas (tire screen, tire HUD).</summary>
    internal static class HudUi
    {
        public static readonly Color Good = new(0.78f, 0.94f, 0.78f, 1f);
        public static readonly Color Worn = new(1f, 0.82f, 0.45f, 1f);
        public static readonly Color Flat = new(1f, 0.38f, 0.32f, 1f);
        public static readonly Color Dim = new(1f, 1f, 1f, 0.55f);

        /// <summary>
        /// Clones the tilt dial's blurred glass (<c>Mask/BlurredBackground</c>) under
        /// <paramref name="parent"/> as a plain rectangle filling it. False if the dial has none.
        /// </summary>
        public static bool Glass(ContextUIInclineIndicator dial, RectTransform parent)
        {
            var src = dial.transform.Find("Mask/BlurredBackground");
            if (src == null) return false;
            var glass = Object.Instantiate(src.gameObject, parent, false);
            glass.name = "Glass";
            Stretch(glass.GetComponent<RectTransform>());
            var img = glass.GetComponent<Image>();
            if (img != null) { img.sprite = null; img.raycastTarget = false; }
            return true;
        }

        public static Image Panel(string name, Transform parent, Color color)
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

        public static void Stretch(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size, Color color,
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
        public static TMP_FontAsset FindFont(Canvas canvas)
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
