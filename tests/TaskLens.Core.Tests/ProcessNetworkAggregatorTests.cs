using TaskLens.Core.Services;

namespace TaskLens.Core.Tests;

public class ProcessNetworkAggregatorTests
{
    [Fact]
    public void Tick_DividesAccumulatedBytesByElapsed()
    {
        var aggregator = new ProcessNetworkAggregator();
        aggregator.Add(100, 1_000);
        aggregator.Add(100, 1_000);
        aggregator.Add(200, 500);

        var rates = aggregator.Tick(TimeSpan.FromSeconds(2));

        Assert.Equal(2, rates.Count);
        Assert.Equal(1_000, rates[100], precision: 10);
        Assert.Equal(250, rates[200], precision: 10);
    }

    [Fact]
    public void Tick_DrainsSoSilentPidsArePruned()
    {
        var aggregator = new ProcessNetworkAggregator();
        aggregator.Add(100, 4_000);
        _ = aggregator.Tick(TimeSpan.FromSeconds(1));

        // PID 100 went silent (or exited) — it must vanish from the map, not report a stale rate.
        aggregator.Add(200, 2_000);
        var rates = aggregator.Tick(TimeSpan.FromSeconds(1));

        var rate = Assert.Single(rates);
        Assert.Equal(200, rate.Key);
        Assert.Equal(2_000, rate.Value, precision: 10);
    }

    [Fact]
    public void Tick_NoTraffic_EmptyMap() =>
        Assert.Empty(new ProcessNetworkAggregator().Tick(TimeSpan.FromSeconds(1)));

    [Fact]
    public void Tick_ZeroOrNegativeElapsed_EmptyMapAndStillDrains()
    {
        var aggregator = new ProcessNetworkAggregator();
        aggregator.Add(100, 1_000);

        Assert.Empty(aggregator.Tick(TimeSpan.Zero));
        // The zero-elapsed tick drained the pending bytes — they must not leak into the next tick.
        Assert.Empty(aggregator.Tick(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Add_IgnoresNonPositiveByteCounts()
    {
        var aggregator = new ProcessNetworkAggregator();
        aggregator.Add(100, 0);
        aggregator.Add(100, -5);

        Assert.Empty(aggregator.Tick(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task Add_FromParallelThreads_LosesNoBytes()
    {
        var aggregator = new ProcessNetworkAggregator();
        const int threads = 4;
        const int addsPerThread = 10_000;

        var tasks = Enumerable.Range(0, threads).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < addsPerThread; i++)
            {
                aggregator.Add(100, 1);
            }
        })).ToArray();
        await Task.WhenAll(tasks);

        var rates = aggregator.Tick(TimeSpan.FromSeconds(1));
        Assert.Equal(threads * addsPerThread, rates[100], precision: 10);
    }
}
