using TaskLens.Core.Services;

namespace TaskLens.App.Services;

/// <summary>Real-filesystem wiring for <see cref="PawnIoDetector"/>: the actual drivers folder and <see cref="File.Exists(string)"/>.</summary>
internal static class PawnIoInstallCheck
{
    public static bool IsInstalled()
    {
        var driversDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers");
        return PawnIoDetector.IsInstalled(driversDirectory, File.Exists);
    }
}
