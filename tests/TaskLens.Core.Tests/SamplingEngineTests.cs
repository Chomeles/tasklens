using TaskLens.Core.Models;
using TaskLens.Core.Services;

namespace TaskLens.Core.Tests;

public class SamplingEngineTests
{
    private static readonly DateTime Start = new(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc);

    private readonly ManualClock clock = new();
    private readonly FakeProcessEnumerator processes = new();
    private readonly FakeSensorService sensors = new();
    private readonly FakeGpuProcessService gpu = new();
    private readonly FakeSystemMetricsService metrics = new();
    private readonly List<SystemSnapshot> snapshots = [];

    private SamplingEngine CreateEngine(IDispatcher? dispatcher = null, int processorCount = 4, int historyCapacity = 60)
    {
        var engine = new SamplingEngine(
            processes, sensors, gpu, metrics, clock,
            dispatcher ?? new SyncDispatcher(),
            processorCount: processorCount,
            historyCapacity: historyCapacity);
        engine.SnapshotReady += snapshots.Add;
        return engine;
    }

    private static ProcessSample Proc(
        int pid = 100,
        DateTime? startTimeUtc = null,
        double cpuSeconds = 0,
        long ioRead = 0,
        long ioWrite = 0,
        string name = "worker") => new(
            Pid: pid,
            Name: name,
            StartTimeUtc: startTimeUtc ?? Start,
            TotalCpuTime: TimeSpan.FromSeconds(cpuSeconds),
            WorkingSetBytes: 1024,
            IoReadBytes: ioRead,
            IoWriteBytes: ioWrite);

    [Fact]
    public void FirstTick_AllRatesAreZero()
    {
        processes.Samples = [Proc(cpuSeconds: 123, ioRead: 5_000, ioWrite: 7_000)];
        CreateEngine().Tick();

        var delta = Assert.Single(Assert.Single(snapshots).Processes);
        Assert.Equal(0, delta.CpuPercent);
        Assert.Equal(0, delta.IoReadBytesPerSecond);
        Assert.Equal(0, delta.IoWriteBytesPerSecond);
    }

    [Fact]
    public void SecondTick_ComputesCpuPercentAndIoRates()
    {
        var engine = CreateEngine(processorCount: 4);
        processes.Samples = [Proc(cpuSeconds: 10, ioRead: 1_000, ioWrite: 500)];
        engine.Tick();

        clock.Advance(TimeSpan.FromSeconds(1));
        processes.Samples = [Proc(cpuSeconds: 12, ioRead: 3_000, ioWrite: 1_500)];
        engine.Tick();

        // 2 CPU-seconds over 1 wall-second on 4 cores = 50%.
        var delta = Assert.Single(snapshots[1].Processes);
        Assert.Equal(50, delta.CpuPercent, precision: 10);
        Assert.Equal(2_000, delta.IoReadBytesPerSecond, precision: 10);
        Assert.Equal(1_000, delta.IoWriteBytesPerSecond, precision: 10);
    }

    [Fact]
    public void DeltaMath_ScalesWithWallClockInterval()
    {
        var engine = CreateEngine(processorCount: 4);
        processes.Samples = [Proc(cpuSeconds: 0, ioRead: 0)];
        engine.Tick();

        clock.Advance(TimeSpan.FromSeconds(2));
        processes.Samples = [Proc(cpuSeconds: 2, ioRead: 4_000)];
        engine.Tick();

        // 2 CPU-seconds over 2 wall-seconds on 4 cores = 25%; 4000 B over 2 s = 2000 B/s.
        var delta = Assert.Single(snapshots[1].Processes);
        Assert.Equal(25, delta.CpuPercent, precision: 10);
        Assert.Equal(2_000, delta.IoReadBytesPerSecond, precision: 10);
    }

    [Fact]
    public void PidReuse_DifferentStartTime_IsANewProcessWithZeroRates()
    {
        var engine = CreateEngine();
        processes.Samples = [Proc(pid: 100, startTimeUtc: Start, cpuSeconds: 500)];
        engine.Tick();

        clock.Advance(TimeSpan.FromSeconds(1));
        // Same PID, later start time, *lower* CPU time — a naive pid-keyed delta
        // would produce a negative or garbage spike.
        processes.Samples = [Proc(pid: 100, startTimeUtc: Start.AddMinutes(5), cpuSeconds: 1)];
        engine.Tick();

        var delta = Assert.Single(snapshots[1].Processes);
        Assert.Equal(0, delta.CpuPercent);
        Assert.Equal(0, delta.IoReadBytesPerSecond);
    }

