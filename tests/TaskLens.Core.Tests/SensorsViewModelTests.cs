using System.Collections.Specialized;
using System.ComponentModel;
using TaskLens.Core.Models;
using TaskLens.Core.Services;
using TaskLens.Core.ViewModels;

namespace TaskLens.Core.Tests;

public class SensorsViewModelTests
{
    private static readonly DateTime Start = new(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc);

    private readonly SensorsViewModel vm = new();

    private static SensorReading Reading(string hardware, string name, SensorKind kind, float? value) =>
        new(hardware, name, kind, value);

    private static SystemSnapshot Snap(
        SensorAvailability availability = SensorAvailability.Available,
        params SensorReading[] sensors) => new(
            TimestampUtc: Start,
            Processes: [],
            Sensors: sensors,
            SensorAvailability: availability,
            CpuTotalPercent: 0,
            MemoryUsedBytes: 0,
            MemoryTotalBytes: 0);

    // --- availability / banner states ---

    [Fact]
    public void Available_HidesBanner()
    {
        vm.ApplySnapshot(Snap(SensorAvailability.Available, Reading("CPU", "Core", SensorKind.Temperature, 50)));

        Assert.Equal(SensorAvailability.Available, vm.Availability);
        Assert.False(vm.ShowBanner);
        Assert.Equal("", vm.BannerText);
    }

    [Theory]
    [InlineData(SensorAvailability.NoAdmin, "administrator")]
    [InlineData(SensorAvailability.NoPawnIo, "PawnIO")]
    [InlineData(SensorAvailability.NoSensors, "virtual machines")]
    public void DegradedStates_ShowBannerNamingTheRemedy(SensorAvailability availability, string expectedHint)
    {
        vm.ApplySnapshot(Snap(availability));

        Assert.Equal(availability, vm.Availability);
        Assert.True(vm.ShowBanner);
        Assert.Contains(expectedHint, vm.BannerText);
    }

    [Fact]
    public void DegradedStates_HaveDistinctBannerTexts()
    {
        var texts = new[] { SensorAvailability.NoAdmin, SensorAvailability.NoPawnIo, SensorAvailability.NoSensors }
            .Select(a =>
            {
                vm.ApplySnapshot(Snap(a));
                return vm.BannerText;
            })
            .ToList();

        Assert.Equal(3, texts.Distinct().Count());
        Assert.All(texts, t => Assert.NotEqual("", t));
    }

    [Fact]
    public void AvailabilityChange_NotifiesBannerProperties()
    {
        vm.ApplySnapshot(Snap(SensorAvailability.NoAdmin));

        var changed = new List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        vm.ApplySnapshot(Snap(SensorAvailability.Available, Reading("CPU", "Core", SensorKind.Temperature, 50)));

        Assert.Contains(nameof(SensorsViewModel.Availability), changed);
        Assert.Contains(nameof(SensorsViewModel.ShowBanner), changed);
        Assert.Contains(nameof(SensorsViewModel.BannerText), changed);
    }

    [Fact]
    public void PartialDegradation_KeepsAvailableReadingsVisible()
    {
        // NoPawnIo still yields driverless sensors (GPU via NVML etc.) — banner AND data coexist.
        vm.ApplySnapshot(Snap(SensorAvailability.NoPawnIo, Reading("GPU", "GPU Core", SensorKind.Temperature, 61)));

        Assert.True(vm.ShowBanner);
        Assert.Equal("61.0 °C", vm.Groups.Single().Sensors.Single().ValueText);
    }

    // --- unit formatting ---

    [Theory]
    [InlineData(SensorKind.Temperature, 54.04f, "54.0 °C")]
    [InlineData(SensorKind.Temperature, 99.95f, "100.0 °C")]
    [InlineData(SensorKind.Load, 12.34f, "12.3 %")]
    [InlineData(SensorKind.Clock, 4200.4f, "4200 MHz")]
    [InlineData(SensorKind.Fan, 1200.49f, "1200 RPM")]
    [InlineData(SensorKind.Power, 45.26f, "45.3 W")]
    [InlineData(SensorKind.Voltage, 1.2504f, "1.250 V")]
    public void Format_AppendsUnitPerKind(SensorKind kind, float value, string expected) =>
        Assert.Equal(expected, SensorRowViewModel.Format(kind, value));

    [Theory]
    [InlineData(SensorKind.Temperature)]
    [InlineData(SensorKind.Load)]
    [InlineData(SensorKind.Clock)]
    [InlineData(SensorKind.Fan)]
    [InlineData(SensorKind.Power)]
    [InlineData(SensorKind.Voltage)]
    public void Format_NullValue_IsDash(SensorKind kind) =>
        Assert.Equal("—", SensorRowViewModel.Format(kind, null));

    [Fact]
    public void ValueText_TracksValueUpdates_AndNotifies()
    {
        var row = new SensorRowViewModel("Core", SensorKind.Temperature, 50);
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        row.Value = 62.5f;

        Assert.Equal("62.5 °C", row.ValueText);
        Assert.Contains(nameof(SensorRowViewModel.Value), changed);
        Assert.Contains(nameof(SensorRowViewModel.ValueText), changed);
    }

    // --- grouping ---

    [Fact]
    public void Grouping_ByHardware_FirstSeenOrder_InterleavedInput()
    {
        vm.ApplySnapshot(Snap(SensorAvailability.Available,
            Reading("CPU", "Package", SensorKind.Power, 45),
            Reading("GPU", "GPU Core", SensorKind.Temperature, 60),
            Reading("CPU", "Core Max", SensorKind.Temperature, 71),
            Reading("Motherboard", "Fan #1", SensorKind.Fan, 900)));

        Assert.Equal(["CPU", "GPU", "Motherboard"], vm.Groups.Select(g => g.Name));
        Assert.Equal(["Package", "Core Max"], vm.Groups[0].Sensors.Select(s => s.Name));
        Assert.Equal(["45.0 W", "71.0 °C"], vm.Groups[0].Sensors.Select(s => s.ValueText));
    }

