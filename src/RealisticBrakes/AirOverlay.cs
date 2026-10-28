using System.Text;
using RealisticBrakes.Core;
using RealisticBrakes.Sim;
using UnityEngine;

namespace RealisticBrakes
{
    /// <summary>
    /// A plain window listing each car's brake pressures, gauge bar, for the
    /// train the player is on: for testing the model in game. Shown by the
    /// mod's setting.
    /// </summary>
    internal sealed class AirOverlay : MonoBehaviour
    {
        private const int MaxRows = 24;

        private readonly StringBuilder text = new StringBuilder();
        private GUIStyle style;
        private float nextRefresh;

        private void OnGUI()
        {
            if (!Main.Active || !Main.Settings.ShowOverlay)
                return;
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + 0.2f;
                Refresh();
            }
            if (text.Length == 0)
                return;
            if (style == null)
                style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 12, wordWrap = false, richText = false };
            var content = new GUIContent(text.ToString());
            var size = style.CalcSize(content);
            GUI.Box(new Rect(10, 120, size.x + 8, size.y + 8), content, style);
        }

        private void Refresh()
        {
            text.Length = 0;
            var car = PlayerManager.Car;
            if (car == null || car.brakeSystem == null || car.brakeSystem.brakeset == null)
                return;
            if (!BrakeSets.TryGetLine(car.brakeSystem.brakeset, out var line))
                return;
            text.AppendLine($"Realistic Brakes: {line.Cars.Count} cars, gauge bar");
            text.AppendLine(" #  car            pipe   ctrl   aux    cyl    (ctrl: the American valve's emergency reservoir)");
            var set = car.brakeSystem.brakeset;
            var count = set.cars.Count;
            for (var i = 0; i < count; i++)
            {
                if (count > MaxRows && i == MaxRows / 2)
                {
                    text.AppendLine($"   ... {count - MaxRows} more ...");
                    i = count - MaxRows / 2;
                }
                var system = set.cars[i];
                if (!BrakeSets.TryGet(system, out var air))
                    continue;
                var brake = air.Brake;
                var distributor = brake.Distributor ?? brake.Locomotive?.Distributor;
                var american = brake.ControlValve;
                var name = air.Car != null ? air.Car.ID : system.name;
                var mark = air.Car == car ? '>' : ' ';
                text.Append($"{mark}{i + 1,2} {Trim(name, 13),-13} {G(brake.PipeSection.Pressure),6} ");
                if (distributor != null)
                    text.Append($"{G(distributor.ControlReservoir.Pressure),6} {G(distributor.AuxiliaryReservoir.Pressure),6} ");
                else if (american != null)
                    text.Append($"{G(american.EmergencyReservoir.Pressure),6} {G(american.AuxiliaryReservoir.Pressure),6} ");
                else
                    text.Append("     -      - ");
                text.Append(brake.Cylinder != null ? $"{G(brake.Cylinder.Pressure),5}" : "    -");
                if (brake.Distributor != null)
                    text.Append($" {(brake.Distributor.Mode == BrakingMode.Goods ? 'G' : 'P')}{(brake.Distributor.Load == LoadSetting.Empty ? " empty" : "")}{(brake.Distributor.Applied ? " on" : "")}");
                if (american != null)
                    text.Append($" AB{(american.Load == LoadSetting.Empty ? " empty" : "")}{(american.Retainer != Retainer.Exhaust ? " " + american.Retainer : "")}{(american.Emergency ? " EMERGENCY" : american.Applied ? " on" : "")}");
                if (brake.Locomotive != null)
                {
                    var engine = brake.Locomotive;
                    text.Append($" MR {G(engine.MainReservoir.Pressure):F2}");
                    if (engine.DriverValve != null && engine.DriverValve.CutIn)
                        text.Append($" ER {G(engine.DriverValve.EqualizingReservoir.Pressure):F2} {engine.DriverValve.Position}");
                }
                text.AppendLine();
            }
        }

        private static string G(double pascals) => Air.ToGaugeBar(pascals).ToString("F2");

        private static string Trim(string s, int n) => string.IsNullOrEmpty(s) ? "" : s.Length <= n ? s : s.Substring(0, n);
    }
}
