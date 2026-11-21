using System.Globalization;
using System.Text;
using DV.Simulation.Brake;
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
        private GUIContent cachedContent;
        private Vector2 cachedSize;
        private bool sizeDirty;
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
            if (cachedContent == null)
                return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 12, wordWrap = false, richText = false };
                sizeDirty = true;
            }
            if (sizeDirty)
            {
                cachedSize = style.CalcSize(cachedContent);
                sizeDirty = false;
            }
            GUI.Box(new Rect(10, 120, cachedSize.x + 8, cachedSize.y + 8), cachedContent, style);
        }

        private void Refresh()
        {
            text.Length = 0;
            var car = PlayerManager.Car;
            if (car == null || car.brakeSystem == null || car.brakeSystem.brakeset == null)
            {
                cachedContent = null;
                return;
            }
            if (!BrakeSets.TryGetLine(car.brakeSystem.brakeset, out var line))
            {
                cachedContent = null;
                return;
            }
            var set = car.brakeSystem.brakeset;
            if (BrakeSets.TryGet(car.brakeSystem, out var own) && own.IsVacuum)
            {
                RefreshVacuum(line, set, car);
                UpdateContent();
                return;
            }
            text.AppendLine($"Realistic Brakes: {line.Cars.Count} cars, gauge bar");
            text.AppendLine(" #  car            pipe   ctrl   aux    cyl    (ctrl: the American valve's emergency reservoir)");
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
                    text.Append($" MR {G(engine.MainReservoir.Pressure)}");
                    if (engine.DriverValve != null && engine.DriverValve.CutIn)
                        text.Append($" ER {G(engine.DriverValve.EqualizingReservoir.Pressure)} {engine.DriverValve.Position}");
                }
                AppendFlags(system, air);
                text.AppendLine();
            }
            UpdateContent();
        }

        private void UpdateContent()
        {
            if (text.Length == 0)
            {
                cachedContent = null;
                return;
            }
            if (cachedContent == null)
                cachedContent = new GUIContent(text.ToString());
            else
                cachedContent.text = text.ToString();
            sizeDirty = true;
        }

        private void RefreshVacuum(BrakeLine line, Brakeset set, TrainCar car)
        {
            text.AppendLine($"Realistic Brakes: {line.Cars.Count} cars, vacuum in inches");
            text.AppendLine(" #  car            pipe  chamb  under  force");
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
                var name = air.Car != null ? air.Car.ID : system.name;
                var mark = air.Car == car ? '>' : ' ';
                text.Append($"{mark}{i + 1,2} {Trim(name, 13),-13} {V(brake.PipeSection.Pressure),5} ");
                var cylinder = brake.VacuumCylinder;
                var engine = brake.VacuumEngine;
                if (cylinder != null)
                    text.Append($"{V(cylinder.Chamber.Pressure),6} {V(cylinder.Underside.Pressure),6} {cylinder.ForceShare,5:P0}{(cylinder.Load == LoadSetting.Empty ? " empty" : "")}");
                else if (engine != null)
                {
                    text.Append($"     -      - {engine.BrakeShare,5:P0} {engine.Position}{(engine.CutIn ? "" : " cut out")}{(engine.PumpOn ? "" : " pump off")}");
                    if (engine.Spec.SteamBrake)
                        text.Append($" steam {(engine.SteamBrakePressure / 6894.757).ToString("F0", CultureInfo.InvariantCulture)} psi");
                    else
                        text.Append($" MR {G(engine.MainReservoir.Pressure)}");
                }
                AppendFlags(system, air);
                text.AppendLine();
            }
        }

        private static string V(double pascals) => Vacuum.ToInches(pascals).ToString("F1", CultureInfo.InvariantCulture);

        private void AppendFlags(BrakeSystem system, CarAir air)
        {
            var brake = air.Brake;
            if (system.handbrakePosition > 0.01f)
                text.Append(" HB");
            if (brake.CutOut == true)
                text.Append(" CUT OUT");
            if (brake.BrakeValveCutOut == true)
                text.Append(" VALVE OUT");
            CheckCoupledEnds(air.Car, system, out var hoseOff, out var cockShut);
            if (hoseOff)
                text.Append(" HOSE OFF");
            if (cockShut)
                text.Append(" COCK SHUT");
        }

        private static void CheckCoupledEnds(TrainCar car, BrakeSystem system, out bool hoseOff, out bool cockShut)
        {
            hoseOff = false;
            cockShut = false;
            if (car != null)
            {
                CheckCouplerEnd(car.frontCoupler, ref hoseOff, ref cockShut);
                CheckCouplerEnd(car.rearCoupler, ref hoseOff, ref cockShut);
                return;
            }
            CheckConnectedHose(system?.Front, ref cockShut);
            CheckConnectedHose(system?.Rear, ref cockShut);
        }

        private static void CheckCouplerEnd(Coupler coupler, ref bool hoseOff, ref bool cockShut)
        {
            if (coupler == null || !coupler.IsCoupled())
                return;
            var hose = coupler.hoseAndCock;
            if (hose == null)
                return;
            var otherHose = coupler.coupledTo != null ? coupler.coupledTo.hoseAndCock : null;
            if (!hose.IsHoseConnected || (otherHose != null && !ReferenceEquals(hose.connectedTo, otherHose)))
                hoseOff = true;
            else if (!hose.cockOpen || !hose.connectedTo.cockOpen)
                cockShut = true;
        }

        private static void CheckConnectedHose(HoseAndCock hose, ref bool cockShut)
        {
            if (hose != null && hose.IsHoseConnected && (!hose.cockOpen || !hose.connectedTo.cockOpen))
                cockShut = true;
        }

        private static string G(double pascals) => Air.ToGaugeBar(pascals).ToString("F2", CultureInfo.InvariantCulture);

        private static string Trim(string s, int n) => string.IsNullOrEmpty(s) ? "" : s.Length <= n ? s : s.Substring(0, n);
    }
}
