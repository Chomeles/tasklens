using TaskLens.Core.Models;
using TaskLens.Core.ViewModels;

namespace TaskLens.Core.Tests;

public class Tm2PerformanceViewModelTests
{
    private static readonly DateTime Start = new(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc);

    private readonly Tm2PerformanceViewModel vm = new();

    private static ProcessDelta Delta(int pid, double cpu = 0, double gpu = 0, double ioRead = 0, double ioWrite = 0) => new(
        new ProcessSample(pid, $"p{pid}", Start, TimeSpan.Zero, 1024, 0, 0), cpu, gpu, ioRead, ioWrite);

    private static SensorReading Reading(
        string hardware, string name, SensorKind kind, float? value, HardwareKind hardwareKind = HardwareKind.Other) =>
        new(hardware, name, kind, value, hardwareKind);

    private static SystemSnapshot Snap(
        ProcessDelta[]? deltas = null,
        SensorReading[]? sensors = null,
        double cpuTotal = 0,
        long memoryUsed = 0,
        long memoryTotal = 0,
        MemoryDetails? memory = null) => new(
        TimestampUtc: Start,
        Processes: deltas ?? [],
        Sensors: sensors ?? [],
        SensorAvailability: sensors is { Length: > 0 } ? SensorAvailability.Available : SensorAvailability.NoSensors,
        CpuTotalPercent: cpuTotal,
        MemoryUsedBytes: memoryUsed,
        MemoryTotalBytes: memoryTotal,
        Memory: memory);

    [Fact]
    public void Ctor_FourSystemEntries_CpuSelected()
    {
        Assert.Equal(["CPU", "Arbeitsspeicher", "Datenträger", "GPU"], vm.Entries.Select(e => e.Name));
        Assert.All(vm.Entries, e => Assert.Null(e.Group));
        Assert.Same(vm.Entries[0], vm.SelectedEntry);
    }

    [Fact]
    public void ApplySnapshot_AppendsOneRailEntryPerHardwareGroup()
    {
        vm.ApplySnapshot(Snap(sensors:
        [
            Reading("AMD Ryzen 7 5800X", "Core (Tctl)", SensorKind.Temperature, 55f, HardwareKind.Cpu),
            Reading("AMD Ryzen 7 5800X", "CPU Total", SensorKind.Load, 12f, HardwareKind.Cpu),
            Reading("NVIDIA RTX 4080", "GPU Core", SensorKind.Load, 33f, HardwareKind.Gpu),
        ]));

        Assert.Equal(
            ["CPU", "Arbeitsspeicher", "Datenträger", "GPU", "AMD Ryzen 7 5800X", "NVIDIA RTX 4080"],
            vm.Entries.Select(e => e.Name));
        Assert.Same(vm.Sensors.Groups[0], vm.Entries[4].Group);
        Assert.Same(vm.Sensors.Groups[0].Sensors, vm.Entries[4].Sensors);
        Assert.Equal(SensorAvailability.Available, vm.Sensors.Availability);
    }

    [Fact]
    public void ApplySnapshot_CpuEntry_TracksCpuTotalPercent()
    {
        vm.ApplySnapshot(Snap(cpuTotal: 10));
        vm.ApplySnapshot(Snap(cpuTotal: 20.5));

        Assert.Equal([10f, 20.5f], vm.Entries[0].History);
        Assert.Equal("20,5 %", vm.Entries[0].ValueText);
    }

    [Fact]
    public void ApplySnapshot_MemoryEntry_PercentHistory_ByteText()
    {
        vm.ApplySnapshot(Snap(memoryUsed: 512, memoryTotal: 1024));

        Assert.Equal([50f], vm.Entries[1].History);
        Assert.Equal("512,0 B / 1,0 KB", vm.Entries[1].ValueText);
    }

    [Fact]
    public void ApplySnapshot_MemoryTotalUnknown_NullHistoryPoint()
    {
        vm.ApplySnapshot(Snap());

        Assert.Equal([(float?)null], vm.Entries[1].History);
    }

