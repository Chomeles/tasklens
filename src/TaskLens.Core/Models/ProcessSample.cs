namespace TaskLens.Core.Models;

/// <summary>
/// Raw per-tick row for one process, as produced by <c>IProcessEnumerator</c>.
/// Identity across ticks is <c>(Pid, StartTimeUtc)</c> — PID-reuse safe (deltas in task 03).
/// </summary>
public sealed record ProcessSample(
    int Pid,
    string Name,
    DateTime StartTimeUtc,
    TimeSpan TotalCpuTime,
    long WorkingSetBytes,
    long IoReadBytes,
    long IoWriteBytes,
    bool HasVisibleWindow = false)
{
    // ponytail: constructor-path guards via initializers; `with` can bypass, engine only constructs.
    public int Pid { get; init; } =
        Pid >= 0 ? Pid : throw new ArgumentOutOfRangeException(nameof(Pid), Pid, "Pid must be >= 0.");

    public TimeSpan TotalCpuTime { get; init; } =
        TotalCpuTime >= TimeSpan.Zero
            ? TotalCpuTime
            : throw new ArgumentOutOfRangeException(nameof(TotalCpuTime), TotalCpuTime, "TotalCpuTime must be >= 0.");

    public long WorkingSetBytes { get; init; } =
        WorkingSetBytes >= 0
            ? WorkingSetBytes
            : throw new ArgumentOutOfRangeException(nameof(WorkingSetBytes), WorkingSetBytes, "WorkingSetBytes must be >= 0.");

    public long IoReadBytes { get; init; } =
        IoReadBytes >= 0
            ? IoReadBytes
            : throw new ArgumentOutOfRangeException(nameof(IoReadBytes), IoReadBytes, "IoReadBytes must be >= 0.");

    public long IoWriteBytes { get; init; } =
        IoWriteBytes >= 0
            ? IoWriteBytes
            : throw new ArgumentOutOfRangeException(nameof(IoWriteBytes), IoWriteBytes, "IoWriteBytes must be >= 0.");
}
