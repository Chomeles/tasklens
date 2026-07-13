using TaskLens.Core.Models;
using TaskLens.Core.Services;

namespace TaskLens.Core.Tests;

public class HistoryBufferTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveCapacity_Throws(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HistoryBuffer<int>(capacity));
    }

    [Fact]
    public void StartsEmpty_WithFixedCapacity()
    {
        var buffer = new HistoryBuffer<int>(3);
        Assert.Equal(3, buffer.Capacity);
        Assert.Empty(buffer);
        Assert.Empty(buffer.ToArray());
    }

    [Fact]
    public void Add_BelowCapacity_KeepsInsertionOrder()
    {
        var buffer = new HistoryBuffer<int>(3);
        buffer.Add(1);
        buffer.Add(2);

        Assert.Equal(2, buffer.Count);
        Assert.Equal(1, buffer[0]);
        Assert.Equal(2, buffer[1]);
        Assert.Equal([1, 2], buffer.ToArray());
    }

    [Fact]
    public void Add_BeyondCapacity_WrapsAround_OverwritingOldest()
    {
        var buffer = new HistoryBuffer<int>(3);
        for (var i = 1; i <= 5; i++)
        {
            buffer.Add(i);
        }

        Assert.Equal(3, buffer.Count);
        Assert.Equal([3, 4, 5], buffer.ToArray());
    }

    [Fact]
    public void WrapAround_ManyTimes_StaysOldestFirst()
    {
        var buffer = new HistoryBuffer<int>(4);
        for (var i = 0; i < 103; i++)
        {
            buffer.Add(i);
        }

        Assert.Equal(4, buffer.Count);
        Assert.Equal([99, 100, 101, 102], buffer.ToArray());
    }

    [Fact]
    public void Enumeration_MatchesToArray()
    {
        var buffer = new HistoryBuffer<int>(2);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);

        Assert.Equal(buffer.ToArray(), buffer.ToList());
    }

    [Fact]
    public void CapacityOne_KeepsOnlyTheLatest()
    {
        var buffer = new HistoryBuffer<string>(1);
        buffer.Add("a");
        buffer.Add("b");

        Assert.Equal(["b"], buffer.ToArray());
    }

    [Fact]
    public void Indexer_OutOfRange_Throws()
    {
        var buffer = new HistoryBuffer<int>(2);
        buffer.Add(7);

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[1]);
    }
}

public class SparklineDownsampleTests
{
    [Fact]
    public void InputThatAlreadyFits_IsReturnedUnchanged()
    {
        Assert.Equal([1d, 2d, 3d], Sparkline.Downsample([1d, 2d, 3d], 10));
        Assert.Equal([1d, 2d, 3d], Sparkline.Downsample([1d, 2d, 3d], 3));
    }

    [Fact]
    public void EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(Sparkline.Downsample([], 5));
    }

    [Fact]
    public void Downsample_ReducesToExactlyMaxPoints()
    {
        var points = Enumerable.Range(0, 100).Select(i => (double)i).ToArray();
        Assert.Equal(60, Sparkline.Downsample(points, 60).Length);
    }

    [Fact]
    public void Downsample_TakesBucketMax_SoSpikesSurvive()
    {
        // 8 points → 2 buckets of 4; the spike at index 1 must not vanish.
        Assert.Equal([100d, 9d], Sparkline.Downsample([0, 100, 0, 0, 5, 3, 9, 1], 2));
    }

    [Fact]
    public void Downsample_UnevenBuckets_CoverEveryPoint()
    {
        // 7 points into 3 buckets: [1,2] [3,4] [5,6,7] → maxes 2, 4, 7.
        Assert.Equal([2d, 4d, 7d], Sparkline.Downsample([1, 2, 3, 4, 5, 6, 7], 3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveMaxPoints_Throws(int maxPoints)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Sparkline.Downsample([1d], maxPoints));
    }
}
