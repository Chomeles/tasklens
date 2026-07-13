using TaskLens.Core.Models;
using TaskLens.Core.Services;

namespace TaskLens.App.Services;

// ponytail: deterministic stub data so the shell launches before tasks 10-12 land the real
// Windows services. Deleted when the last real implementation replaces its registration.
internal sealed class StubProcessEnumerator : IProcessEnumerator
{
    private static readonly DateTime StartTimeUtc = DateTime.UtcNow;
    private static readonly string[] Names = ["tasklens", "explorer", "dwm", "chrome", "code"];
    private int tick;

    public IReadOnlyList<ProcessSample> Enumerate()
    {
        tick++; // advancing CPU/IO counters so the deltas in task 03 produce non-zero rates
        var samples = new ProcessSample[Names.Length];
        for (var i = 0; i < Names.Length; i++)
        {
            samples[i] = new ProcessSample(
                Pid: 100 + i,
                Name: Names[i],
                StartTimeUtc: StartTimeUtc,
                TotalCpuTime: TimeSpan.FromMilliseconds(tick * 50 * (i + 1)),
                WorkingSetBytes: (i + 1) * 64L * 1024 * 1024,
                IoReadBytes: tick * 1024L * (i + 1),
                IoWriteBytes: tick * 512L * (i + 1));
        }

        return samples;
    }
}

internal sealed class StubSensorService : ISensorService
{
    public SensorSnapshot Sample() => new(
        [
            new SensorReading("Stub CPU", "Core (Tctl/Tdie)", SensorKind.Temperature, 48.5f),
            new SensorReading("Stub CPU", "Package Power", SensorKind.Power, 35.2f),
            new SensorReading("Stub GPU", "GPU Core", SensorKind.Temperature, 41.0f),
            new SensorReading("Stub GPU", "Fan", SensorKind.Fan, 1180f),
        ],
        SensorAvailability.Available);
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
