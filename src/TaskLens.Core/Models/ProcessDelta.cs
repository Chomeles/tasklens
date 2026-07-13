namespace TaskLens.Core.Models;

/// <summary>
/// One process row with per-tick computed rates: CPU% (all-cores normalized, 0–100),
/// GPU% and IO bytes/sec. Rates are 0 on a process's first sighting (no previous sample).
/// </summary>
public sealed record ProcessDelta(
    ProcessSample Sample,
    double CpuPercent,
    double GpuPercent,
    double IoReadBytesPerSecond,
    double IoWriteBytesPerSecond)
{
    public ProcessSample Sample { get; init; } =
        Sample ?? throw new ArgumentNullException(nameof(Sample));

    public double CpuPercent { get; init; } =
        CpuPercent is >= 0 and <= 100
            ? CpuPercent
            : throw new ArgumentOutOfRangeException(nameof(CpuPercent), CpuPercent, "CpuPercent must be within [0, 100].");

    public double GpuPercent { get; init; } =
        GpuPercent is >= 0 and <= 100
            ? GpuPercent
            : throw new ArgumentOutOfRangeException(nameof(GpuPercent), GpuPercent, "GpuPercent must be within [0, 100].");

    public double IoReadBytesPerSecond { get; init; } =
        IoReadBytesPerSecond >= 0
            ? IoReadBytesPerSecond
            : throw new ArgumentOutOfRangeException(nameof(IoReadBytesPerSecond), IoReadBytesPerSecond, "IoReadBytesPerSecond must be >= 0.");

    public double IoWriteBytesPerSecond { get; init; } =
        IoWriteBytesPerSecond >= 0
            ? IoWriteBytesPerSecond
            : throw new ArgumentOutOfRangeException(nameof(IoWriteBytesPerSecond), IoWriteBytesPerSecond, "IoWriteBytesPerSecond must be >= 0.");
}
