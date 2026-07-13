using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TaskLens.Core.Models;

namespace TaskLens.Core.ViewModels;

/// <summary>Sensors of one hardware node (e.g. "AMD Ryzen 9 5950X"). Name is the group identity.</summary>
public sealed class HardwareGroupViewModel
{
    public HardwareGroupViewModel(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public string Name { get; }

    public ObservableCollection<SensorRowViewModel> Sensors { get; } = [];
}

/// <summary>
/// Sensor panel: readings grouped by hardware (first-seen snapshot order) plus a degradation
/// banner mapped from <see cref="SensorAvailability"/> (no admin / no PawnIO / no sensors).
/// <see cref="ApplySnapshot"/> is called on the UI thread (the engine posts via <c>IDispatcher</c>);
/// when the sensor tree structure is unchanged — the overwhelmingly common case — values are
/// updated in place and no collection events fire.
/// </summary>
public sealed partial class SensorsViewModel : ObservableObject
{
    public ObservableCollection<HardwareGroupViewModel> Groups { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowBanner))]
    [NotifyPropertyChangedFor(nameof(BannerText))]
    private SensorAvailability availability = SensorAvailability.NoSensors;

    /// <summary>True whenever sensors are degraded and the banner should be visible.</summary>
    public bool ShowBanner => Availability != SensorAvailability.Available;

    /// <summary>User-facing explanation of the current degradation; empty when available.</summary>
    public string BannerText => Availability switch
    {
        SensorAvailability.Available => "",
        SensorAvailability.NoAdmin =>
            "Run TaskLens as administrator to unlock CPU temperatures, power and fan sensors.",
        SensorAvailability.NoPawnIo =>
            "The PawnIO driver is not installed. Install PawnIO to read CPU temperatures, power and fan speeds.",
        SensorAvailability.NoSensors =>
            "No hardware sensors were found. This is normal inside virtual machines.",
        _ => throw new ArgumentOutOfRangeException(nameof(Availability), Availability, "Unknown availability."),
    };

    /// <summary>Applies one snapshot as a batch: group by hardware, update values in place.</summary>
    public void ApplySnapshot(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Availability = snapshot.SensorAvailability;

        var grouped = GroupByHardware(snapshot.Sensors);
        if (StructureMatches(grouped))
        {
            for (var g = 0; g < grouped.Count; g++)
            {
                var rows = Groups[g].Sensors;
                var readings = grouped[g].Readings;
                for (var s = 0; s < readings.Count; s++)
                {
                    rows[s].Update(readings[s].Value);
                }
            }

            return;
        }

        // ponytail: full rebuild on structural change — the sensor tree is static after startup,
        // so this runs on the first snapshot and virtually never again; reconcile if that changes.
        Groups.Clear();
        foreach (var (hardware, readings) in grouped)
        {
            var group = new HardwareGroupViewModel(hardware);
            foreach (var reading in readings)
            {
                group.Sensors.Add(new SensorRowViewModel(reading.Name, reading.Kind, reading.Value));
            }

            Groups.Add(group);
        }
    }

    /// <summary>Groups readings by hardware, groups and rows in first-seen snapshot order.</summary>
    private static List<(string Hardware, List<SensorReading> Readings)> GroupByHardware(
        IReadOnlyList<SensorReading> sensors)
    {
        var grouped = new List<(string Hardware, List<SensorReading> Readings)>();
        var byHardware = new Dictionary<string, List<SensorReading>>();
        foreach (var reading in sensors)
        {
            if (!byHardware.TryGetValue(reading.Hardware, out var list))
            {
                list = [];
                byHardware[reading.Hardware] = list;
                grouped.Add((reading.Hardware, list));
            }

            list.Add(reading);
        }

        return grouped;
    }

    private bool StructureMatches(List<(string Hardware, List<SensorReading> Readings)> grouped)
    {
        if (grouped.Count != Groups.Count)
        {
            return false;
        }

        for (var g = 0; g < grouped.Count; g++)
        {
            var rows = Groups[g].Sensors;
            var readings = grouped[g].Readings;
            if (grouped[g].Hardware != Groups[g].Name || readings.Count != rows.Count)
            {
                return false;
            }

            for (var s = 0; s < readings.Count; s++)
            {
                if (readings[s].Name != rows[s].Name || readings[s].Kind != rows[s].Kind)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
