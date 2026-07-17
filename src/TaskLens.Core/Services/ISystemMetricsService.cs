namespace TaskLens.Core.Services;

/// <summary>System-wide totals for one tick. Invariants are enforced by <c>SystemSnapshot</c>.</summary>
public sealed record SystemMetrics(
    double CpuTotalPercent,
    long MemoryUsedBytes,
    long MemoryTotalBytes,
    Models.MemoryDetails? Memory = null,
    IReadOnlyList<Models.NetworkAdapterRate>? Network = null);

/// <summary>Reads system-wide metrics (total CPU, RAM).</summary>
public interface ISystemMetricsService
{
    public SystemMetrics Sample();
}
