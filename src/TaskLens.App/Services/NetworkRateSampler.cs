using System.Net.NetworkInformation;
using TaskLens.Core.Models;

namespace TaskLens.App.Services;

/// <summary>
/// Per-adapter throughput from <see cref="NetworkInterface"/> byte counters (tm3-04): rates are
/// deltas between calls, first call primes and reports zeros. Loopback/tunnel adapters are
/// skipped like in the real Task Manager. Counter-based, no ETW — per-process attribution stays
/// out of scope (honest zeros in the process list).
/// </summary>
internal sealed class NetworkRateSampler
{
    private readonly Dictionary<string, (long Rx, long Tx, DateTime AtUtc)> last = [];

    public IReadOnlyList<NetworkAdapterRate> Sample()
    {
        var rates = new List<NetworkAdapterRate>();
        var now = DateTime.UtcNow;
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up
                    || nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                var stats = nic.GetIPStatistics();
                double rx = 0, tx = 0;
                if (last.TryGetValue(nic.Id, out var prev))
                {
                    var seconds = (now - prev.AtUtc).TotalSeconds;
                    if (seconds > 0)
                    {
                        rx = Math.Max(0, (stats.BytesReceived - prev.Rx) / seconds);
                        tx = Math.Max(0, (stats.BytesSent - prev.Tx) / seconds);
                    }
                }

                last[nic.Id] = (stats.BytesReceived, stats.BytesSent, now);
                rates.Add(new NetworkAdapterRate(nic.Name, rx, tx, nic.Speed > 0 ? nic.Speed : 0));
            }
        }
        catch (NetworkInformationException)
        {
            // Enumeration itself failed — report nothing rather than stale data.
        }

        return rates;
    }
}
