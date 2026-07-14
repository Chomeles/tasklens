using Microsoft.UI.Xaml.Media;
using TaskLens.Core.Services;
using Windows.Foundation;

namespace TaskLens.App.Views;

/// <summary>x:Bind helper: converts Core sparkline geometry to WinUI Polyline points.</summary>
public static class SparklineRender
{
    /// <summary>Maps a sensor history to Polyline points; Core owns the math, this only converts types.</summary>
    public static PointCollection Points(IReadOnlyList<float?> history, double width, double height)
    {
        var collection = new PointCollection();
        foreach (var point in Sparkline.MapPoints(history, width, height))
        {
            collection.Add(new Point(point.X, point.Y));
        }

        return collection;
    }

    /// <summary>x:Bind helper: a [0, 1] fraction as a pixel width — drives composition bars (tm3-03).</summary>
    public static double Fraction(double fraction, double width) =>
        Math.Clamp(fraction, 0, 1) * width;
}
