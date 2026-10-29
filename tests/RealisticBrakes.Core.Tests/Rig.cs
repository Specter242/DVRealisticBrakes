using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

/// <summary>A test train: one engine with a driver's brake valve and a rake of UIC cars behind it.</summary>
internal sealed class Rig
{
    public const double Frame = 1 / 60.0;

    /// <summary>A DE6-sized compressor, bar·L/s of free air (the game's figure).</summary>
    public const double CompressorRate = 100;

    public static readonly PipeGeometry Geometry = PipeGeometry.Uic();

    public Rig(int cars, BrakingMode mode = BrakingMode.Goods, bool charged = true, double carLength = 15, bool american = false,
        LocomotiveBrakeSpec? engine = null, DriverBrakeValveSpec? driver = null)
    {
        engine ??= new LocomotiveBrakeSpec();
        engine.MainReservoirVolume = 2.0;
        Engine = new LocomotiveBrake(engine, new DriverBrakeValve(driver));
        var all = new List<CarBrake> { new(20, Geometry, locomotive: Engine) };
        for (var i = 0; i < cars; i++)
            all.Add(new CarBrake(carLength, Geometry, american ? new AarControlValve() : new UicDistributor { Mode = mode }));
        foreach (var car in all)
            car.ChargeTo(Air.FromGaugeBar(charged ? 5 : 0));
        Engine.MainReservoir.Pressure = Air.FromAbsoluteBar(9);
        Cars = all;
        Line = new BrakeLine(all, Geometry);
    }

    public LocomotiveBrake Engine { get; }

    public DriverBrakeValve Valve => Engine.DriverValve!;

    public List<CarBrake> Cars { get; }

    public BrakeLine Line { get; }

    public CarBrake Front => Cars[1];

    public CarBrake Rear => Cars[^1];

    public double Length => Cars.Sum(c => c.Length);

    public double Time { get; private set; }

    /// <summary>The game's governor: the pump runs under 8.3 bar and stops at 9 (absolute).</summary>
    private bool pumping = true;

    public void Run(double seconds) => RunUntil(() => false, seconds);

    /// <summary>Runs frame by frame until <paramref name="done"/> or <paramref name="limit"/> s; returns the time it took, or NaN.</summary>
    public double RunUntil(Func<bool> done, double limit)
    {
        var start = Time;
        while (Time - start < limit)
        {
            var mr = Air.ToAbsoluteBar(Engine.MainReservoir.Pressure);
            if (mr < 8.3)
                pumping = true;
            else if (mr >= 9)
                pumping = false;
            Engine.CompressorFlow = pumping ? FreeAir(CompressorRate) : 0;
            Line.Advance(Frame);
            Time += Frame;
            if (done())
                return Time - start;
        }
        return double.NaN;
    }

    /// <summary>kg/s of air from the game's bar·L/s of free air.</summary>
    public static double FreeAir(double barLitersPerSecond) => barLitersPerSecond * Air.Bar * 1e-3 / Air.RT;

    public void Service(double reductionBar)
    {
        Valve.Position = DriverValvePosition.Service;
        Valve.ServiceReduction = reductionBar * Air.Bar;
    }
}
