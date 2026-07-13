namespace TaskLens.Core.Services;

/// <summary>
/// Pure parsing/aggregation for PDH <c>\GPU Engine(*)\Utilization Percentage</c> counter instances.
/// Instance names look like <c>pid_1234_luid_0x..._phys_0_eng_0_engtype_3D</c>; this extracts the
/// PID and aggregates per-PID as the max across all engine instances (matches Task Manager,
/// research.md §3). Lives in Core so it's unit-tested on Linux; the P/Invoke that reads real PDH
/// counter values is <c>PdhGpuProcessService</c> in the App project.
/// </summary>
public static class GpuEngineAggregator
{
    /// <summary>Extracts the PID from a <c>GPU Engine</c> instance name (<c>pid_1234_...</c>).</summary>
    public static bool TryParsePid(string instanceName, out int pid)
    {
        pid = 0;
        const string prefix = "pid_";
        if (!instanceName.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var start = prefix.Length;
        var end = instanceName.IndexOf('_', start);
        var digits = end < 0 ? instanceName[start..] : instanceName[start..end];
        return digits.Length > 0 && int.TryParse(digits, out pid);
    }

    /// <summary>Per-PID max utilization % across all engine instances belonging to that PID.</summary>
    public static IReadOnlyDictionary<int, double> AggregateMaxByPid(
        IEnumerable<(string InstanceName, double Value)> counters)
    {
        var result = new Dictionary<int, double>();
        foreach (var (instanceName, value) in counters)
        {
            if (!TryParsePid(instanceName, out var pid))
            {
                continue;
            }

            if (!result.TryGetValue(pid, out var max) || value > max)
            {
                result[pid] = value;
            }
        }

        return result;
    }
}
