namespace TaskLens.Core.Models;

/// <summary>One network adapter's throughput for the current tick (plan-tm3 tm3-04). Rates are
/// computed by the metrics service from interface byte counters; link speed feeds the
/// utilization-% graphs (0 when the adapter does not report a speed).</summary>
public sealed record NetworkAdapterRate(
    string Name,
    double ReceivedBytesPerSecond,
    double SentBytesPerSecond,
    long LinkSpeedBitsPerSecond)
{
    /// <summary>Utilization in [0, 100]: total throughput over link capacity; 0 when unknown.</summary>
    public double UtilizationPercent => LinkSpeedBitsPerSecond > 0
        ? Math.Clamp((ReceivedBytesPerSecond + SentBytesPerSecond) * 8.0 / LinkSpeedBitsPerSecond * 100.0, 0, 100)
        : 0;
}
