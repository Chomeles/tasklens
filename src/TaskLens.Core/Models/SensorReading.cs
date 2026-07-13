namespace TaskLens.Core.Models;

/// <summary>Coarse category of the hardware a <see cref="SensorReading"/> came from.</summary>
public enum HardwareKind
{
    Other,
    Cpu,
    Gpu,
    Motherboard,
    Storage,
}

/// <summary>
/// One sensor value at one point in time.
/// <paramref name="Value"/> is <c>null</c> when the sensor exists but reported no reading.
/// </summary>
public sealed record SensorReading(
    string Hardware, string Name, SensorKind Kind, float? Value, HardwareKind HardwareKind = HardwareKind.Other)
{
    public string Hardware { get; init; } =
        !string.IsNullOrWhiteSpace(Hardware)
            ? Hardware
            : throw new ArgumentException("Hardware must be non-empty.", nameof(Hardware));

    public string Name { get; init; } =
        !string.IsNullOrWhiteSpace(Name)
            ? Name
            : throw new ArgumentException("Name must be non-empty.", nameof(Name));
}
