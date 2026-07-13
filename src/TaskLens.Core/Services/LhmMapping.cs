using TaskLens.Core.Models;

namespace TaskLens.Core.Services;

/// <summary>
/// One flattened LibreHardwareMonitor sensor row, provider-neutral so the mapping is testable on
/// Linux. <c>LhmSensorService</c> (App project) fills these from the live LHM tree;
/// <paramref name="SensorTypeName"/> is the LHM <c>SensorType</c> enum member name ("Temperature", …).
/// </summary>
public readonly record struct LhmSensorRow(string Hardware, string Name, string SensorTypeName, float? Value, string HardwareTypeName = "");

/// <summary>
/// Pure mapping from the LibreHardwareMonitor sensor tree to Core models. Lives in Core so the
/// logic runs under the Linux gates; the thread and Computer lifecycle around it is
/// <c>LhmSensorService</c> in the App project.
/// </summary>
public static class LhmMapping
{
    /// <summary>Maps an LHM sensor type name to a <see cref="SensorKind"/>; null for kinds TaskLens does not show.</summary>
    public static SensorKind? MapKind(string sensorTypeName) => sensorTypeName switch
    {
        "Temperature" => SensorKind.Temperature,
        "Load" => SensorKind.Load,
        "Clock" => SensorKind.Clock,
        "Fan" => SensorKind.Fan,
        "Power" => SensorKind.Power,
        "Voltage" => SensorKind.Voltage,
        _ => null, // Control/Data/Throughput/... - ponytail: extend the switch when a page needs one
    };

    /// <summary>Maps an LHM hardware type name to a <see cref="HardwareKind"/>; unrecognised names map to Other.</summary>
    public static HardwareKind MapHardwareKind(string hardwareTypeName) => hardwareTypeName switch
    {
        "Cpu" => HardwareKind.Cpu,
        "GpuNvidia" or "GpuAmd" or "GpuIntel" => HardwareKind.Gpu,
        "Motherboard" => HardwareKind.Motherboard,
        "Storage" => HardwareKind.Storage,
        _ => HardwareKind.Other,
    };

    /// <summary>
    /// Availability precedence: any mapped reading wins; an empty tree is explained by the most
    /// actionable missing prerequisite first (elevation, then the PawnIO driver), and only a fully
    /// provisioned machine with nothing to read — the VM case — reports <see cref="SensorAvailability.NoSensors"/>.
    /// </summary>
    public static SensorAvailability ClassifyAvailability(bool hasReadings, bool isElevated, bool isPawnIoInstalled) =>
        hasReadings ? SensorAvailability.Available
        : !isElevated ? SensorAvailability.NoAdmin
        : !isPawnIoInstalled ? SensorAvailability.NoPawnIo
        : SensorAvailability.NoSensors;

    /// <summary>
    /// Maps flattened rows to one <see cref="SensorSnapshot"/>: unshown kinds and blank-named rows
    /// are dropped (absence is data, never exceptions), order is preserved, null values pass
    /// through, and availability is classified on the mapped result.
    /// </summary>
    public static SensorSnapshot BuildSnapshot(IEnumerable<LhmSensorRow> rows, bool isElevated, bool isPawnIoInstalled)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var readings = new List<SensorReading>();
        foreach (var row in rows)
        {
            if (MapKind(row.SensorTypeName) is { } kind
                && !string.IsNullOrWhiteSpace(row.Hardware)
                && !string.IsNullOrWhiteSpace(row.Name))
            {
                readings.Add(new SensorReading(row.Hardware, row.Name, kind, row.Value, MapHardwareKind(row.HardwareTypeName)));
            }
        }

        return new SensorSnapshot(readings, ClassifyAvailability(readings.Count > 0, isElevated, isPawnIoInstalled));
    }
}
