using System.Globalization;
using TaskLens.Core.Models;

namespace TaskLens.Core.ViewModels;

/// <summary>
/// Invariant-culture display formatting for the process list. Static so XAML can call it
/// via x:Bind function bindings; lives in Core so it unit-tests on Linux.
/// </summary>
public static class ProcessFormat
{
    private static readonly string[] ByteUnits = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>"12.5" — bare number; the column header carries the % unit.</summary>
    public static string Percent(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);

    /// <summary>"64.0 MB" — binary-scaled byte count.</summary>
    public static string Bytes(long value) => Scale(value);

    /// <summary>"1.5 KB/s" — binary-scaled byte rate.</summary>
    public static string Rate(double bytesPerSecond) => Scale(bytesPerSecond) + "/s";

    /// <summary>"0.1 MB/s" — combined read+write rate in fixed MB/s, like the Windows Task Manager's disk column.</summary>
    public static string DiskRate(double readBytesPerSecond, double writeBytesPerSecond) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{(readBytesPerSecond + writeBytesPerSecond) / (1024.0 * 1024.0):0.0} MB/s");

    /// <summary>"54.0 °C"; "—" without a reading.</summary>
    public static string Temperature(float? value) => SensorRowViewModel.Format(SensorKind.Temperature, value);

    /// <summary>"45.2 W"; "—" without a reading.</summary>
    public static string Power(float? value) => SensorRowViewModel.Format(SensorKind.Power, value);

    /// <summary>"1200 RPM"; "—" without a reading.</summary>
    public static string Fan(float? value) => SensorRowViewModel.Format(SensorKind.Fan, value);

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

        return string.Create(CultureInfo.InvariantCulture, $"{value:0.0} {ByteUnits[unit]}");
    }
}
