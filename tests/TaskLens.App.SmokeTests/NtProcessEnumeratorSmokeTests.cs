using TaskLens.App.Services;

namespace TaskLens.App.SmokeTests;

/// <summary>
/// Windows-only smoke tests (run by the windows-latest CI workflow): exercise the real
/// <c>NtQuerySystemInformation</c> path and the <c>Process.GetProcesses()</c> fallback against
/// the live OS. The byte-level parsing logic itself is unit-tested on Linux in Core.Tests.
/// </summary>
public class NtProcessEnumeratorSmokeTests
{
    [Fact]
    public void NtPath_EnumeratesRealProcesses_WithPlausibleFields()
    {
        // A real ReadFile so our own IO read counter is provably non-zero.
        _ = File.ReadAllBytes(typeof(NtProcessEnumeratorSmokeTests).Assembly.Location);

        var samples = new NtProcessEnumerator().EnumerateNt();

        Assert.True(samples.Count > 10, $"expected a real process table, got {samples.Count} rows");
        Assert.Contains(samples, s => s.Pid == 4 && s.Name == "System");

        var me = Assert.Single(samples, s => s.Pid == Environment.ProcessId);
        Assert.EndsWith(".exe", me.Name, StringComparison.OrdinalIgnoreCase);
        Assert.True(me.WorkingSetBytes > 1024 * 1024, $"working set {me.WorkingSetBytes}");
        Assert.True(me.TotalCpuTime > TimeSpan.Zero);
        Assert.True(me.IoReadBytes > 0, "IO read counter should reflect the ReadAllBytes above");
        Assert.InRange(me.StartTimeUtc, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public void PublicEnumerate_UsesTheNtPath()
    {
        // NT image names keep the ".exe" suffix; the fallback's ProcessName does not.
        var me = Assert.Single(new NtProcessEnumerator().Enumerate(), s => s.Pid == Environment.ProcessId);

        Assert.EndsWith(".exe", me.Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Fallback_EnumeratesRealProcesses()
    {
        var samples = NtProcessEnumerator.EnumerateFallback();

        var me = Assert.Single(samples, s => s.Pid == Environment.ProcessId);
        Assert.False(string.IsNullOrEmpty(me.Name));
        Assert.True(me.WorkingSetBytes > 0);
        Assert.True(me.TotalCpuTime > TimeSpan.Zero);
    }
}
