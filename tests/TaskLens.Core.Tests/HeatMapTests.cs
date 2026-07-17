using TaskLens.Core.ViewModels;

namespace TaskLens.Core.Tests;

public class HeatMapTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(0.5)]
    public void CellArgb_NearZero_IsFullyTransparent(double percent) =>
        Assert.Equal(0u, HeatMap.CellArgb(percent));

    [Fact]
    public void CellArgb_HigherLoad_HasHigherAlpha()
    {
        var low = HeatMap.CellArgb(10) >> 24;
        var high = HeatMap.CellArgb(90) >> 24;
        Assert.True(high > low);
    }

    [Fact]
    public void CellArgb_ClampsAboveHundred() =>
        Assert.Equal(HeatMap.CellArgb(100), HeatMap.CellArgb(150));

    [Fact]
    public void CellArgb_ClampsBelowZero() =>
        Assert.Equal(HeatMap.CellArgb(0), HeatMap.CellArgb(-20));

    [Fact]
    public void DiskPercent_ScalesAndClamps()
    {
        Assert.Equal(0, HeatMap.DiskPercent(0, 0));
        Assert.Equal(50, HeatMap.DiskPercent(25.0 * 1024 * 1024, 0), 3);
        Assert.Equal(100, HeatMap.DiskPercent(500.0 * 1024 * 1024, 500.0 * 1024 * 1024));
    }

    [Fact]
    public void NetworkPercent_ScalesAndClamps()
    {
        Assert.Equal(0, HeatMap.NetworkPercent(0));
        Assert.Equal(50, HeatMap.NetworkPercent(50.0 * 1_000_000 / 8), 3); // 50 MBit/s of the 100 full scale
        Assert.Equal(100, HeatMap.NetworkPercent(1e9));
    }

    [Fact]
    public void CellArgb_FullLoad_IsOrange()
    {
        var argb = HeatMap.CellArgb(100);
        var r = (byte)(argb >> 16);
        var g = (byte)(argb >> 8);
        var b = (byte)argb;
        Assert.Equal(255, r);
        Assert.Equal(140, g);
        Assert.Equal(0, b);
    }
}
