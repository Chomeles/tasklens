using TaskLens.Core.Models;

namespace TaskLens.Core.Services;

/// <summary>
/// Per-tick sampling loop: composes all services into one immutable <see cref="SystemSnapshot"/>,
/// computes CPU%/IO-rate deltas keyed by <c>(Pid, StartTimeUtc)</c> (PID-reuse safe), and delivers
/// the snapshot with exactly one <see cref="IDispatcher.Post"/> per tick.
/// First tick — and the first sighting of any process — reports zero rates.
/// </summary>
public sealed class SamplingEngine
{
    private readonly IProcessEnumerator processEnumerator;
    private readonly ISensorService sensorService;
    private readonly IGpuProcessService gpuProcessService;
    private readonly ISystemMetricsService systemMetricsService;
    private readonly IClock clock;
    private readonly IDispatcher dispatcher;
    private readonly int processorCount;

    // Rebuilt each tick from live processes, so entries for exited processes are pruned
    // and a reused PID (different start time) never matches a stale sample.
    private Dictionary<(int Pid, DateTime StartTimeUtc), ProcessSample> previousSamples = [];
    private DateTime? previousTickUtc;
    private TimeSpan interval;

    public SamplingEngine(
        IProcessEnumerator processEnumerator,
        ISensorService sensorService,
        IGpuProcessService gpuProcessService,
        ISystemMetricsService systemMetricsService,
        IClock clock,
        IDispatcher dispatcher,
        TimeSpan? interval = null,
        int? processorCount = null)
    {
        this.processEnumerator = processEnumerator ?? throw new ArgumentNullException(nameof(processEnumerator));
        this.sensorService = sensorService ?? throw new ArgumentNullException(nameof(sensorService));
        this.gpuProcessService = gpuProcessService ?? throw new ArgumentNullException(nameof(gpuProcessService));
        this.systemMetricsService = systemMetricsService ?? throw new ArgumentNullException(nameof(systemMetricsService));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        Interval = interval ?? Settings.Default.RefreshInterval;
        this.processorCount = processorCount ?? Environment.ProcessorCount;
        if (this.processorCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(processorCount), processorCount, "processorCount must be >= 1.");
        }
    }

    /// <summary>Raised once per tick, on the dispatcher, with the composed snapshot.</summary>
    public event Action<SystemSnapshot>? SnapshotReady;

    /// <summary>Sampling interval. May be changed while running; applies from the next tick.</summary>
    public TimeSpan Interval
    {
        get => interval;
        set => interval = value > TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Interval must be positive.");
    }

    /// <summary>Runs the sampling loop until cancelled. Call from a background thread/task.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                Tick();
                timer.Period = Interval; // pick up runtime interval changes
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
    }

    /// <summary>One sampling tick. Public so tests drive it deterministically without timers.</summary>
    public void Tick()
    {
        var now = clock.UtcNow;
        var samples = processEnumerator.Enumerate();
        var gpuByPid = gpuProcessService.SampleGpuPercentByPid();
        var sensors = sensorService.Sample();
        var metrics = systemMetricsService.Sample();

        var wallSeconds = previousTickUtc is { } prev ? (now - prev).TotalSeconds : 0d;

        var deltas = new List<ProcessDelta>(samples.Count);
        var current = new Dictionary<(int Pid, DateTime StartTimeUtc), ProcessSample>(samples.Count);
        foreach (var sample in samples)
        {
            var key = (sample.Pid, sample.StartTimeUtc);
            double cpuPercent = 0, ioRead = 0, ioWrite = 0;
            if (wallSeconds > 0 && previousSamples.TryGetValue(key, out var last))
            {
                var cpuSeconds = (sample.TotalCpuTime - last.TotalCpuTime).TotalSeconds;
                cpuPercent = Math.Clamp(cpuSeconds / (wallSeconds * processorCount) * 100, 0, 100);
                ioRead = Math.Max(0, (sample.IoReadBytes - last.IoReadBytes) / wallSeconds);
                ioWrite = Math.Max(0, (sample.IoWriteBytes - last.IoWriteBytes) / wallSeconds);
            }

            gpuByPid.TryGetValue(sample.Pid, out var gpuPercent);
            deltas.Add(new ProcessDelta(sample, cpuPercent, Math.Clamp(gpuPercent, 0, 100), ioRead, ioWrite));
            current[key] = sample;
        }

        previousSamples = current;
        previousTickUtc = now;

        var snapshot = new SystemSnapshot(
            TimestampUtc: now,
            Processes: deltas,
            Sensors: sensors.Readings,
            SensorAvailability: sensors.Availability,
            CpuTotalPercent: metrics.CpuTotalPercent,
            MemoryUsedBytes: metrics.MemoryUsedBytes,
            MemoryTotalBytes: metrics.MemoryTotalBytes);

        dispatcher.Post(() => SnapshotReady?.Invoke(snapshot));
    }
}
