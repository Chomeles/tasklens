namespace TaskLens.Core.Models;

/// <summary>
/// The Arbeitsspeicher detail values of the real Task Manager's Leistung page (plan-tm3 tm3-03):
/// commit charge, system cache, kernel pools and the system-wide object counts. Produced by
/// <c>ISystemMetricsService</c> where the platform provides them; a null on the snapshot means
/// "not available" and the view shows placeholders.
/// </summary>
public sealed record MemoryDetails(
    long CommittedBytes,
    long CommitLimitBytes,
    long CachedBytes,
    long PagedPoolBytes,
    long NonPagedPoolBytes,
    int ProcessCount,
    int ThreadCount,
    int HandleCount);
