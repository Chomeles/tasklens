using TaskLens.Core.Models;
using TaskLens.Core.Services;

namespace TaskLens.App.Services;

// ponytail: deterministic stub data so the shell launches before tasks 11-12 land the real
// Windows services. Deleted when the last real implementation replaces its registration.
internal sealed class StubSensorService : ISensorService
{
    private int tick;

    public SensorSnapshot Sample()
    {
        // Slow sine wobble so the task-09 sparklines visibly move on stub data.
        var wobble = (float)Math.Sin(++tick / 5.0);
        return new SensorSnapshot(
            [
                new SensorReading("Stub CPU", "Core (Tctl/Tdie)", SensorKind.Temperature, 48.5f + 3f * wobble),
                new SensorReading("Stub CPU", "Package Power", SensorKind.Power, 35.2f + 8f * wobble),
                new SensorReading("Stub GPU", "GPU Core", SensorKind.Temperature, 41.0f + 2f * wobble),
                new SensorReading("Stub GPU", "Fan", SensorKind.Fan, 1180f + 120f * wobble),
            ],
            SensorAvailability.Available);
    }
}

internal sealed class StubGpuProcessService : IGpuProcessService
{
    public IReadOnlyDictionary<int, double> SampleGpuPercentByPid() =>
        new Dictionary<int, double> { [103] = 12.5 };
}

internal sealed class StubSystemMetricsService : ISystemMetricsService
{
    public SystemMetrics Sample() => new(
        CpuTotalPercent: 17.3,
        MemoryUsedBytes: 9L * 1024 * 1024 * 1024,
        MemoryTotalBytes: 32L * 1024 * 1024 * 1024);
}
