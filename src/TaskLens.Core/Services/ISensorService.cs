using TaskLens.Core.Models;

namespace TaskLens.Core.Services;

/// <summary>One tick's sensor readings plus why they may be missing.</summary>
public sealed record SensorSnapshot(IReadOnlyList<SensorReading> Readings, SensorAvailability Availability)
{
    public IReadOnlyList<SensorReading> Readings { get; init; } =
        Readings ?? throw new ArgumentNullException(nameof(Readings));
}

/// <summary>Reads the hardware sensor tree. Absence is modelled as data, never exceptions.</summary>
public interface ISensorService
{
    public SensorSnapshot Sample();
}
