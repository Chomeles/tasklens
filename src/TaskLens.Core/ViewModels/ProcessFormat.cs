using System.Globalization;
using TaskLens.Core.Models;

namespace TaskLens.Core.ViewModels;

/// <summary>
/// German (de-DE) display formatting — the real German Task Manager shows decimal commas. Static so XAML can call it
/// via x:Bind function bindings; lives in Core so it unit-tests on Linux.
/// </summary>
public static class ProcessFormat
{
    /// <summary>Display culture for every metric cell: de-DE, like the real German Task Manager.</summary>
    public static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("de-DE");

    private static readonly string[] ByteUnits = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>"12,5" — bare number; the column header carries the % unit.</summary>
    public static string Percent(double value) => value.ToString("0.0", DisplayCulture);

    /// <summary>"64,0 MB" — binary-scaled byte count.</summary>
    public static string Bytes(long value) => Scale(value);

    /// <summary>"2,1%" — cell text with the % sign inline, like the real TM's metric cells.</summary>
    public static string CellPercent(double value) =>
        value.ToString("0.0", DisplayCulture) + "%";

    /// <summary>"4%" — integer percent for the big column-header aggregate.</summary>
    public static string HeaderPercent(double value) =>
        value.ToString("0", DisplayCulture) + "%";

    /// <summary>"1,2 MBit/s" — bit rate from a byte rate, decimal-scaled like the real TM.</summary>
    public static string BitRate(double bytesPerSecond)
    {
        var bits = bytesPerSecond * 8;
        return bits >= 1_000_000_000 ? string.Create(DisplayCulture, $"{bits / 1_000_000_000:0.0} GBit/s")
            : bits >= 1_000_000 ? string.Create(DisplayCulture, $"{bits / 1_000_000:0.0} MBit/s")
            : string.Create(DisplayCulture, $"{bits / 1_000:0.0} KBit/s");
    }

    /// <summary>"1,4 ms" — average response time from seconds, the real TM's Antwortzeit format.</summary>
    public static string Milliseconds(double seconds) =>
        string.Create(DisplayCulture, $"{seconds * 1000:0.0} ms");

    /// <summary>"0:05:37:12" — d:hh:mm:ss, the real TM's Betriebszeit format.</summary>
    public static string Uptime(TimeSpan value) =>
        string.Create(DisplayCulture, $"{(int)value.TotalDays}:{value.Hours:00}:{value.Minutes:00}:{value.Seconds:00}");

    /// <summary>Disk-header aggregate from raw IO rates — one call because x:Bind can't nest functions.</summary>
    public static string DiskHeaderPercent(double readBytesPerSecond, double writeBytesPerSecond) =>
        HeaderPercent(HeatMap.DiskPercent(readBytesPerSecond, writeBytesPerSecond));

    /// <summary>"123,4 MB" — the real TM's memory column is always MB, never rescaled.</summary>
    public static string MemoryMb(long bytes) =>
        string.Create(DisplayCulture, $"{bytes / (1024.0 * 1024.0):0.0} MB");

    /// <summary>"1,5 KB/s" — binary-scaled byte rate.</summary>
    public static string Rate(double bytesPerSecond) => Scale(bytesPerSecond) + "/s";

    /// <summary>"0,1 MB/s" — combined read+write rate in fixed MB/s, like the Windows Task Manager's disk column.</summary>
    public static string DiskRate(double readBytesPerSecond, double writeBytesPerSecond) =>
        string.Create(
            DisplayCulture,
            $"{(readBytesPerSecond + writeBytesPerSecond) / (1024.0 * 1024.0):0.0} MB/s");

    /// <summary>"1:03:07" — h:mm:ss with unpadded total hours (26h stays 26, no day rollover), like the Task Manager's CPU-Zeit column.</summary>
    public static string CpuTime(TimeSpan value) =>
        string.Create(DisplayCulture, $"{(long)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}");

    /// <summary>"54,0 °C"; "—" without a reading.</summary>
    // ponytail: fixed °C, ignores TemperatureUnit; thread the setting through the Tm2 rows if the
    // Leistung page (tm2-04) makes the mismatch visible.
    public static string Temperature(float? value) => SensorRowViewModel.Format(SensorKind.Temperature, value);

    /// <summary>"45,2 W"; "—" without a reading.</summary>
    public static string Power(float? value) => SensorRowViewModel.Format(SensorKind.Power, value);

    /// <summary>"1200 RPM"; "—" without a reading.</summary>
    public static string Fan(float? value) => SensorRowViewModel.Format(SensorKind.Fan, value);

    /// <summary>"87,4 %"; "—" without a reading — the nullable-% caption next to a history graph.</summary>
    public static string Load(float? value) => SensorRowViewModel.Format(SensorKind.Load, value);

    /// <summary>
    /// Column-header text with a direction arrow on the active sort column.
    /// <paramref name="column"/> is a <see cref="ProcessColumn"/> name — a string because
    /// x:Bind function arguments only support string/number/bool constants.
    /// </summary>
    public static string Header(string title, string column, ProcessColumn activeColumn, bool descending) =>
        Enum.Parse<ProcessColumn>(column) == activeColumn
            ? title + (descending ? " ▼" : " ▲")
            : title;

    private static string Scale(double value)
    {
        var unit = 0;
        while (value >= 1024 && unit < ByteUnits.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return string.Create(DisplayCulture, $"{value:0.0} {ByteUnits[unit]}");
    }
}
