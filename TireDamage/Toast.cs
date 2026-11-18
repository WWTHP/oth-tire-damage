using UnityEngine;

namespace TireDamage
{
    /// <summary>
    /// On-screen text (IMGUI): the latest message for a few seconds, and the repair progress bar
    /// while the repair key is held.
    /// </summary>
    internal static class Toast
    {
        private const float Duration = 4f;
        private static string _text;
        private static float _until;

        public static void Show(string text)
        {
            if (!Settings.ShowToasts.Value) return;
            _text = text;
            _until = Time.unscaledTime + Duration;
        }

        public static void Draw()
        {
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float w = Screen.width / scale, h = Screen.height / scale;
            const float BoxW = 420f;
            float x = (w - BoxW) / 2f, y = h * 0.72f;

            if (Tires.RepairText != null)
            {
                GUI.Box(new Rect(x, y, BoxW, 54f), "");
                GUI.Label(new Rect(x + 12f, y + 4f, BoxW - 24f, 24f), $"<b>{Tires.RepairText}</b>");
                if (Tires.RepairProgress > 0f)
                {
                    GUI.Box(new Rect(x + 12f, y + 30f, BoxW - 24f, 14f), "");
                    GUI.Box(new Rect(x + 12f, y + 30f, (BoxW - 24f) * Tires.RepairProgress, 14f), "");
                }
                y -= 40f;
            }
            if (_text != null && Time.unscaledTime < _until)
            {
                GUI.Box(new Rect(x, y, BoxW, 32f), "");
                GUI.Label(new Rect(x + 12f, y + 5f, BoxW - 24f, 24f), $"<b>{_text}</b>");
            }
            GUI.matrix = Matrix4x4.identity;
        }
    }
}
