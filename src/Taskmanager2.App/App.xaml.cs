using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using TaskLens.App.Services;
using TaskLens.Core.Services;
using TaskLens.Core.ViewModels;
using Taskmanager2.App.Services;
using Taskmanager2.App.Views;

namespace Taskmanager2.App;

/// <summary>
/// Composition root. Wires the same TaskLens.Core sampling pipeline as TaskLens.App — every
/// Windows service impl here is link-compiled from TaskLens.App/Services (plan-tm2.md: no
/// duplicated logic). Remaining page-specific Tm2*ViewModels land with their tasks; pages without
/// one work against the existing Core ViewModels until then.
/// </summary>
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
        // The Tm2 wrappers delegate to their wrapped VMs (Inner ProcessListViewModel /
        // SensorsViewModel) — wiring wrapper and wrapped both would apply twice.
        engine.SnapshotReady += Services.GetRequiredService<Tm2ProcessListViewModel>().ApplySnapshot;
        engine.SnapshotReady += Services.GetRequiredService<Tm2PerformanceViewModel>().ApplySnapshot;
        engine.SnapshotReady += Services.GetRequiredService<Tm2AppHistoryViewModel>().ApplySnapshot;
        engine.SnapshotReady += Services.GetRequiredService<Tm2ServicesViewModel>().ApplySnapshot;
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
        window.Closed += (_, _) => (Services as IDisposable)?.Dispose();
        window.Activate();
    }

    private static ServiceProvider ConfigureServices() => new ServiceCollection()
        .AddSingleton<IClock, SystemClock>()
        .AddSingleton<IDispatcher>(new DispatcherQueueDispatcher(DispatcherQueue.GetForCurrentThread()))
        .AddSingleton<IProcessEnumerator, NtProcessEnumerator>()
        .AddSingleton<IGpuProcessService, PdhGpuProcessService>()
#if DEBUG
        // ponytail: debug-only stub data sources, same pattern as TaskLens.App.
        .AddSingleton<ISensorService, StubSensorService>()
        .AddSingleton<ISystemMetricsService, StubSystemMetricsService>()
        .AddSingleton<IServiceCatalog, StubServiceCatalog>()
#else
        .AddSingleton<ISensorService, LhmSensorService>()
        .AddSingleton<ISystemMetricsService, WinSystemMetricsService>()
        .AddSingleton<IServiceCatalog, ScmServiceCatalog>()
#endif
        .AddSingleton<ISettingsStore, JsonSettingsStore>()
        .AddSingleton<SamplingEngine>()
        // Factory, not open registration: constructor injection would pick the (ProcessListViewModel)
        // ctor, whose registration below resolves Tm2 again — infinite recursion at first resolve.
        .AddSingleton(_ => new Tm2ProcessListViewModel())
        // ProcessListViewModel == the instance Tm2 wraps, so both resolve to the same rows/sort state.
        .AddSingleton(sp => sp.GetRequiredService<Tm2ProcessListViewModel>().Inner)
        // Same recipe as Tm2ProcessListViewModel: factory (two ctors), wrapped VM == the Tm2 instance's.
        .AddSingleton(_ => new Tm2PerformanceViewModel())
        .AddSingleton(sp => sp.GetRequiredService<Tm2PerformanceViewModel>().Sensors)
        .AddSingleton<Tm2AppHistoryViewModel>()
        .AddSingleton<Tm2ServicesViewModel>()
        .AddSingleton<DetailsViewModel>()
        .AddSingleton<SettingsViewModel>()
        .AddSingleton(_ => new PawnIoBannerViewModel(PawnIoInstallCheck.IsInstalled()))
        .BuildServiceProvider();
}
