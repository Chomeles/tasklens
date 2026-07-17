namespace TaskLens.Core.Services;

/// <summary>
/// Pure rate aggregation for per-process network byte counts. <see cref="Add"/> is called from the
/// ETW event-callback thread with (pid, bytes) increments; <see cref="Tick"/> is called once per
/// sampling tick and drains everything accumulated since the last tick into bytes/sec. The pending
/// map is rebuilt from scratch between ticks, so PIDs without traffic — including exited ones —
/// are pruned automatically (absent = 0, same contract as the GPU map). Lives in Core so it's
/// unit-tested on Linux; the ETW plumbing is <c>EtwProcessNetworkService</c> in the App project.
/// </summary>
public sealed class ProcessNetworkAggregator
{
    private static readonly IReadOnlyDictionary<int, double> Empty = new Dictionary<int, double>();

    // ponytail: one global lock — Add is a dictionary upsert, contention is invisible next to the
    // kernel→user ETW hop. Shard per-PID only if a profiler ever blames this.
    private readonly object gate = new();
    private Dictionary<int, long> pending = [];

    /// <summary>Records one event's byte count for a PID. Thread-safe; called on the event thread.</summary>
    public void Add(int pid, long bytes)
    {
        if (bytes <= 0)
        {
            return;
        }

        lock (gate)
        {
            pending.TryGetValue(pid, out var total);
            pending[pid] = total + bytes;
        }
    }

    /// <summary>Drains the accumulated bytes into per-PID bytes/sec over <paramref name="elapsed"/>.</summary>
    public IReadOnlyDictionary<int, double> Tick(TimeSpan elapsed)
    {
        Dictionary<int, long> drained;
        lock (gate)
        {
            drained = pending;
            pending = [];
        }

        if (elapsed <= TimeSpan.Zero || drained.Count == 0)
        {
            return Empty;
        }

        var result = new Dictionary<int, double>(drained.Count);
        foreach (var (pid, bytes) in drained)
        {
            result[pid] = bytes / elapsed.TotalSeconds;
        }

        return result;
    }
}