    [Fact]
    public void ProcessThatDisappears_IsPruned_SoAReappearanceStartsAtZero()
    {
        var engine = CreateEngine();
        processes.Samples = [Proc(cpuSeconds: 1)];
        engine.Tick();

        clock.Advance(TimeSpan.FromSeconds(1));
        processes.Samples = [];
        engine.Tick();

        clock.Advance(TimeSpan.FromSeconds(1));
        processes.Samples = [Proc(cpuSeconds: 3)];
        engine.Tick();

        Assert.Empty(snapshots[1].Processes);
        Assert.Equal(0, Assert.Single(snapshots[2].Processes).CpuPercent);
    }

    [Fact]
    public void NewProcessAppearingMidRun_HasZeroRatesOnFirstSighting()
    {
        var engine = CreateEngine();
        processes.Samples = [Proc(pid: 1, cpuSeconds: 1)];
        engine.Tick();

        clock.Advance(TimeSpan.FromSeconds(1));
        processes.Samples = [Proc(pid: 1, cpuSeconds: 2), Proc(pid: 2, cpuSeconds: 99)];
        engine.Tick();

        var byPid = snapshots[1].Processes.ToDictionary(d => d.Sample.Pid);
        Assert.True(byPid[1].CpuPercent > 0);
        Assert.Equal(0, byPid[2].CpuPercent);
    }

    [Fact]
    public void CpuPercent_IsClampedTo100_AndNegativeDeltasToZero()
    {
        var engine = CreateEngine(processorCount: 1);
        processes.Samples = [Proc(pid: 1, cpuSeconds: 10), Proc(pid: 2, cpuSeconds: 10)];
        engine.Tick();

        clock.Advance(TimeSpan.FromSeconds(1));
        processes.Samples = [Proc(pid: 1, cpuSeconds: 20), Proc(pid: 2, cpuSeconds: 9)];
        engine.Tick();

        var byPid = snapshots[1].Processes.ToDictionary(d => d.Sample.Pid);
        Assert.Equal(100, byPid[1].CpuPercent); // 10 CPU-s over 1 wall-s on 1 core, clamped
        Assert.Equal(0, byPid[2].CpuPercent);   // CPU time went backwards
    }

    [Fact]
    public void ZeroWallClockDelta_YieldsZeroRatesInsteadOfDividingByZero()
    {
        var engine = CreateEngine();
        processes.Samples = [Proc(cpuSeconds: 1)];
        engine.Tick();
        processes.Samples = [Proc(cpuSeconds: 2)];
        engine.Tick(); // clock not advanced

        Assert.Equal(0, Assert.Single(snapshots[1].Processes).CpuPercent);
    }

    [Fact]
    public void GpuPercent_IsMappedByPid_MissingPidsAreZero()
    {
        processes.Samples = [Proc(pid: 1), Proc(pid: 2)];
        gpu.GpuPercentByPid[1] = 33.5;
        CreateEngine().Tick();

        var byPid = snapshots[0].Processes.ToDictionary(d => d.Sample.Pid);
        Assert.Equal(33.5, byPid[1].GpuPercent);
        Assert.Equal(0, byPid[2].GpuPercent);
    }

    [Fact]
    public void Snapshot_ComposesSensorsMetricsAndTimestamp()
    {
        var reading = new SensorReading("CPU", "Package", SensorKind.Power, 42f);
        sensors.Snapshot = new SensorSnapshot([reading], SensorAvailability.NoAdmin);
        metrics.Metrics = new SystemMetrics(CpuTotalPercent: 37.5, MemoryUsedBytes: 512, MemoryTotalBytes: 1024);
        CreateEngine().Tick();

        var s = Assert.Single(snapshots);
        Assert.Equal(clock.UtcNow, s.TimestampUtc);
        Assert.Equal([reading], s.Sensors);
        Assert.Equal(SensorAvailability.NoAdmin, s.SensorAvailability);
        Assert.Equal(37.5, s.CpuTotalPercent);
        Assert.Equal(512, s.MemoryUsedBytes);
        Assert.Equal(1024, s.MemoryTotalBytes);
    }

    [Fact]
    public void Tick_DeliversExactlyOnePostPerTick_ViaDispatcher()
    {
        var dispatcher = new QueueDispatcher();
        var engine = CreateEngine(dispatcher);

        engine.Tick();
        Assert.Empty(snapshots); // not raised until the dispatcher runs
        Assert.Single(dispatcher.Pending);

        dispatcher.RunAll();
        Assert.Single(snapshots);
    }

