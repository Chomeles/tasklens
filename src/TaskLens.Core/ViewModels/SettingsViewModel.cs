using CommunityToolkit.Mvvm.ComponentModel;
using TaskLens.Core.Models;
using TaskLens.Core.Services;

namespace TaskLens.Core.ViewModels;

/// <summary>
/// Settings page state. Loads once from <see cref="ISettingsStore"/> at construction; every
/// property change is saved immediately and re-raised via <see cref="Applied"/> so a live host
/// (e.g. <c>SamplingEngine</c>) can apply the change without restarting.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore store;
    private bool loading = true;

    public SettingsViewModel(ISettingsStore store)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        var loaded = store.Load();
        loading = true;
        RefreshIntervalSeconds = loaded.RefreshInterval.TotalSeconds;
        TemperatureUnit = loaded.TemperatureUnit;
        CpuNormalization = loaded.CpuNormalization;
        Theme = loaded.Theme;
        StartPage = loaded.StartPage;
        loading = false;
    }

    /// <summary>Raised after a change is persisted, with the new settings snapshot.</summary>
    public event Action<Settings>? Applied;

    [ObservableProperty]
    private double refreshIntervalSeconds;

    [ObservableProperty]
    private TemperatureUnit temperatureUnit;

    [ObservableProperty]
    private CpuPercentNormalization cpuNormalization;

    [ObservableProperty]
    private AppTheme theme;

    [ObservableProperty]
    private StartPage startPage;

    /// <summary>0/1 view of <see cref="TemperatureUnit"/> for XAML <c>ComboBox.SelectedIndex</c> binding.</summary>
    public int TemperatureUnitIndex
    {
        get => (int)TemperatureUnit;
        set => TemperatureUnit = (TemperatureUnit)value;
    }

    /// <summary>0/1 view of <see cref="CpuNormalization"/> for XAML <c>ComboBox.SelectedIndex</c> binding.</summary>
    public int CpuNormalizationIndex
    {
        get => (int)CpuNormalization;
        set => CpuNormalization = (CpuPercentNormalization)value;
    }

    /// <summary>0/1/2 view of <see cref="Theme"/> for the App-Theme ComboBox.</summary>
    public int ThemeIndex
    {
        get => (int)Theme;
        set => Theme = (AppTheme)value;
    }

    /// <summary>0..6 view of <see cref="StartPage"/> for the Standardstartseite ComboBox.</summary>
    public int StartPageIndex
    {
        get => (int)StartPage;
        set => StartPage = (StartPage)value;
    }

    partial void OnRefreshIntervalSecondsChanged(double value) => SaveAndApply();

    partial void OnTemperatureUnitChanged(TemperatureUnit value) => SaveAndApply();

    partial void OnCpuNormalizationChanged(CpuPercentNormalization value) => SaveAndApply();

    partial void OnThemeChanged(AppTheme value)
    {
        OnPropertyChanged(nameof(ThemeIndex));
        SaveAndApply();
    }

    partial void OnStartPageChanged(StartPage value)
    {
        OnPropertyChanged(nameof(StartPageIndex));
        SaveAndApply();
    }

    private void SaveAndApply()
    {
        if (loading)
        {
            return;
        }

        // ponytail: WinUI NumberBox reports an emptied box as NaN even with Minimum set.
        var refreshSeconds = double.IsNaN(RefreshIntervalSeconds) ? 0.1 : RefreshIntervalSeconds;
        var settings = new Settings
        {
            RefreshInterval = TimeSpan.FromSeconds(Math.Max(0.1, refreshSeconds)),
            TemperatureUnit = TemperatureUnit,
            CpuNormalization = CpuNormalization,
            Theme = Theme,
            StartPage = StartPage,
        };
        store.Save(settings);
        Applied?.Invoke(settings);
    }
}
