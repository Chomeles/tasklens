namespace TaskLens.Core.Models;

/// <summary>
/// One sensor value at one point in time.
/// <paramref name="Value"/> is <c>null</c> when the sensor exists but reported no reading.
/// </summary>
public sealed record SensorReading(string Hardware, string Name, SensorKind Kind, float? Value)
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
