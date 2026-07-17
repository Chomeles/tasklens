namespace TaskLens.Core.Models;

/// <summary>
/// One immutable snapshot of the whole system for one sampling tick.
/// Built by the sampling engine (task 03) and posted to ViewModels in a single batch.
/// Record equality is by reference for the list members; scalar members compare by value.
/// </summary>
public sealed record SystemSnapshot(
    DateTime TimestampUtc,
    IReadOnlyList<ProcessDelta> Processes,
    IReadOnlyList<SensorReading> Sensors,
    SensorAvailability SensorAvailability,
    double CpuTotalPercent,
    long MemoryUsedBytes,
    long MemoryTotalBytes,
    MemoryDetails? Memory = null,
    IReadOnlyList<NetworkAdapterRate>? Network = null,
    DiskDetails? Disk = null)
{
    /// <summary>Per-adapter throughput; empty when the platform provides none.</summary>
    public IReadOnlyList<NetworkAdapterRate> Network { get; init; } = Network ?? [];

    public IReadOnlyList<ProcessDelta> Processes { get; init; } =
        Processes ?? throw new ArgumentNullException(nameof(Processes));

    public IReadOnlyList<SensorReading> Sensors { get; init; } =
        Sensors ?? throw new ArgumentNullException(nameof(Sensors));

    public double CpuTotalPercent { get; init; } =
        CpuTotalPercent is >= 0 and <= 100
            ? CpuTotalPercent
            : throw new ArgumentOutOfRangeException(nameof(CpuTotalPercent), CpuTotalPercent, "CpuTotalPercent must be within [0, 100].");

    public long MemoryTotalBytes { get; init; } =
        MemoryTotalBytes >= 0
            ? MemoryTotalBytes
            : throw new ArgumentOutOfRangeException(nameof(MemoryTotalBytes), MemoryTotalBytes, "MemoryTotalBytes must be >= 0.");

    public long MemoryUsedBytes { get; init; } =
        MemoryUsedBytes >= 0 && MemoryUsedBytes <= MemoryTotalBytes
            ? MemoryUsedBytes
            : throw new ArgumentOutOfRangeException(nameof(MemoryUsedBytes), MemoryUsedBytes, "MemoryUsedBytes must be within [0, MemoryTotalBytes].");
}
