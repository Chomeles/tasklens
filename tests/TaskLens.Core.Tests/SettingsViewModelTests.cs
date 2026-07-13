using TaskLens.Core.Models;
using TaskLens.Core.Services;
using TaskLens.Core.ViewModels;

namespace TaskLens.Core.Tests;

file sealed class InMemorySettingsStore : ISettingsStore
{
    public Settings Current { get; set; } = Settings.Default;

    public int SaveCount { get; private set; }

    public Settings Load() => Current;

    public void Save(Settings settings)
    {
        Current = settings;
        SaveCount++;
    }
}

public class SettingsViewModelTests
{
    [Fact]
    public void Constructor_LoadsFromStore_WithoutSaving()
    {
        var store = new InMemorySettingsStore
        {
            Current = new Settings { TemperatureUnit = TemperatureUnit.Fahrenheit },
        };

        var vm = new SettingsViewModel(store);

        Assert.Equal(TemperatureUnit.Fahrenheit, vm.TemperatureUnit);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public void ChangingAProperty_SavesAndRaisesApplied()
    {
        var store = new InMemorySettingsStore();
        var vm = new SettingsViewModel(store);
        Settings? applied = null;
        vm.Applied += s => applied = s;

        vm.CpuNormalization = CpuPercentNormalization.SingleCore;

        Assert.Equal(1, store.SaveCount);
        Assert.Equal(CpuPercentNormalization.SingleCore, store.Current.CpuNormalization);
        Assert.Equal(CpuPercentNormalization.SingleCore, applied?.CpuNormalization);
    }

    [Fact]
    public void RefreshIntervalSeconds_BelowMinimum_ClampsInsteadOfThrowing()
    {
        var store = new InMemorySettingsStore();
        var vm = new SettingsViewModel(store);

        vm.RefreshIntervalSeconds = 0;

        Assert.True(store.Current.RefreshInterval > TimeSpan.Zero);
    }
}
