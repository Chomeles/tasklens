using System.Collections.Specialized;
using TaskLens.Core.Models;
using TaskLens.Core.Services;
using TaskLens.Core.ViewModels;

namespace TaskLens.Core.Tests;

public class ProcessListViewModelTests
{
    private static readonly DateTime Start = new(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc);

    private readonly ManualClock clock = new();
    private readonly FakeProcessEnumerator processes = new();
    private readonly ProcessListViewModel vm = new();

    /// <summary>Engine wired to the VM through the SyncDispatcher — snapshots apply inline on Tick.</summary>
    private SamplingEngine CreateEngine()
    {
        var engine = new SamplingEngine(
            processes, new FakeSensorService(), new FakeGpuProcessService(), new FakeSystemMetricsService(),
            clock, new SyncDispatcher(), processorCount: 1);
        engine.SnapshotReady += vm.ApplySnapshot;
        return engine;
    }

    private static ProcessSample Proc(int pid, string name, double cpuSeconds = 0, long memory = 1024) => new(
        Pid: pid,
        Name: name,
        StartTimeUtc: Start,
        TotalCpuTime: TimeSpan.FromSeconds(cpuSeconds),
        WorkingSetBytes: memory,
        IoReadBytes: 0,
        IoWriteBytes: 0);

    private static ProcessDelta Delta(
        int pid, string name, double cpu = 0, double gpu = 0, long memory = 1024,
        double ioRead = 0, double ioWrite = 0, DateTime? start = null) => new(
            new ProcessSample(pid, name, start ?? Start, TimeSpan.Zero, memory, 0, 0),
            cpu, gpu, ioRead, ioWrite);

    private static SystemSnapshot Snap(params ProcessDelta[] deltas) => new(
        TimestampUtc: Start,
        Processes: deltas,
        Sensors: [],
        SensorAvailability: SensorAvailability.NoSensors,
        CpuTotalPercent: 0,
        MemoryUsedBytes: 0,
        MemoryTotalBytes: 0);

    [Fact]
    public void EngineTick_ViaSyncDispatcher_PopulatesRows()
    {
        processes.Samples = [Proc(1, "alpha"), Proc(2, "beta")];
        CreateEngine().Tick();

        Assert.Equal(2, vm.Rows.Count);
        Assert.Contains(vm.Rows, r => r is { Pid: 1, Name: "alpha" });
        Assert.Contains(vm.Rows, r => r is { Pid: 2, Name: "beta" });
    }

    [Fact]
    public void SecondTick_UpdatesRowsInPlace_NoCollectionChurn()
    {
        var engine = CreateEngine();
        processes.Samples = [Proc(1, "alpha"), Proc(2, "beta", memory: 2048)];
        engine.Tick();
        var rowsBefore = vm.Rows.ToList();

        var events = new List<NotifyCollectionChangedEventArgs>();
        vm.Rows.CollectionChanged += (_, e) => events.Add(e);
        clock.Advance(TimeSpan.FromSeconds(1));
        processes.Samples = [Proc(1, "alpha", cpuSeconds: 0.5), Proc(2, "beta", memory: 4096)];
        engine.Tick();

        Assert.Empty(events); // same set, same order: zero collection events
        Assert.Equal(rowsBefore, vm.Rows); // identical row instances, updated in place
        Assert.Equal(50, vm.Rows.Single(r => r.Pid == 1).CpuPercent);
        Assert.Equal(4096, vm.Rows.Single(r => r.Pid == 2).WorkingSetBytes);
    }

    [Fact]
    public void ExitedProcessRemoved_NewProcessAdded_SurvivorKeepsRowInstance()
    {
        vm.ApplySnapshot(Snap(Delta(1, "alpha"), Delta(2, "beta")));
        var survivor = vm.Rows.Single(r => r.Pid == 1);

        vm.ApplySnapshot(Snap(Delta(1, "alpha"), Delta(3, "gamma")));

        Assert.Equal(2, vm.Rows.Count);
        Assert.Same(survivor, vm.Rows.Single(r => r.Pid == 1));
        Assert.DoesNotContain(vm.Rows, r => r.Pid == 2);
        Assert.Contains(vm.Rows, r => r.Pid == 3);
    }

    [Fact]
    public void PidReuse_DifferentStartTime_GetsFreshRow()
    {
        vm.ApplySnapshot(Snap(Delta(1, "alpha")));
        var oldRow = vm.Rows.Single();

        vm.ApplySnapshot(Snap(Delta(1, "alpha", start: Start.AddMinutes(5))));

        Assert.NotSame(oldRow, vm.Rows.Single());
    }

