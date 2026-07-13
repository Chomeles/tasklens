namespace TaskLens.Core.Services;

/// <summary>A sparkline vertex in output space; plain doubles so Core stays UI-free.</summary>
public readonly record struct SparkPoint(double X, double Y);

/// <summary>Pure helpers for sparkline rendering.</summary>
public static class Sparkline
{
    /// <summary>
    /// Maps a value series (oldest-first, <c>null</c> = no reading that tick) to polyline points in
    /// a <paramref name="width"/> × <paramref name="height"/> box: x from series position (nulls
    /// keep their time slot, the line bridges the gap), y linearly autoscaled so the minimum sits
    /// at the bottom edge and the maximum at the top. A flat series draws at mid-height; fewer
    /// than two readings yield no points (nothing to draw).
    /// </summary>
    public static SparkPoint[] MapPoints(IReadOnlyList<float?> values, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "height must be positive.");
        }

        var known = 0;
        var min = double.MaxValue;
        var max = double.MinValue;
        foreach (var value in values)
        {
            if (value is { } v)
            {
                known++;
                min = Math.Min(min, v);
                max = Math.Max(max, v);
            }
        }

        if (known < 2)
        {
            return [];
        }

        var points = new SparkPoint[known];
        var p = 0;
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is not { } v)
            {
                continue;
            }

            var x = i * width / (values.Count - 1);
            var y = max > min ? (max - v) / (max - min) * height : height / 2;
            points[p++] = new SparkPoint(x, y);
        }

        return points;
    }

    /// <summary>
    /// Downsamples <paramref name="points"/> (oldest-first) to at most <paramref name="maxPoints"/>
    /// values by taking the maximum of each even bucket, so short spikes stay visible.
    /// Input that already fits is returned unchanged (as a copy).
    /// </summary>
    public static double[] Downsample(IReadOnlyList<double> points, int maxPoints)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (maxPoints < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPoints), maxPoints, "maxPoints must be >= 1.");
        }

        if (points.Count <= maxPoints)
        {
            return [.. points];
        }

        // ponytail: bucket-max — cheapest shape that keeps spikes; upgrade to LTTB if fidelity matters.
        var result = new double[maxPoints];
        for (var i = 0; i < maxPoints; i++)
        {
            var start = (int)((long)i * points.Count / maxPoints);
            var end = (int)((long)(i + 1) * points.Count / maxPoints);
            var max = points[start];
            for (var j = start + 1; j < end; j++)
            {
                max = Math.Max(max, points[j]);
            }

            result[i] = max;
        }

        return result;
    }
}
