using TaskLens.Core.ViewModels;

namespace TaskLens.Core.Tests;

public class ProcessFormatTests
{
    [Theory]
    [InlineData(0, "0,0")]
    [InlineData(12.54, "12,5")]
    [InlineData(100, "100,0")]
    public void Percent_FormatsGerman(double value, string expected) =>
        Assert.Equal(expected, ProcessFormat.Percent(value));

    [Theory]
    [InlineData(0, "0,0 B")]
    [InlineData(1023, "1023,0 B")]
    [InlineData(1024, "1,0 KB")]
    [InlineData(64L * 1024 * 1024, "64,0 MB")]
    [InlineData(3L * 1024 * 1024 * 1024 / 2, "1,5 GB")]
    [InlineData(2048L * 1024 * 1024 * 1024 * 1024, "2048,0 TB")]
    public void Bytes_ScalesBinaryUnits(long value, string expected) =>
        Assert.Equal(expected, ProcessFormat.Bytes(value));

    [Fact]
    public void Rate_AppendsPerSecond() =>
        Assert.Equal("1,5 KB/s", ProcessFormat.Rate(1536));

    [Theory]
    [InlineData(0, 0, "0,0 MB/s")]
    [InlineData(1024 * 1024, 512 * 1024, "1,5 MB/s")]
    [InlineData(300L * 1024 * 1024, 12L * 1024 * 1024, "312,0 MB/s")]
    public void DiskRate_SumsReadWrite_FixedMbUnit(double read, double write, string expected) =>
        Assert.Equal(expected, ProcessFormat.DiskRate(read, write));

    [Theory]
    [InlineData(0, "0:00:00")]
    [InlineData(59, "0:00:59")]
    [InlineData(3787, "1:03:07")]
    [InlineData(93784, "26:03:04")] // > 24 h: total hours, no day rollover
    public void CpuTime_FormatsHoursMinutesSeconds(double seconds, string expected) =>
        Assert.Equal(expected, ProcessFormat.CpuTime(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void CpuTime_TruncatesSubSeconds() =>
        Assert.Equal("0:00:01", ProcessFormat.CpuTime(TimeSpan.FromMilliseconds(1900)));

    [Fact]
    public void SensorCells_FormatWithUnit_DashWithoutReading()
    {
        Assert.Equal("54,0 °C", ProcessFormat.Temperature(54));
        Assert.Equal("45,2 W", ProcessFormat.Power(45.2f));
        Assert.Equal("1200 RPM", ProcessFormat.Fan(1200));
        Assert.Equal("87,4 %", ProcessFormat.Load(87.4f));
        Assert.Equal("—", ProcessFormat.Temperature(null));
        Assert.Equal("—", ProcessFormat.Power(null));
        Assert.Equal("—", ProcessFormat.Fan(null));
        Assert.Equal("—", ProcessFormat.Load(null));
    }

    [Theory]
    [InlineData("CPU %", "Cpu", ProcessColumn.Cpu, true, "CPU % ▼")]
    [InlineData("CPU %", "Cpu", ProcessColumn.Cpu, false, "CPU % ▲")]
    [InlineData("Name", "Name", ProcessColumn.Cpu, true, "Name")]
    public void Header_MarksActiveSortColumn(
        string title, string column, ProcessColumn active, bool descending, string expected) =>
        Assert.Equal(expected, ProcessFormat.Header(title, column, active, descending));

    [Fact]
    public void Header_UnknownColumnName_Throws() =>
        Assert.Throws<ArgumentException>(() => ProcessFormat.Header("X", "Nope", ProcessColumn.Cpu, true));
}
