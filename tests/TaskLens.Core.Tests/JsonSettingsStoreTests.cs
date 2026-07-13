using TaskLens.Core.Models;
using TaskLens.Core.Services;

namespace TaskLens.Core.Tests;

public class JsonSettingsStoreTests : IDisposable
{
    private readonly string directory =
        Path.Combine(Path.GetTempPath(), "TaskLensTests_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_NoFileYet_ReturnsDefault()
    {
        var store = new JsonSettingsStore(directory);

        Assert.Equal(Settings.Default, store.Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllFields()
    {
        var store = new JsonSettingsStore(directory);
        var settings = new Settings
        {
            RefreshInterval = TimeSpan.FromSeconds(2.5),
            TemperatureUnit = TemperatureUnit.Fahrenheit,
            CpuNormalization = CpuPercentNormalization.SingleCore,
        };

        store.Save(settings);

        Assert.Equal(settings, store.Load());
    }

    [Fact]
    public void Load_CorruptFile_RecoversToDefault()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "settings.json"), "{ not valid json ][");
        var store = new JsonSettingsStore(directory);

        Assert.Equal(Settings.Default, store.Load());
    }

    [Fact]
    public void Load_EmptyFile_RecoversToDefault()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "settings.json"), string.Empty);
        var store = new JsonSettingsStore(directory);

        Assert.Equal(Settings.Default, store.Load());
    }

    [Fact]
    public void Load_OutOfRangeRefreshInterval_RecoversToDefault()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "settings.json"),
            """{"RefreshInterval":"-00:00:01","TemperatureUnit":0,"CpuNormalization":0}""");
        var store = new JsonSettingsStore(directory);

        Assert.Equal(Settings.Default, store.Load());
    }

    [Fact]
    public void Save_CreatesDirectoryIfMissing()
    {
        var store = new JsonSettingsStore(directory);

        store.Save(Settings.Default);

        Assert.True(File.Exists(Path.Combine(directory, "settings.json")));
    }
}
