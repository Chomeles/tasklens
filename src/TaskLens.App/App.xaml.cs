using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using TaskLens.App.Services;
using TaskLens.App.Views;
using TaskLens.Core.Services;
using TaskLens.Core.ViewModels;

namespace TaskLens.App;

/// <summary>Composition root: builds the service provider, wires the engine, opens the shell.</summary>
public partial class App : Application
{
    private Window? window;

    public App()
    {
        InitializeComponent();
    }

    /// <summary>Resolved by Views for their ViewModels — the only service-locator use allowed (plan.md).</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Services = ConfigureServices();

        var engine = Services.GetRequiredService<SamplingEngine>();
        var sensorsViewModel = Services.GetRequiredService<SensorsViewModel>();
        engine.SnapshotReady += Services.GetRequiredService<ProcessListViewModel>().ApplySnapshot;
        engine.SnapshotReady += sensorsViewModel.ApplySnapshot;
        engine.SnapshotReady += Services.GetRequiredService<DetailsViewModel>().ApplySnapshot;

        var settingsViewModel = Services.GetRequiredService<SettingsViewModel>();
        engine.Interval = TimeSpan.FromSeconds(settingsViewModel.RefreshIntervalSeconds);
        engine.Normalization = settingsViewModel.CpuNormalization;
        sensorsViewModel.Unit = settingsViewModel.TemperatureUnit;
        settingsViewModel.Applied += settings =>
        {
            engine.Interval = settings.RefreshInterval;
            engine.Normalization = settings.CpuNormalization;
            sensorsViewModel.Unit = settings.TemperatureUnit;
        };

        _ = engine.RunAsync(CancellationToken.None); // ponytail: no CTS — the loop dies with the process

        window = new Shell();
        // Disposes IDisposable singletons — LhmSensorService joins its thread and runs Computer.Close() here.
        window.Closed += (_, _) => (Services as IDisposable)?.Dispose();
        window.Activate();
    }

    private static ServiceProvider ConfigureServices() => new ServiceCollection()
        .AddSingleton<IClock, SystemClock>()
        .AddSingleton<IDispatcher>(new DispatcherQueueDispatcher(DispatcherQueue.GetForCurrentThread()))
        .AddSingleton<IProcessEnumerator, NtProcessEnumerator>()
        .AddSingleton<IGpuProcessService, PdhGpuProcessService>()
#if DEBUG
        // ponytail: debug-only stub data sources — real Windows services fill the Release path
        .AddSingleton<ISensorService, StubSensorService>()
        .AddSingleton<ISystemMetricsService, StubSystemMetricsService>()
#else
        .AddSingleton<ISensorService, LhmSensorService>()
        .AddSingleton<ISystemMetricsService, WinSystemMetricsService>()
#endif
        .AddSingleton<ISettingsStore, JsonSettingsStore>()
        .AddSingleton<SamplingEngine>()
        .AddSingleton<ProcessListViewModel>()
        .AddSingleton<SensorsViewModel>()
        .AddSingleton<DetailsViewModel>()
        .AddSingleton<SettingsViewModel>()
        .AddSingleton(_ => new PawnIoBannerViewModel(PawnIoInstallCheck.IsInstalled()))
        .BuildServiceProvider();
}
