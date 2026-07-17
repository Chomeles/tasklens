namespace TaskLens.Core.Models;

/// <summary>System-wide physical-disk figures for one tick (plan-tm3 tm3-05): active time and
/// average transfer response, as shown in the real TM's Datenträger panel. Absent (null on the
/// snapshot) when the platform provides no disk counters.</summary>
public sealed record DiskDetails(
    double ActiveTimePercent,
    double AverageResponseSeconds);
