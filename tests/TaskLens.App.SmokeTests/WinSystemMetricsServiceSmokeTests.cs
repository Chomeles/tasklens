using TaskLens.App.Services;

namespace TaskLens.App.SmokeTests;

/// <summary>
/// Windows-only smoke test for the real kernel32 metrics path: any machine (including a CI VM)
/// has physical RAM and monotonic system times, so real values are asserted here, not just shape.
/// </summary>
public class WinSystemMetricsServiceSmokeTests
{
    [Fact]
    public void Sample_Twice_YieldsPlausibleTotals()
    {
        var service = new WinSystemMetricsService();

        var first = service.Sample();
        Thread.Sleep(200);
        var second = service.Sample();

        Assert.Equal(0.0, first.CpuTotalPercent); // unprimed first sample reports 0 %
        Assert.InRange(second.CpuTotalPercent, 0.0, 100.0);
        Assert.True(second.MemoryTotalBytes > 0, "no physical memory reported");
        Assert.InRange(second.MemoryUsedBytes, 1, second.MemoryTotalBytes);
    }
}
