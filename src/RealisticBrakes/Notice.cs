using UnityEngine;

namespace RealisticBrakes
{
 internal sealed class Notice : MonoBehaviour
    {
        private const float Offset = 96f;
        private const float Seconds = 4f;

        private static string text;
        private static float shownAt = float.NegativeInfinity;
        private static float shownFor = Seconds;

        private GUIStyle style;

        /// <summary>Shows <paramref name="message"/> for a few seconds, unless the brake controls' text is off, and logs it.</summary>
        public static void Show(string message)
        {
            Main.Mod?.Logger.Log(message);
            if (Main.Settings == null || Main.Settings.ShowValveText)
                Display(message);
        }

        /// <summary>Shows <paramref name="message"/> for <paramref name="seconds"/> s whatever the settings, and logs it as a warning: a real problem.</summary>
        public static void Warn(string message, float seconds = Seconds)
        {
            Main.Mod?.Logger.Warning(message);
            Display(message, seconds);
        }

        private static void Display(string message, float seconds = Seconds)
        {
            text = message;
            shownAt = Time.time;
            shownFor = seconds;
        }

        private void OnGUI()
        {
            if (text == null || Time.time - shownAt > shownFor)
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

    /// <summary>
    /// Says, once each time a game is loaded, that the mod stands aside: a
    /// patch didn't fit the game (an update changed what it patches), so the
    /// game's own brake runs. The mod manager still lists the mod as loaded,
    /// and without this a player saw nothing but the stock brake.
    /// </summary>
    internal sealed class StandAsideWarning : MonoBehaviour
    {
        internal const string Message = "Realistic Brakes isn't running: it doesn't fit this version of the game, so the game's own brake runs. Look for an update.";

        // Into the loaded game, past its first frames.
        private const float Delay = 8f;
        private const float Shown = 12f;

        private bool inGame;
        private float due = float.PositiveInfinity;

        private void Update()
        {
            var now = PlayerManager.PlayerTransform != null;
            if (now && !inGame)
                due = Time.time + Delay;
            inGame = now;
            if (!now || Time.time < due)
                return;
            due = float.PositiveInfinity;
            Notice.Warn(Message, Shown);
        }
    }
}
