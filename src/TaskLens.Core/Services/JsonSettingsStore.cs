using System.Text.Json;
using TaskLens.Core.Models;

namespace TaskLens.Core.Services;

/// <summary>
/// <see cref="ISettingsStore"/> backed by a JSON file, one per <paramref name="directoryPath"/>
/// (defaults to <c>%LocalAppData%\TaskLens</c>). A missing, unreadable, or corrupt file is treated
/// as "no settings yet" and recovers to <see cref="Settings.Default"/> rather than throwing.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private const string FileName = "settings.json";

    private readonly string filePath;

    public JsonSettingsStore(string? directoryPath = null)
    {
        var directory = directoryPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TaskLens");
        filePath = Path.Combine(directory, FileName);
    }

    public Settings Load()
    {
        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<Settings>(json) ?? Settings.Default;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // ponytail: missing file (first run) and corrupt file (crash mid-write, manual edit)
            // both recover to defaults instead of crashing the app on startup.
            return Settings.Default;
        }
    }

    public void Save(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, JsonSerializer.Serialize(settings));
    }
}