    [Fact]
    public void ApplySnapshot_DiskEntry_SumsPerProcessIoRates()
    {
        vm.ApplySnapshot(Snap([Delta(1, ioRead: 1024 * 1024, ioWrite: 512 * 1024), Delta(2, ioRead: 512 * 1024)]));

        Assert.Equal([2f * 1024 * 1024], vm.Entries[2].History);
        Assert.Equal("2,0 MB/s", vm.Entries[2].ValueText);
    }

    [Fact]
    public void ApplySnapshot_GpuEntry_PrefersGpuLoadSensor()
    {
        vm.ApplySnapshot(Snap(
            [Delta(1, gpu: 10), Delta(2, gpu: 15)],
            [
                Reading("NVIDIA RTX 4080", "GPU Memory", SensorKind.Load, 20f, HardwareKind.Gpu),
                Reading("AMD Ryzen 7 5800X", "CPU Total", SensorKind.Load, 99f, HardwareKind.Cpu), // not GPU: ignored
            ]));

        Assert.Equal([20f], vm.Entries[3].History);
        Assert.Equal("20,0 %", vm.Entries[3].ValueText);
    }

    [Fact]
    public void ApplySnapshot_GpuEntry_PrefersCoreLoad_OverEarlierNonCoreLoad()
    {
        vm.ApplySnapshot(Snap(
            [Delta(1, gpu: 10)],
            [
                Reading("NVIDIA RTX 4080", "GPU Memory Controller", SensorKind.Load, 20f, HardwareKind.Gpu),
                Reading("NVIDIA RTX 4080", "GPU Core", SensorKind.Load, 63f, HardwareKind.Gpu),
            ]));

        Assert.Equal([63f], vm.Entries[3].History);
    }

    [Fact]
    public void ApplySnapshot_GpuEntry_WithoutGpuSensor_SumsPerProcessGpu()
    {
        vm.ApplySnapshot(Snap([Delta(1, gpu: 10), Delta(2, gpu: 15.5)]));

        Assert.Equal([25.5f], vm.Entries[3].History);
        Assert.Equal("25,5 %", vm.Entries[3].ValueText);
    }

    [Fact]
    public void ApplySnapshot_GroupEntry_HeadlineIsFirstLoadSensor()
    {
        vm.ApplySnapshot(Snap(sensors:
        [
            Reading("AMD Ryzen 7 5800X", "Core (Tctl)", SensorKind.Temperature, 55f, HardwareKind.Cpu),
            Reading("AMD Ryzen 7 5800X", "CPU Total", SensorKind.Load, 12f, HardwareKind.Cpu),
        ]));

        Assert.Equal([12f], vm.Entries[4].History);
        Assert.Equal("12,0 %", vm.Entries[4].ValueText);
    }

    [Fact]
    public void ApplySnapshot_GroupEntry_WithoutLoadSensor_FallsBackToFirstSensor()
    {
        vm.ApplySnapshot(Snap(sensors: [Reading("Motherboard", "Fan #1", SensorKind.Fan, 1200f, HardwareKind.Motherboard)]));

        Assert.Equal([1200f], vm.Entries[4].History);
        Assert.Equal("1200 RPM", vm.Entries[4].ValueText);
    }

    [Fact]
    public void ApplySnapshot_StableStructure_KeepsEntryInstancesSelectionAndHistory()
    {
        SensorReading[] Sensors(float load) => [Reading("AMD Ryzen 7 5800X", "CPU Total", SensorKind.Load, load, HardwareKind.Cpu)];

        vm.ApplySnapshot(Snap(sensors: Sensors(10f)));
        var entry = vm.Entries[4];
        vm.SelectedEntry = entry;

        vm.ApplySnapshot(Snap(sensors: Sensors(20f)));

        Assert.Same(entry, vm.Entries[4]);
        Assert.Same(entry, vm.SelectedEntry);
        Assert.Equal([10f, 20f], entry.History);
    }