    [Fact]
    public void Interval_DefaultsToOneSecond_AndRejectsNonPositiveValues()
    {
        var engine = CreateEngine();
        Assert.Equal(TimeSpan.FromSeconds(1), engine.Interval);

        engine.Interval = TimeSpan.FromMilliseconds(250);
        Assert.Equal(TimeSpan.FromMilliseconds(250), engine.Interval);

        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Interval = TimeSpan.Zero);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Interval = TimeSpan.FromSeconds(-1));
    }

    [Fact]
    public void ProcessorCount_MustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateEngine(processorCount: 0));
    }

    [Fact]
    public void HistoryCapacity_MustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateEngine(historyCapacity: 0));
    }

    [Fact]
    public void ProcessCpuHistory_AccruesOnePointPerTick_OldestFirst()
    {
        var engine = CreateEngine(processorCount: 4);
        processes.Samples = [Proc(cpuSeconds: 10)];
        engine.Tick();

        clock.Advance(TimeSpan.FromSeconds(1));
        processes.Samples = [Proc(cpuSeconds: 12)];
        engine.Tick();

        Assert.Equal([0d, 50d], engine.GetProcessCpuHistory(100, Start));
    }

    [Fact]
    public void ProcessCpuHistory_IsCappedAtHistoryCapacity()
    {
        var engine = CreateEngine(processorCount: 4, historyCapacity: 2);
        for (var tick = 0; tick <= 3; tick++)
        {
            processes.Samples = [Proc(cpuSeconds: tick)]; // 1 CPU-s per 1 wall-s on 4 cores = 25%
            engine.Tick();
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        Assert.Equal([25d, 25d], engine.GetProcessCpuHistory(100, Start));
    }

    [Fact]
    public void ProcessCpuHistory_IsPrunedWhenTheProcessExits()
    {
        var engine = CreateEngine();
        processes.Samples = [Proc()];
        engine.Tick();
        Assert.Single(engine.GetProcessCpuHistory(100, Start));

        clock.Advance(TimeSpan.FromSeconds(1));
        processes.Samples = [];
        engine.Tick();

        Assert.Empty(engine.GetProcessCpuHistory(100, Start));
    }

    [Fact]
    public void PidReuse_DifferentStartTime_GetsAFreshHistory()
    {
        var engine = CreateEngine();
        processes.Samples = [Proc(pid: 100, startTimeUtc: Start)];
        engine.Tick();
        clock.Advance(TimeSpan.FromSeconds(1));
        engine.Tick();

        var reusedStart = Start.AddMinutes(5);
        clock.Advance(TimeSpan.FromSeconds(1));
        processes.Samples = [Proc(pid: 100, startTimeUtc: reusedStart)];
        engine.Tick();

        Assert.Empty(engine.GetProcessCpuHistory(100, Start));
        Assert.Single(engine.GetProcessCpuHistory(100, reusedStart));
    }

    [Fact]
    public void SensorHistory_AccruesPerTick_IncludingNullReadings()
    {
        var engine = CreateEngine();
        sensors.Snapshot = new SensorSnapshot(
            [new SensorReading("CPU", "Package", SensorKind.Temperature, 50f)],
            SensorAvailability.Available);
        engine.Tick();

        clock.Advance(TimeSpan.FromSeconds(1));
        sensors.Snapshot = new SensorSnapshot(
            [new SensorReading("CPU", "Package", SensorKind.Temperature, null)],
            SensorAvailability.Available);
        engine.Tick();

        Assert.Equal([50f, null], engine.GetSensorHistory("CPU", "Package"));
    }

    [Fact]
    public void SensorHistory_UnknownKeyIsEmpty_AndVanishedSensorsArePruned()
    {
        var engine = CreateEngine();
        Assert.Empty(engine.GetSensorHistory("CPU", "Package"));

        sensors.Snapshot = new SensorSnapshot(
            [new SensorReading("CPU", "Package", SensorKind.Temperature, 50f)],
            SensorAvailability.Available);
        engine.Tick();

        clock.Advance(TimeSpan.FromSeconds(1));
        sensors.Snapshot = new SensorSnapshot([], SensorAvailability.NoSensors);
        engine.Tick();

        Assert.Empty(engine.GetSensorHistory("CPU", "Package"));
    }

    [Fact]
    public async Task RunAsync_TicksOnTheConfiguredInterval_AndStopsOnCancellation()
    {
        var engine = new SamplingEngine(
            processes, sensors, gpu, metrics, clock, new SyncDispatcher(),
            interval: TimeSpan.FromMilliseconds(1), processorCount: 1);
        var first = new TaskCompletionSource();
        engine.SnapshotReady += _ => first.TrySetResult();

        using var cts = new CancellationTokenSource();
        var run = engine.RunAsync(cts.Token);

        await first.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();
        await run.WaitAsync(TimeSpan.FromSeconds(5)); // completes without throwing
    }
}
