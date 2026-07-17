using TaskLens.Core.Models;
using TaskLens.Core.Services;

namespace TaskLens.Core.Tests;

internal sealed class ManualClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);

    public void Advance(TimeSpan by) => UtcNow += by;
}

/// <summary>Executes posted actions inline — deterministic stand-in for the UI dispatcher.</summary>
internal sealed class SyncDispatcher : IDispatcher
{
    public void Post(Action action) => action();
}

/// <summary>Queues posted actions until <see cref="RunAll"/> — verifies marshalling behavior.</summary>
internal sealed class QueueDispatcher : IDispatcher
{
    public List<Action> Pending { get; } = [];

    public void Post(Action action) => Pending.Add(action);

    public void RunAll()
    {
        foreach (var action in Pending)
        {
            action();
        }

        Pending.Clear();
    }
}

internal sealed class FakeProcessEnumerator : IProcessEnumerator
{
    public IReadOnlyList<ProcessSample> Samples { get; set; } = [];

    public IReadOnlyList<ProcessSample> Enumerate() => Samples;
}

internal sealed class FakeSensorService : ISensorService
{
    public SensorSnapshot Snapshot { get; set; } = new([], SensorAvailability.NoSensors);

    public SensorSnapshot Sample() => Snapshot;
}

internal sealed class FakeGpuProcessService : IGpuProcessService
{
    public Dictionary<int, double> GpuPercentByPid { get; } = [];

    public IReadOnlyDictionary<int, double> SampleGpuPercentByPid() => GpuPercentByPid;
}

internal sealed class FakeSystemMetricsService : ISystemMetricsService
{
    public SystemMetrics Metrics { get; set; } = new(0, 0, 0);

    public SystemMetrics Sample() => Metrics;
}

internal sealed class FakeServiceCatalog : IServiceCatalog
{
    public ServiceCatalogSnapshot Snapshot { get; set; } = new([], ServiceCatalogAvailability.Available);

    public int QueryCount { get; private set; }

    public ServiceCatalogSnapshot Query()
    {
        QueryCount++;
        return Snapshot;
    }
}

internal sealed class FakeStartupItemSource : IStartupItemSource
{
    public StartupSnapshot Snapshot { get; set; } = new([], CatalogAvailability.Available);

    public int QueryCount { get; private set; }

    public StartupSnapshot Query()
    {
        QueryCount++;
        return Snapshot;
    }
}

internal sealed class FakeUserSessionSource : IUserSessionSource
{
    public UserSessionSnapshot Snapshot { get; set; } = new([], CatalogAvailability.Available);

    public int QueryCount { get; private set; }

    public UserSessionSnapshot Query()
    {
        QueryCount++;
        return Snapshot;
    }
}

internal sealed class FakeProcessActionService : IProcessActionService
{
    public ActionResult Result { get; set; } = ActionResult.Ok;

    public List<(int Pid, bool EntireTree)> Calls { get; } = [];

    public ActionResult Terminate(int pid, bool entireTree)
    {
        Calls.Add((pid, entireTree));
        return Result;
    }

    public List<int> EfficiencyCalls { get; } = [];

    public List<(string Command, bool Elevated)> Launches { get; } = [];

    public ActionResult SetEfficiencyMode(int pid)
    {
        EfficiencyCalls.Add(pid);
        return Result;
    }

    public ActionResult Launch(string command, bool elevated)
    {
        Launches.Add((command, elevated));
        return Result;
    }
}

internal sealed class FakeStartupManager : IStartupManager
{
    public ActionResult Result { get; set; } = ActionResult.Ok;

    public List<(StartupItem Item, bool Enabled)> Calls { get; } = [];

    public ActionResult SetEnabled(StartupItem item, bool enabled)
    {
        Calls.Add((item, enabled));
        return Result;
    }
}
