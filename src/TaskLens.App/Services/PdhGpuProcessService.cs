using System.Runtime.InteropServices;
using TaskLens.App.Services.Interop;
using TaskLens.Core.Services;

namespace TaskLens.App.Services;

/// <summary>
/// <see cref="IGpuProcessService"/> backed by one long-lived PDH wildcard query on
/// <c>\GPU Engine(*)\Utilization Percentage</c> (research.md §3). The query and counter handle are
/// opened once and reused for the process lifetime — rebuilding per tick is the expensive mistake
/// PDH wildcard expansion punishes. Instance-name parsing and per-PID max-across-engines
/// aggregation live in Core's <see cref="GpuEngineAggregator"/> so they're unit-tested on Linux.
/// </summary>
internal sealed class PdhGpuProcessService : IGpuProcessService, IDisposable
{
    private const string CounterPath = @"\GPU Engine(*)\Utilization Percentage";
    private static readonly IReadOnlyDictionary<int, double> Empty = new Dictionary<int, double>();

    private nint query;
    private nint counter;
    private bool available;

    public PdhGpuProcessService()
    {
        try
        {
            ThrowIfError(Pdh.PdhOpenQuery(null, 0, out query));
            ThrowIfError(Pdh.PdhAddEnglishCounter(query, CounterPath, 0, out counter));
            available = true;
        }
        catch (Exception)
        {
            // ponytail: no GPU Engine counter set (older WDDM driver, headless system) — degrade
            // permanently to an empty map instead of retrying every tick (research.md §4).
            available = false;
        }
    }

    public IReadOnlyDictionary<int, double> SampleGpuPercentByPid()
    {
        if (!available)
        {
            return Empty;
        }

        try
        {
            // First collect after PdhOpenQuery (and every collect for a rate counter) needs a prior
            // sample to compute against; PdhGetFormattedCounterArray legitimately errors until then.
            if (Pdh.PdhCollectQueryData(query) != 0)
            {
                return Empty;
            }

            return GpuEngineAggregator.AggregateMaxByPid(ReadCounters());
        }
        catch (Exception)
        {
            available = false;
            return Empty;
        }
    }

    private IEnumerable<(string InstanceName, double Value)> ReadCounters()
    {
        var bufferSize = 0;
        var itemCount = 0;
        var status = Pdh.PdhGetFormattedCounterArray(counter, Pdh.PdhFmtDouble, ref bufferSize, ref itemCount, 0);
        if (status != Pdh.PdhMoreData || bufferSize <= 0)
        {
            yield break;
        }

        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            ThrowIfError(Pdh.PdhGetFormattedCounterArray(counter, Pdh.PdhFmtDouble, ref bufferSize, ref itemCount, buffer));

            var itemSize = Marshal.SizeOf<Pdh.FormattedCounterValueItem>();
            for (var i = 0; i < itemCount; i++)
            {
                var item = Marshal.PtrToStructure<Pdh.FormattedCounterValueItem>(buffer + i * itemSize);
                if (item.CStatus != 0 || item.SzName == 0)
                {
                    continue; // instance had no valid data this sample
                }

                var name = Marshal.PtrToStringUni(item.SzName) ?? string.Empty;
                yield return (name, item.DoubleValue);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void ThrowIfError(uint status)
    {
        if (status != 0)
        {
            throw new InvalidOperationException($"PDH call failed: 0x{status:X8}");
        }
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