    [Fact]
    public void EmptySensors_YieldNoGroups()
    {
        vm.ApplySnapshot(Snap(SensorAvailability.NoSensors));

        Assert.Empty(vm.Groups);
    }

    // --- in-place update semantics ---

    [Fact]
    public void SameStructure_UpdatesValuesInPlace_NoCollectionChurn()
    {
        vm.ApplySnapshot(Snap(SensorAvailability.Available,
            Reading("CPU", "Core", SensorKind.Temperature, 50),
            Reading("CPU", "Package", SensorKind.Power, 40)));
        var group = vm.Groups.Single();
        var rows = group.Sensors.ToList();

        var events = new List<NotifyCollectionChangedEventArgs>();
        vm.Groups.CollectionChanged += (_, e) => events.Add(e);
        group.Sensors.CollectionChanged += (_, e) => events.Add(e);

        vm.ApplySnapshot(Snap(SensorAvailability.Available,
            Reading("CPU", "Core", SensorKind.Temperature, 55.5f),
            Reading("CPU", "Package", SensorKind.Power, null)));

        Assert.Empty(events);
        Assert.Same(group, vm.Groups.Single());
        Assert.Equal(rows, vm.Groups.Single().Sensors);
        Assert.Equal("55.5 °C", rows[0].ValueText);
        Assert.Equal("—", rows[1].ValueText);
    }

    [Fact]
    public void StructuralChange_RebuildsGroups()
    {
        vm.ApplySnapshot(Snap(SensorAvailability.Available, Reading("CPU", "Core", SensorKind.Temperature, 50)));

        vm.ApplySnapshot(Snap(SensorAvailability.Available,
            Reading("CPU", "Core", SensorKind.Temperature, 50),
            Reading("GPU", "GPU Core", SensorKind.Temperature, 60)));

        Assert.Equal(["CPU", "GPU"], vm.Groups.Select(g => g.Name));
    }

    [Fact]
    public void SameSensorName_DifferentKind_IsAStructuralChange()
    {
        vm.ApplySnapshot(Snap(SensorAvailability.Available, Reading("CPU", "Core", SensorKind.Temperature, 50)));
        var oldRow = vm.Groups.Single().Sensors.Single();

        vm.ApplySnapshot(Snap(SensorAvailability.Available, Reading("CPU", "Core", SensorKind.Load, 50)));

        var newRow = vm.Groups.Single().Sensors.Single();
        Assert.NotSame(oldRow, newRow);
        Assert.Equal(SensorKind.Load, newRow.Kind);
    }

    // --- sparkline history ---

    [Fact]
    public void EveryTick_AppendsToHistory_EvenWhenValueIsUnchanged()
    {
        vm.ApplySnapshot(Snap(SensorAvailability.Available, Reading("CPU", "Core", SensorKind.Temperature, 50)));
        var row = vm.Groups.Single().Sensors.Single();

        vm.ApplySnapshot(Snap(SensorAvailability.Available, Reading("CPU", "Core", SensorKind.Temperature, 50)));
        vm.ApplySnapshot(Snap(SensorAvailability.Available, Reading("CPU", "Core", SensorKind.Temperature, 55)));

        Assert.Equal(new float?[] { 50f, 50f, 55f }, row.History);
    }

    [Fact]
    public void HistoryUpdate_NotifiesHistoryProperty()
    {
        vm.ApplySnapshot(Snap(SensorAvailability.Available, Reading("CPU", "Core", SensorKind.Temperature, 50)));
        var row = vm.Groups.Single().Sensors.Single();
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.ApplySnapshot(Snap(SensorAvailability.Available, Reading("CPU", "Core", SensorKind.Temperature, 50)));

        Assert.Contains(nameof(SensorRowViewModel.History), changed);
    }

    [Fact]
    public void History_IsCappedAtCapacity_OldestDropped()
    {
        var row = new SensorRowViewModel("Core", SensorKind.Temperature, 0);
        for (var i = 1; i <= 100; i++)
        {
            row.Update(i);
        }

        Assert.Equal(60, row.History.Count);
        Assert.Equal(41f, row.History[0]);
        Assert.Equal(100f, row.History[^1]);
    }

    // --- engine wiring ---

    [Fact]
    public void EngineTick_ViaSyncDispatcher_PopulatesGroupsAndAvailability()
    {
        var sensors = new FakeSensorService
        {
            Snapshot = new SensorSnapshot(
                [Reading("CPU", "Core", SensorKind.Temperature, 42)],
                SensorAvailability.Available),
        };
        var engine = new SamplingEngine(
            new FakeProcessEnumerator(), sensors, new FakeGpuProcessService(), new FakeSystemMetricsService(),
            new ManualClock(), new SyncDispatcher(), processorCount: 1);
        engine.SnapshotReady += vm.ApplySnapshot;

        engine.Tick();

        Assert.Equal(SensorAvailability.Available, vm.Availability);
        Assert.Equal("42.0 °C", vm.Groups.Single().Sensors.Single().ValueText);
    }

    [Fact]
    public void Unit_Fahrenheit_ConvertsExistingAndNewTemperatureRows()
    {
        vm.ApplySnapshot(Snap(SensorAvailability.Available, Reading("CPU", "Core", SensorKind.Temperature, 0)));

        vm.Unit = TemperatureUnit.Fahrenheit;

        Assert.Equal("32.0 °F", vm.Groups.Single().Sensors.Single().ValueText);
    }
}
