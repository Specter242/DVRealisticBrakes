using UnityEngine;

namespace RealisticBrakes
{
    /// <summary>
    /// A short notice under the crosshair, and in the log: what a click on a
    /// control did. Drawn below Realistic Firing's own notices, so the two
    /// mods' never overlap.
    /// </summary>
    internal sealed class Notice : MonoBehaviour
    {
        private const float Offset = 96f;
        private const float Seconds = 4f;

        private static string text;
        private static float shownAt = float.NegativeInfinity;

        private GUIStyle style;

        /// <summary>Shows <paramref name="message"/> for a few seconds, and logs it.</summary>
        public static void Show(string message)
        {
            text = message;
            shownAt = Time.time;
            Main.Mod?.Logger.Log(message);
        }

        private void OnGUI()
        {
            if (text == null || Time.time - shownAt > Seconds)
                return;
            style = style ?? new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
            };
            var area = new Rect(0f, Screen.height / 2f + Offset, Screen.width, 30f);
            style.normal.textColor = Color.black;
            GUI.Label(new Rect(area.x + 1f, area.y + 1f, area.width, area.height), text, style);
            style.normal.textColor = new Color(1f, 0.85f, 0.5f);
            GUI.Label(area, text, style);
        }
    }
}
