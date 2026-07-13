using TaskLens.Core.Models;
using TaskLens.Core.Services;

namespace TaskLens.App.Services;

// ponytail: deterministic stub data so the shell launches before task 11 lands the real
// Windows service. Deleted when the last real implementation replaces its registration.
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

internal sealed class StubSystemMetricsService : ISystemMetricsService
{
    public SystemMetrics Sample() => new(
        CpuTotalPercent: 17.3,
        MemoryUsedBytes: 9L * 1024 * 1024 * 1024,
        MemoryTotalBytes: 32L * 1024 * 1024 * 1024);
}
