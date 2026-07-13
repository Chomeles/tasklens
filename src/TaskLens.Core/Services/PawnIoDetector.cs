namespace TaskLens.Core.Services;

/// <summary>
/// Detects whether the PawnIO driver (the kernel driver LHM uses for hardware sensor access,
/// research.md §"WinRing0 is dead, PawnIO is the present") is installed. This is a separate,
/// cheap file-existence check run once at startup for the first-run banner — independent of
/// the per-tick <see cref="Models.SensorAvailability"/> the sensor service reports later.
/// The file check itself is injected so this stays pure and Linux-testable.
/// </summary>
public static class PawnIoDetector
{
    public const string DriverFileName = "PawnIO.sys";

    /// <summary>True if the PawnIO driver file exists directly under <paramref name="driversDirectory"/>.</summary>
    public static bool IsInstalled(string driversDirectory, Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(driversDirectory);
        ArgumentNullException.ThrowIfNull(fileExists);
        return fileExists(Path.Combine(driversDirectory, DriverFileName));
    }
}
