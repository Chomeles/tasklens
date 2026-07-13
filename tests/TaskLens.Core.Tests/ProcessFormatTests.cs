using TaskLens.Core.ViewModels;

namespace TaskLens.Core.Tests;

public class ProcessFormatTests
{
    [Theory]
    [InlineData(0, "0.0")]
    [InlineData(12.54, "12.5")]
    [InlineData(100, "100.0")]
    public void Percent_FormatsInvariant(double value, string expected) =>
        Assert.Equal(expected, ProcessFormat.Percent(value));

    [Theory]
    [InlineData(0, "0.0 B")]
    [InlineData(1023, "1023.0 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(64L * 1024 * 1024, "64.0 MB")]
    [InlineData(3L * 1024 * 1024 * 1024 / 2, "1.5 GB")]
    [InlineData(2048L * 1024 * 1024 * 1024 * 1024, "2048.0 TB")]
    public void Bytes_ScalesBinaryUnits(long value, string expected) =>
        Assert.Equal(expected, ProcessFormat.Bytes(value));

    [Fact]
    public void Rate_AppendsPerSecond() =>
        Assert.Equal("1.5 KB/s", ProcessFormat.Rate(1536));

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
