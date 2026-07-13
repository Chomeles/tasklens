using TaskLens.Core.Models;
using TaskLens.Core.ViewModels;

namespace TaskLens.Core.Tests;

public class DetailsViewModelTests
{
    private static SystemSnapshot Snapshot(DateTime now, IReadOnlyList<ProcessDelta> processes, double cpuTotal, long used, long total) =>
        new(now, processes, [], SensorAvailability.Available, cpuTotal, used, total);

    private static ProcessDelta Delta(int pid, DateTime startTimeUtc, double cpuPercent) =>
        new(new ProcessSample(pid, "app.exe", startTimeUtc, TimeSpan.Zero, 0, 0, 0), cpuPercent, 0, 0, 0);

    [Fact]
    public void ApplySnapshot_BeforeSelection_OnlyBuildsSystemHistory()
    {
        var vm = new DetailsViewModel();
        var now = DateTime.UtcNow;

        vm.ApplySnapshot(Snapshot(now, [], 25, 50, 200));

        Assert.Equal([25f], vm.SystemCpuHistory);
        Assert.Equal([25f], vm.SystemMemoryHistory);
        Assert.Empty(vm.ProcessCpuHistory);
        Assert.Equal("No process selected", vm.ProcessName);
    }

    [Fact]
    public void ApplySnapshot_MatchingProcess_AppendsItsCpuPercent()
    {
        var vm = new DetailsViewModel();
        var start = DateTime.UtcNow;
        vm.SelectProcess(42, start, "app.exe");

        vm.ApplySnapshot(Snapshot(DateTime.UtcNow, [Delta(42, start, 33)], 10, 0, 100));

        Assert.Equal([33f], vm.ProcessCpuHistory);
        Assert.Equal(33f, vm.ProcessCpuPercent);
        Assert.Equal("app.exe", vm.ProcessName);
    }

    [Fact]
    public void ApplySnapshot_ProcessAbsent_AppendsNullGap()
    {
        var vm = new DetailsViewModel();
        var start = DateTime.UtcNow;
        vm.SelectProcess(42, start, "app.exe");

        vm.ApplySnapshot(Snapshot(DateTime.UtcNow, [], 10, 0, 100));

        Assert.Equal([null], vm.ProcessCpuHistory);
        Assert.Null(vm.ProcessCpuPercent);
    }

    [Fact]
    public void ApplySnapshot_PidReusedByDifferentProcess_DoesNotMatchStaleStartTime()
    {
        var vm = new DetailsViewModel();
        var originalStart = DateTime.UtcNow;
        vm.SelectProcess(42, originalStart, "app.exe");

        // Same PID, different StartTimeUtc = a different process (PID reuse) - must not match.
        vm.ApplySnapshot(Snapshot(DateTime.UtcNow, [Delta(42, originalStart.AddSeconds(5), 90)], 10, 0, 100));

        Assert.Equal([null], vm.ProcessCpuHistory);
    }

    [Fact]
    public void SelectProcess_ResetsPreviousProcessHistory()
    {
        var vm = new DetailsViewModel();
        var start1 = DateTime.UtcNow;
        vm.SelectProcess(1, start1, "one.exe");
        vm.ApplySnapshot(Snapshot(DateTime.UtcNow, [Delta(1, start1, 50)], 10, 0, 100));
        Assert.Single(vm.ProcessCpuHistory);

        vm.SelectProcess(2, DateTime.UtcNow, "two.exe");

        Assert.Empty(vm.ProcessCpuHistory);
        Assert.Equal("two.exe", vm.ProcessName);
    }

    [Fact]
    public void ApplySnapshot_ZeroTotalMemory_ReportsNullPercent()
    {
        var vm = new DetailsViewModel();

        vm.ApplySnapshot(Snapshot(DateTime.UtcNow, [], 0, 0, 0));

        Assert.Equal([null], vm.SystemMemoryHistory);
        Assert.Null(vm.SystemMemoryPercent);
    }

    [Fact]
    public void ApplySnapshot_Null_Throws()
    {
        var vm = new DetailsViewModel();
        Assert.Throws<ArgumentNullException>(() => vm.ApplySnapshot(null!));
    }
}
