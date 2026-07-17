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

    /// <summary>Disk-cell tint from raw IO rates — one call because x:Bind can't nest functions.</summary>
    public static Brush DiskBrush(double readBytesPerSecond, double writeBytesPerSecond) =>
        Brush(HeatMap.DiskPercent(readBytesPerSecond, writeBytesPerSecond));

    /// <summary>Network-cell tint from the raw byte rate — one call because x:Bind can't nest functions.</summary>
    public static Brush NetworkBrush(double bytesPerSecond) =>
        Brush(HeatMap.NetworkPercent(bytesPerSecond));
}
