using UnityEngine;

namespace RealisticBrakes
{
 internal sealed class Notice : MonoBehaviour
    {
        private const float Offset = 96f;
        private const float Seconds = 4f;

        private static string text;
        private static float shownAt = float.NegativeInfinity;

        private GUIStyle style;

        /// <summary>Shows <paramref name="message"/> for a few seconds, unless the brake controls' text is off, and logs it.</summary>
        public static void Show(string message)
        {
            Main.Mod?.Logger.Log(message);
            if (Main.Settings == null || Main.Settings.ShowValveText)
                Display(message);
        }

        /// <summary>Shows <paramref name="message"/> for a few seconds whatever the settings, and logs it as a warning: a real problem.</summary>
        public static void Warn(string message)
        {
            Main.Mod?.Logger.Warning(message);
            Display(message);
        }

        private static void Display(string message)
        {
            text = message;
            shownAt = Time.time;
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
