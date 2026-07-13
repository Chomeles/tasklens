namespace TaskLens.Core.Services;

/// <summary>Pure helpers for sparkline rendering (point mapping arrives with task 09).</summary>
public static class Sparkline
{
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
