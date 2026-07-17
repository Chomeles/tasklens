using TaskLens.App.Services.Interop;
using TaskLens.Core.Models;

namespace TaskLens.App.Services;

/// <summary>
/// _Total physical-disk counters via PDH (tm3-05): % Disk Time and Avg. Disk sec/Transfer —
/// the real TM's Aktive Zeit / Durchschnittliche Antwortzeit. Rate counters need two collects;
/// the first tick reports null and every later tick uses the interval since the previous one.
/// English counter paths (PdhAddEnglishCounter) keep this locale-independent.
/// </summary>
internal sealed class PdhDiskMetrics : IDisposable
{
    private nint query;
    private nint activeCounter;
    private nint responseCounter;
    private bool primed;
    private bool broken;

    public DiskDetails? Sample()
    {
        if (broken)
        {
            return null;
        }

        if (query == 0)
        {
            if (Pdh.PdhOpenQuery(null, 0, out query) != 0
                || Pdh.PdhAddEnglishCounter(query, @"\PhysicalDisk(_Total)\% Disk Time", 0, out activeCounter) != 0
                || Pdh.PdhAddEnglishCounter(query, @"\PhysicalDisk(_Total)\Avg. Disk sec/Transfer", 0, out responseCounter) != 0)
            {
                broken = true; // no PhysicalDisk counters on this box — panel shows dashes
                return null;
            }
        }

        if (Pdh.PdhCollectQueryData(query) != 0)
        {
            return null;
        }

        if (!primed)
        {
            primed = true; // rate counters need a previous collect
            return null;
        }

        if (Pdh.PdhGetFormattedCounterValue(activeCounter, Pdh.PdhFmtDouble, 0, out var active) != 0
            || Pdh.PdhGetFormattedCounterValue(responseCounter, Pdh.PdhFmtDouble, 0, out var response) != 0)
        {
            return null;
        }

        return new DiskDetails(Math.Clamp(active.DoubleValue, 0, 100), Math.Max(0, response.DoubleValue));
    }

    public void Dispose()
    {
        if (query != 0)
        {
            Pdh.PdhCloseQuery(query);
            query = 0;
        }
    }
}
