using TaskLens.Core.Models;

namespace TaskLens.Core.Services;

/// <summary>Typed get/set persistence for <see cref="Settings"/>.</summary>
public interface ISettingsStore
{
    /// <summary>Loads persisted settings, or <see cref="Settings.Default"/> if none/corrupt exist.</summary>
    public Settings Load();

    /// <summary>Persists the given settings, overwriting any previous file.</summary>
    public void Save(Settings settings);
}