    [Fact]
    public void ApplySnapshot_GroupsRebuilt_SelectedGroupEntryFallsBackToCpu()
    {
        vm.ApplySnapshot(Snap(sensors: [Reading("Old Hardware", "CPU Total", SensorKind.Load, 10f, HardwareKind.Cpu)]));
        vm.SelectedEntry = vm.Entries[4];

        vm.ApplySnapshot(Snap(sensors: [Reading("New Hardware", "CPU Total", SensorKind.Load, 20f, HardwareKind.Cpu)]));

        Assert.Equal("New Hardware", vm.Entries[4].Name);
        Assert.Same(vm.Entries[0], vm.SelectedEntry);
    }

    [Fact]
    public void ApplySnapshot_SensorsGone_DropsGroupEntries()
    {
        vm.ApplySnapshot(Snap(sensors: [Reading("Old Hardware", "CPU Total", SensorKind.Load, 10f, HardwareKind.Cpu)]));
        vm.ApplySnapshot(Snap());

        Assert.Equal(4, vm.Entries.Count);
        Assert.Equal(SensorAvailability.NoSensors, vm.Sensors.Availability);
    }

    [Fact]
    public void ApplySnapshot_WithMemoryDetails_FillsPanel()
    {
        const long gb = 1024L * 1024 * 1024;
        vm.ApplySnapshot(Snap(
            memoryUsed: 20 * gb,
            memoryTotal: 32 * gb,
            memory: new MemoryDetails(
                CommittedBytes: 31 * gb,
                CommitLimitBytes: 36 * gb,
                CachedBytes: 11 * gb,
                PagedPoolBytes: 2 * gb,
                NonPagedPoolBytes: 1 * gb,
                ProcessCount: 214,
                ThreadCount: 2801,
                HandleCount: 94213)));

        var panel = vm.MemoryDetails;
        Assert.Equal(ProcessFormat.Bytes(20 * gb), panel.InUseText);
        Assert.Equal(ProcessFormat.Bytes(12 * gb), panel.AvailableText);
        Assert.Equal($"{ProcessFormat.Bytes(31 * gb)} / {ProcessFormat.Bytes(36 * gb)}", panel.CommittedText);
        Assert.Equal(ProcessFormat.Bytes(11 * gb), panel.CachedText);
        Assert.Equal(ProcessFormat.Bytes(2 * gb), panel.PagedPoolText);
        Assert.Equal(ProcessFormat.Bytes(1 * gb), panel.NonPagedPoolText);
        Assert.Equal(20.0 / 32.0, panel.UsedFraction, precision: 10);
    }

    [Fact]
    public void ApplySnapshot_WithoutMemoryDetails_PanelFallsBackToDashes()
    {
        const long gb = 1024L * 1024 * 1024;
        vm.ApplySnapshot(Snap(memoryUsed: 20 * gb, memoryTotal: 32 * gb));

        var panel = vm.MemoryDetails;
        Assert.Equal(ProcessFormat.Bytes(20 * gb), panel.InUseText); // header values need no details
        Assert.Equal("—", panel.CommittedText);
        Assert.Equal("—", panel.CachedText);
        Assert.Equal("—", panel.PagedPoolText);
        Assert.Equal("—", panel.NonPagedPoolText);
    }

    [Fact]
    public void IsMemorySelected_TracksSelection_AndNotifies()
    {
        var notified = false;
        vm.PropertyChanged += (_, e) => notified |= e.PropertyName == nameof(Tm2PerformanceViewModel.IsMemorySelected);

        Assert.False(vm.IsMemorySelected); // CPU is the initial selection
        vm.SelectedEntry = vm.Entries[1]; // Arbeitsspeicher

        Assert.True(vm.IsMemorySelected);
        Assert.True(notified);
    }
}
