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

public class SparklineMapPointsTests
{
    [Fact]
    public void FewerThanTwoReadings_YieldNoPoints()
    {
        Assert.Empty(Sparkline.MapPoints([], 100, 20));
        Assert.Empty(Sparkline.MapPoints([5f], 100, 20));
        Assert.Empty(Sparkline.MapPoints([null, null], 100, 20));
        Assert.Empty(Sparkline.MapPoints([null, 5f, null], 100, 20));
    }

    [Fact]
    public void MinMax_MapToBottomAndTopEdges()
    {
        Assert.Equal(
            [new SparkPoint(0, 20), new SparkPoint(100, 0)],
            Sparkline.MapPoints([0f, 10f], 100, 20));
    }

    [Fact]
    public void Values_ScaleLinearlyBetweenMinAndMax()
    {
        Assert.Equal(
            [new SparkPoint(0, 20), new SparkPoint(50, 10), new SparkPoint(100, 0)],
            Sparkline.MapPoints([0f, 5f, 10f], 100, 20));
    }

    [Fact]
    public void FlatSeries_DrawsAtMidHeight()
    {
        Assert.Equal(
            [new SparkPoint(0, 10), new SparkPoint(50, 10), new SparkPoint(100, 10)],
            Sparkline.MapPoints([7f, 7f, 7f], 100, 20));
    }

    [Fact]
    public void Nulls_AreSkipped_ButKeepTheirTimeSlot()
    {
        // Index 1 has no reading: the line bridges the gap, x positions stay on the time axis.
        Assert.Equal(
            [new SparkPoint(0, 20), new SparkPoint(60, 0), new SparkPoint(90, 0)],
            Sparkline.MapPoints([0f, null, 10f, 10f], 90, 20));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(100, 0)]
    [InlineData(100, -5)]
    public void NonPositiveSize_Throws(double width, double height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Sparkline.MapPoints([1f, 2f], width, height));
    }
}
