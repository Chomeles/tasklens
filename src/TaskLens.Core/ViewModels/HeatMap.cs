namespace TaskLens.Core.ViewModels;

/// <summary>
/// Real Task-Manager cell shading: pale yellow at low load, ramping to orange at high load.
/// Pure value→ARGB math, no UI types, so it is testable on Linux; the App layer wraps the
/// returned bytes into a platform Brush.
/// </summary>
public static class HeatMap
{
    // Real TM ramp: transparent below ~10%, pale yellow around mid load, saturated orange at 100%.
    private const byte MaxAlpha = 160;
    private static readonly (byte R, byte G, byte B) LowColor = (255, 251, 158); // pale yellow
    private static readonly (byte R, byte G, byte B) HighColor = (255, 140, 0); // orange

    /// <summary>
    /// Maps a 0–100 percent value to an ARGB color (0xAARRGGBB). Below 1% is fully transparent
    /// (no tint on idle rows, matching the real app); alpha and hue both ramp up with load.
    /// </summary>
    public static uint CellArgb(double percent)
    {
        var clamped = Math.Clamp(percent, 0, 100);
        if (clamped < 1)
        {
            return 0;
        }

        var t = clamped / 100.0;
        var alpha = (byte)(MaxAlpha * t);
        var r = Lerp(LowColor.R, HighColor.R, t);
        var g = Lerp(LowColor.G, HighColor.G, t);
        var b = Lerp(LowColor.B, HighColor.B, t);
        return ((uint)alpha << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
    }

    private static byte Lerp(byte from, byte to, double t) => (byte)(from + ((to - from) * t));

    // ponytail: fixed full-scale for the disk-cell tint — 50 MB/s sustained reads as "hot", matching
    // the real TM's feel on SATA-era disks. Calibration knob, retune after a Windows visual pass.
    private const double DiskFullScaleBytesPerSecond = 50.0 * 1024 * 1024;

    /// <summary>Disk cell heat: combined IO rate mapped onto the 0–100 tint scale.</summary>
    public static double DiskPercent(double readBytesPerSecond, double writeBytesPerSecond) =>
        Math.Clamp((readBytesPerSecond + writeBytesPerSecond) / DiskFullScaleBytesPerSecond * 100.0, 0, 100);
}
