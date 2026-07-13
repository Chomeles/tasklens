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
        engine.SnapshotReady += Services.GetRequiredService<ProcessListViewModel>().ApplySnapshot;
        engine.SnapshotReady += Services.GetRequiredService<SensorsViewModel>().ApplySnapshot;
        engine.SnapshotReady += Services.GetRequiredService<DetailsViewModel>().ApplySnapshot;
        _ = engine.RunAsync(CancellationToken.None); // ponytail: no CTS — the loop dies with the process

        window = new Shell();
        window.Activate();
    }

    private static ServiceProvider ConfigureServices() => new ServiceCollection()
        .AddSingleton<IClock, SystemClock>()
        .AddSingleton<IDispatcher>(new DispatcherQueueDispatcher(DispatcherQueue.GetForCurrentThread()))
        .AddSingleton<IProcessEnumerator, NtProcessEnumerator>()
#if DEBUG
        // ponytail: debug-only stub data sources — real Windows services fill the Release path in tasks 11-12
        .AddSingleton<ISensorService, StubSensorService>()
        .AddSingleton<IGpuProcessService, StubGpuProcessService>()
        .AddSingleton<ISystemMetricsService, StubSystemMetricsService>()
#endif
        .AddSingleton<SamplingEngine>()
        .AddSingleton<ProcessListViewModel>()
        .AddSingleton<SensorsViewModel>()
        .AddSingleton<DetailsViewModel>()
        .BuildServiceProvider();
}