    [Theory]
    [InlineData(ProcessColumn.Name, false, new[] { 2, 3, 1 })] // alpha, beta, gamma
    [InlineData(ProcessColumn.Pid, false, new[] { 1, 2, 3 })]
    [InlineData(ProcessColumn.Cpu, true, new[] { 3, 1, 2 })]
    [InlineData(ProcessColumn.Memory, true, new[] { 2, 3, 1 })]
    public void Sort_OrdersByColumnAndDirection(ProcessColumn column, bool descending, int[] expectedPids)
    {
        vm.ApplySnapshot(Snap(
            Delta(1, "gamma", cpu: 10, memory: 100),
            Delta(2, "alpha", cpu: 5, memory: 300),
            Delta(3, "beta", cpu: 20, memory: 200)));

        vm.SortColumn = column;
        vm.SortDescending = descending;

        Assert.Equal(expectedPids, vm.Rows.Select(r => r.Pid));
    }

    [Fact]
    public void Sort_IsStable_EqualKeysKeepSnapshotOrder()
    {
        vm.SortColumn = ProcessColumn.Cpu;
        vm.SortDescending = false;
        vm.ApplySnapshot(Snap(
            Delta(1, "a", cpu: 5),
            Delta(2, "b", cpu: 5),
            Delta(3, "c", cpu: 1),
            Delta(4, "d", cpu: 5)));

        Assert.Equal([3, 1, 2, 4], vm.Rows.Select(r => r.Pid));

        vm.SortDescending = true; // reversal must not disturb the 1,2,4 tie order
        Assert.Equal([1, 2, 4, 3], vm.Rows.Select(r => r.Pid));
    }

    [Fact]
    public void SortByCommand_TogglesDirectionOnSameColumn()
    {
        vm.SortByCommand.Execute(ProcessColumn.Memory);
        Assert.Equal(ProcessColumn.Memory, vm.SortColumn);
        Assert.True(vm.SortDescending);

        vm.SortByCommand.Execute(ProcessColumn.Memory);
        Assert.False(vm.SortDescending);

        vm.SortByCommand.Execute(ProcessColumn.Name);
        Assert.Equal(ProcessColumn.Name, vm.SortColumn);
        Assert.False(vm.SortDescending); // names default ascending
    }

    [Fact]
    public void Filter_CaseInsensitiveSubstring_AndRestoresRowsWhenCleared()
    {
        vm.ApplySnapshot(Snap(Delta(1, "Chrome"), Delta(2, "chrome-helper"), Delta(3, "code")));

        vm.Filter = "CHROME";
        Assert.Equal([1, 2], vm.Rows.Select(r => r.Pid).Order());

        vm.Filter = "";
        Assert.Equal(3, vm.Rows.Count);
    }

    [Fact]
    public void Filter_KeepsRowInstances_AcrossSnapshotsWhileHidden()
    {
        vm.ApplySnapshot(Snap(Delta(1, "alpha"), Delta(2, "beta")));
        var hidden = vm.Rows.Single(r => r.Pid == 2);

        vm.Filter = "alpha";
        vm.ApplySnapshot(Snap(Delta(1, "alpha"), Delta(2, "beta")));
        vm.Filter = "";

        Assert.Same(hidden, vm.Rows.Single(r => r.Pid == 2));
    }

    [Fact]
    public void Totals_AggregateVisibleRows_AndFollowFilter()
    {
        vm.ApplySnapshot(Snap(
            Delta(1, "alpha", cpu: 10, gpu: 1, memory: 100, ioRead: 5, ioWrite: 7),
            Delta(2, "beta", cpu: 20, gpu: 2, memory: 200, ioRead: 11, ioWrite: 13)));

        Assert.Equal(30, vm.Totals.CpuPercent);
        Assert.Equal(3, vm.Totals.GpuPercent);
        Assert.Equal(300, vm.Totals.WorkingSetBytes);
        Assert.Equal(16, vm.Totals.IoReadBytesPerSecond);
        Assert.Equal(20, vm.Totals.IoWriteBytesPerSecond);

        vm.Filter = "beta";
        Assert.Equal(20, vm.Totals.CpuPercent);
        Assert.Equal(200, vm.Totals.WorkingSetBytes);
    }

    [Fact]
    public void ResortAfterValueChange_MovesRowsWithoutRecreatingThem()
    {
        vm.SortColumn = ProcessColumn.Cpu;
        vm.SortDescending = true;
        vm.ApplySnapshot(Snap(Delta(1, "alpha", cpu: 10), Delta(2, "beta", cpu: 20)));
        var alpha = vm.Rows.Single(r => r.Pid == 1);
        Assert.Equal([2, 1], vm.Rows.Select(r => r.Pid));

        vm.ApplySnapshot(Snap(Delta(1, "alpha", cpu: 30), Delta(2, "beta", cpu: 20)));

        Assert.Equal([1, 2], vm.Rows.Select(r => r.Pid));
        Assert.Same(alpha, vm.Rows[0]);
    }
}
