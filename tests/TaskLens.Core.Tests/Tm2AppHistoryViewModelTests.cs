using TaskLens.Core.Models;
using TaskLens.Core.ViewModels;

namespace TaskLens.Core.Tests;

public class Tm2AppHistoryViewModelTests
{
    private static readonly DateTime Start = new(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc);

    private readonly Tm2AppHistoryViewModel vm = new();

    private static ProcessDelta Proc(
        int pid, string name, double cpuSeconds = 0, long read = 0, long write = 0, DateTime? start = null) => new(
        new ProcessSample(pid, name, start ?? Start, TimeSpan.FromSeconds(cpuSeconds), 1024, read, write), 0, 0, 0, 0);

    private static SystemSnapshot Snap(params ProcessDelta[] deltas) => new(
        TimestampUtc: Start,
        Processes: deltas,
        Sensors: [],
        SensorAvailability: SensorAvailability.Available,
        CpuTotalPercent: 0,
        MemoryUsedBytes: 0,
        MemoryTotalBytes: 0);

    [Fact]
    public void FirstSnapshot_BaselinesExistingProcesses_ToZero()
    {
        // "seit dem Start von Taskmanager2": pre-existing counters must not count.
        vm.ApplySnapshot(Snap(Proc(1, "chrome", cpuSeconds: 600, read: 5000, write: 3000)));

        var row = vm.Rows.Single();
        Assert.Equal("chrome", row.Name);
        Assert.Equal(TimeSpan.Zero, row.CpuTime);
        Assert.Equal(0, row.IoReadBytes);
        Assert.Equal(0, row.IoWriteBytes);
    }

    [Fact]
    public void CounterGrowth_ShowsDeltaSinceAppStart()
    {
        vm.ApplySnapshot(Snap(Proc(1, "chrome", cpuSeconds: 600, read: 5000, write: 3000)));
        vm.ApplySnapshot(Snap(Proc(1, "chrome", cpuSeconds: 605, read: 7000, write: 4000)));

        var row = vm.Rows.Single();
        Assert.Equal(TimeSpan.FromSeconds(5), row.CpuTime);
        Assert.Equal(2000, row.IoReadBytes);
        Assert.Equal(1000, row.IoWriteBytes);
    }

    [Fact]
    public void MultiplePids_SameName_SumIntoOneRow()
    {
        vm.ApplySnapshot(Snap(Proc(1, "chrome", 600), Proc(2, "chrome", 100), Proc(3, "code", 50)));
        vm.ApplySnapshot(Snap(Proc(1, "chrome", 610), Proc(2, "chrome", 103), Proc(3, "code", 51)));

        Assert.Equal(2, vm.Rows.Count);
        Assert.Equal(TimeSpan.FromSeconds(13), vm.Rows.Single(r => r.Name == "chrome").CpuTime);
        Assert.Equal(TimeSpan.FromSeconds(1), vm.Rows.Single(r => r.Name == "code").CpuTime);
    }

    [Fact]
    public void LateArrival_CountsItsFullCumulativeCounters()
    {
        vm.ApplySnapshot(Snap(Proc(1, "chrome", 600)));
        // New process after app start: its whole counter lies inside the window (baseline zero).
        vm.ApplySnapshot(Snap(Proc(1, "chrome", 600), Proc(2, "burst", cpuSeconds: 3, read: 100)));

        var burst = vm.Rows.Single(r => r.Name == "burst");
        Assert.Equal(TimeSpan.FromSeconds(3), burst.CpuTime);
        Assert.Equal(100, burst.IoReadBytes);
    }

    [Fact]
    public void ProcessExit_ContributionRemains()
    {
        vm.ApplySnapshot(Snap(Proc(1, "chrome", cpuSeconds: 600, read: 1000), Proc(2, "code", 50)));
        vm.ApplySnapshot(Snap(Proc(1, "chrome", cpuSeconds: 605, read: 3000), Proc(2, "code", 51)));
        vm.ApplySnapshot(Snap(Proc(2, "code", 52))); // chrome exited
        vm.ApplySnapshot(Snap(Proc(2, "code", 53))); // and stays gone

        var chrome = vm.Rows.Single(r => r.Name == "chrome");
        Assert.Equal(TimeSpan.FromSeconds(5), chrome.CpuTime);
        Assert.Equal(2000, chrome.IoReadBytes);
    }

    [Fact]
    public void PidReuse_NewStartTime_AddsFreshOnTopOfRetired()
    {
        vm.ApplySnapshot(Snap(Proc(1, "worker", cpuSeconds: 100)));
        vm.ApplySnapshot(Snap(Proc(1, "worker", cpuSeconds: 105)));
        // Same PID, new StartTimeUtc: old identity retires (5 s), new one counts fresh (2 s).
        vm.ApplySnapshot(Snap(Proc(1, "worker", cpuSeconds: 2, start: Start.AddMinutes(5))));

        var row = vm.Rows.Single();
        Assert.Equal(TimeSpan.FromSeconds(7), row.CpuTime);
    }

    [Fact]
    public void Rows_SortedByCpuTimeDescending_TieByName()
    {
        vm.ApplySnapshot(Snap(Proc(1, "gamma"), Proc(2, "beta"), Proc(3, "alpha")));
        vm.ApplySnapshot(Snap(Proc(1, "gamma", 2), Proc(2, "beta", 5), Proc(3, "alpha", 2)));

        Assert.Equal(["beta", "alpha", "gamma"], vm.Rows.Select(r => r.Name));
    }

    [Fact]
    public void ApplySnapshot_SameRowObject_AcrossTicks()
    {
        vm.ApplySnapshot(Snap(Proc(1, "chrome", 600)));
        var row1 = vm.Rows.Single();

        vm.ApplySnapshot(Snap(Proc(1, "chrome", 605)));

        Assert.Same(row1, vm.Rows.Single());
    }
}
