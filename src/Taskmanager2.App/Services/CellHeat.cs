using Microsoft.UI.Xaml.Media;
using TaskLens.Core.ViewModels;
using Windows.UI;

namespace Taskmanager2.App.Services;

/// <summary>
/// Wraps <see cref="HeatMap.CellArgb"/> (Core, pure/testable) into a WinUI <see cref="Brush"/> for
/// x:Bind function bindings on the Prozesse/Details cell backgrounds.
/// </summary>
public static class CellHeat
{
    public static Brush Brush(double percent)
    {
        var argb = HeatMap.CellArgb(percent);
        var color = Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
        return new SolidColorBrush(color);
    }
}
