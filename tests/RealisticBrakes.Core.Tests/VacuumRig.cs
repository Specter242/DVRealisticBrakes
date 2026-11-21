using RealisticBrakes.Core;

namespace RealisticBrakes.Core.Tests;

 internal sealed class VacuumRig
{
    public const double Frame = 1 / 60.0;

    public static readonly PipeGeometry Geometry = PipeGeometry.VacuumTrainPipe();

    public VacuumRig(int cars, double carLength, bool directAdmission = true, bool charged = true, VacuumEngineSpec? engine = null, bool bogie = false,
        PipeGeometry? geometry = null)
    {
        var pipeGeometry = geometry ?? Geometry;
        Engine = new VacuumEngine(engine ?? VacuumEngineSpec.Steam());
        var all = new List<CarBrake> { new(20, pipeGeometry, vacuumEngine: Engine) };
        for (var i = 0; i < cars; i++)
        {
            var spec = bogie ? VacuumCylinderSpec.Bogie() : VacuumCylinderSpec.Wagon();
            spec.DirectAdmission = directAdmission;
            all.Add(new CarBrake(carLength, pipeGeometry, new VacuumCylinder(spec)));
        }
        foreach (var car in all)
            car.ChargeTo(charged ? Vacuum.FromInches(21) : Air.Atmosphere);
        Cars = all;
        Line = new BrakeLine(all, pipeGeometry);
    }

    public VacuumEngine Engine { get; }

    public List<CarBrake> Cars { get; }

    public BrakeLine Line { get; }

    public CarBrake Rear => Cars[^1];

    public double Length => Cars.Sum(c => c.Length);

    public double Time { get; private set; }

    public static double Inches(Chamber c) => Vacuum.ToInches(c.Pressure);

    public void Run(double seconds) => RunUntil(() => false, seconds);

    /// <summary>Runs frame by frame until <paramref name="done"/> or <paramref name="limit"/> s; returns the time it took, or NaN.</summary>
    public double RunUntil(Func<bool> done, double limit)
    {
        var start = Time;
        while (Time - start < limit)
        {
            Line.Advance(Frame);
            Time += Frame;
            if (done())
                return Time - start;
        }
        return double.NaN;
    }
}
