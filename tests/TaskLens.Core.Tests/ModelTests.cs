using TaskLens.Core.Models;

namespace TaskLens.Core.Tests;

public class ProcessSampleTests
{
    private static ProcessSample Sample() => new(
        Pid: 42,
        Name: "dotnet",
        StartTimeUtc: new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc),
        TotalCpuTime: TimeSpan.FromSeconds(5),
        WorkingSetBytes: 1024,
        IoReadBytes: 100,
        IoWriteBytes: 200);

    [Fact]
    public void EqualByValue()
    {
        Assert.Equal(Sample(), Sample());
        Assert.Equal(Sample().GetHashCode(), Sample().GetHashCode());
    }

    [Fact]
    public void PidAndStartTimeDistinguishIdentity()
    {
        Assert.NotEqual(Sample(), Sample() with { Pid = 43 });
        Assert.NotEqual(Sample(), Sample() with { StartTimeUtc = DateTime.UnixEpoch });
    }

    [Theory]
    [InlineData(-1, 0, 0, 0, 0)]  // Pid
    [InlineData(0, -1, 0, 0, 0)]  // TotalCpuTime seconds
    [InlineData(0, 0, -1, 0, 0)]  // WorkingSetBytes
    [InlineData(0, 0, 0, -1, 0)]  // IoReadBytes
    [InlineData(0, 0, 0, 0, -1)]  // IoWriteBytes
    public void NegativeValuesThrow(int pid, int cpuSeconds, long ws, long ioRead, long ioWrite)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProcessSample(
            pid, "x", DateTime.UnixEpoch, TimeSpan.FromSeconds(cpuSeconds), ws, ioRead, ioWrite));
    }
}

public class SensorReadingTests
{
    [Fact]
    public void EqualByValue()
    {
        var a = new SensorReading("CPU", "Core #1", SensorKind.Temperature, 55.5f);
        var b = new SensorReading("CPU", "Core #1", SensorKind.Temperature, 55.5f);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void NullValueMeansNoReading()
    {
        var r = new SensorReading("GPU", "Hot Spot", SensorKind.Temperature, null);
        Assert.Null(r.Value);
    }

    [Theory]
    [InlineData("", "Core #1")]
    [InlineData(" ", "Core #1")]
    [InlineData("CPU", "")]
    [InlineData("CPU", " ")]
    public void EmptyNamesThrow(string hardware, string name)
    {
        Assert.Throws<ArgumentException>(() => new SensorReading(hardware, name, SensorKind.Load, 1f));
    }
}

public class SystemSnapshotTests
{
    private static SystemSnapshot Snapshot(double cpu = 12.5, long used = 512, long total = 1024) => new(
        TimestampUtc: new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc),
        Processes: [],
        Sensors: [],
        SensorAvailability: SensorAvailability.NoSensors,
        CpuTotalPercent: cpu,
        MemoryUsedBytes: used,
        MemoryTotalBytes: total);

    [Fact]
    public void HoldsWhatItIsGiven()
    {
        var s = Snapshot();
        Assert.Empty(s.Processes);
        Assert.Empty(s.Sensors);
        Assert.Equal(SensorAvailability.NoSensors, s.SensorAvailability);
        Assert.Equal(12.5, s.CpuTotalPercent);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    [InlineData(double.NaN)]
    public void CpuPercentOutsideRangeThrows(double cpu)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Snapshot(cpu: cpu));
    }

    [Theory]
    [InlineData(-1, 1024)]   // negative used
    [InlineData(2048, 1024)] // used > total
    [InlineData(0, -1)]      // negative total
    public void MemoryInvariantsThrow(long used, long total)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Snapshot(used: used, total: total));
    }

    [Fact]
    public void BoundaryCpuValuesAccepted()
    {
        Assert.Equal(0, Snapshot(cpu: 0).CpuTotalPercent);
        Assert.Equal(100, Snapshot(cpu: 100).CpuTotalPercent);
    }
}

public class SettingsTests
{
    [Fact]
    public void DefaultsAreSane()
    {
        var s = Settings.Default;
        Assert.Equal(TimeSpan.FromSeconds(1), s.RefreshInterval);
        Assert.Equal(TemperatureUnit.Celsius, s.TemperatureUnit);
        Assert.Equal(CpuPercentNormalization.AllCores, s.CpuNormalization);
    }

    [Fact]
    public void EqualByValue()
    {
        Assert.Equal(new Settings(), Settings.Default);
        Assert.NotEqual(Settings.Default, Settings.Default with { TemperatureUnit = TemperatureUnit.Fahrenheit });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveRefreshIntervalThrows(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Settings.Default with { RefreshInterval = TimeSpan.FromSeconds(seconds) });
    }
}
