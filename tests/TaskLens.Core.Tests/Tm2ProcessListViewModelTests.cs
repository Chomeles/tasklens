using TaskLens.Core.Models;
using TaskLens.Core.ViewModels;

namespace TaskLens.Core.Tests;

public class Tm2ProcessListViewModelTests
{
    private static readonly DateTime Start = new(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc);

    private readonly Tm2ProcessListViewModel vm = new();

    private static ProcessDelta Delta(int pid, string name, double cpu = 0, double gpu = 0) => new(
        new ProcessSample(pid, name, Start, TimeSpan.Zero, 1024, 0, 0), cpu, gpu, 0, 0);

    private static SensorReading Reading(
        string hardware, string name, SensorKind kind, float? value, HardwareKind hardwareKind = HardwareKind.Other) =>
        new(hardware, name, kind, value, hardwareKind);

    private static SystemSnapshot Snap(
        ProcessDelta[] deltas, SensorReading[]? sensors = null, SensorAvailability availability = SensorAvailability.Available) => new(
        TimestampUtc: Start,
        Processes: deltas,
        Sensors: sensors ?? [],
        SensorAvailability: availability,
        CpuTotalPercent: 0,
        MemoryUsedBytes: 0,
        MemoryTotalBytes: 0);

    [Fact]
    public void ApplySnapshot_JoinsInnerRows_OneToOne()
    {
        vm.ApplySnapshot(Snap([Delta(1, "alpha", cpu: 10, gpu: 5), Delta(2, "beta", cpu: 20)]));

        Assert.Equal(2, vm.Rows.Count);
        Assert.Equal(2, vm.Inner.Rows.Count);
        var alpha = vm.Rows.Single(r => r.Pid == 1);
        Assert.Equal("alpha", alpha.Name);
        Assert.Equal(10, alpha.CpuPercent);
        Assert.Equal(5, alpha.GpuPercent);
        Assert.Same(vm.Inner.Rows.Single(r => r.Pid == 1), alpha.Inner);
    }

    [Fact]
    public void ApplySnapshot_SameRowObject_AcrossTicks()
    {
        vm.ApplySnapshot(Snap([Delta(1, "alpha", cpu: 10)]));
        var row1 = vm.Rows.Single();

        vm.ApplySnapshot(Snap([Delta(1, "alpha", cpu: 30)]));
        var row2 = vm.Rows.Single();

        Assert.Same(row1, row2);
        Assert.Equal(30, row2.CpuPercent);
    }

    [Fact]
    public void ApplySnapshot_StampsCpuTempPackageWattFanRpm_OntoEveryRow()
    {
        vm.ApplySnapshot(Snap(
            [Delta(1, "alpha"), Delta(2, "beta")],
            [
                // real hardware.Name is a CPU model string, never the literal "CPU" - HardwareKind is what matters
                Reading("AMD Ryzen 7 5800X", "Package", SensorKind.Temperature, 55.5f, HardwareKind.Cpu),
                Reading("AMD Ryzen 7 5800X", "Package", SensorKind.Power, 45f, HardwareKind.Cpu),
                Reading("Motherboard", "Fan #1", SensorKind.Fan, 1200f, HardwareKind.Motherboard),
                Reading("NVIDIA RTX 4080", "GPU Core", SensorKind.Temperature, 70f, HardwareKind.Gpu), // not CPU: ignored
            ]));

        foreach (var row in vm.Rows)
        {
            Assert.Equal(55.5f, row.CpuTempCelsius);
            Assert.Equal(45f, row.PackageWattage);
            Assert.Equal(1200f, row.FanRpm);
        }
    }

    [Fact]
    public void ApplySnapshot_SensorsUnavailable_LeavesColumnsEmpty()
    {
        vm.ApplySnapshot(Snap([Delta(1, "alpha")], sensors: [], availability: SensorAvailability.NoSensors));

        var row = vm.Rows.Single();
        Assert.Null(row.CpuTempCelsius);
        Assert.Null(row.PackageWattage);
        Assert.Null(row.FanRpm);
    }

    [Fact]
    public void ApplySnapshot_GrowsCpuHistory_OneEntryPerTick()
    {
        vm.ApplySnapshot(Snap([Delta(1, "alpha", cpu: 10)]));
        vm.ApplySnapshot(Snap([Delta(1, "alpha", cpu: 20)]));
        vm.ApplySnapshot(Snap([Delta(1, "alpha", cpu: 30)]));

        var row = vm.Rows.Single();
        Assert.Equal([10f, 20f, 30f], row.CpuHistory);
    }

    [Fact]
    public void ApplySnapshot_ExitedProcess_DropsRow()
    {
        vm.ApplySnapshot(Snap([Delta(1, "alpha"), Delta(2, "beta")]));
        vm.ApplySnapshot(Snap([Delta(1, "alpha")]));

        Assert.Single(vm.Rows);
        Assert.Equal(1, vm.Rows.Single().Pid);
    }

    [Fact]
    public void Filter_DelegatesToInner_AndJoinFollows()
    {
        vm.Inner.Filter = "alpha";
        vm.ApplySnapshot(Snap([Delta(1, "alpha"), Delta(2, "beta")]));

        Assert.Single(vm.Rows);
        Assert.Equal("alpha", vm.Rows.Single().Name);
    }
}
