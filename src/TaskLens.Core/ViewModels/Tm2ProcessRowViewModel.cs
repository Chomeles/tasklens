using CommunityToolkit.Mvvm.ComponentModel;
using TaskLens.Core.Models;

namespace TaskLens.Core.ViewModels;

/// <summary>
/// Taskmanager2 process row: a thin join of the existing <see cref="ProcessRowViewModel"/> (name,
/// PID, CPU/GPU %, memory, IO — untouched) plus what Taskmanager2 adds: a per-row CPU% sparkline
/// series and the system-wide sensor readings stamped onto every row (same values across rows —
/// there is no per-process temperature/power/fan). Property-changed notifications from the inner
/// row are forwarded so bindings on the pass-through properties keep working.
/// </summary>
public sealed partial class Tm2ProcessRowViewModel : ObservableObject
{
    // ponytail: 60 points ≈ one minute at the default 1 s tick, same as DetailsViewModel/SensorRowViewModel.
    private const int HistoryCapacity = 60;

    private readonly HistoryBuffer<float?> cpuHistory = new(HistoryCapacity);

    public Tm2ProcessRowViewModel(ProcessRowViewModel inner)
    {
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        Inner.PropertyChanged += OnInnerPropertyChanged;
    }

    private void OnInnerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        OnPropertyChanged(e.PropertyName);

    /// <summary>Detaches from <see cref="Inner"/> so this wrapper can be garbage-collected once dropped.</summary>
    internal void Detach() => Inner.PropertyChanged -= OnInnerPropertyChanged;

    /// <summary>The joined-over row from the existing process list (sort/filter/totals live there).</summary>
    public ProcessRowViewModel Inner { get; }

    public int Pid => Inner.Pid;

    public DateTime StartTimeUtc => Inner.StartTimeUtc;

    public string Name => Inner.Name;

    public double CpuPercent => Inner.CpuPercent;

    public double GpuPercent => Inner.GpuPercent;

    public long WorkingSetBytes => Inner.WorkingSetBytes;

    public double IoReadBytesPerSecond => Inner.IoReadBytesPerSecond;

    public double IoWriteBytesPerSecond => Inner.IoWriteBytesPerSecond;

    /// <summary>Send+receive bytes/sec via ETW — drives the Netzwerk cell (tm2r-01).</summary>
    public double NetworkBytesPerSecond => Inner.NetworkBytesPerSecond;

    /// <summary>Working set as percent of total RAM — drives the Arbeitsspeicher cell tint.</summary>
    public double MemoryPercent => Inner.MemoryPercent;

    /// <summary>First visible window title (null = windowless) — the expandable app-row child.</summary>
    public string? WindowTitle => Inner.WindowTitle;

    public bool HasWindow => Inner.HasWindow;

    /// <summary>App-row chevron state, shared with the inner row (single source of truth).</summary>
    public bool IsExpanded
    {
        get => Inner.IsExpanded;
        set => Inner.IsExpanded = value;
    }

    [ObservableProperty]
    private float? cpuTempCelsius;

    [ObservableProperty]
    private float? packageWattage;

    [ObservableProperty]
    private float? fanRpm;

    /// <summary>Recent CPU% values for this row, oldest first — feeds the sparkline cell.</summary>
    public IReadOnlyList<float?> CpuHistory => cpuHistory;

    /// <summary>Stamps the system-wide sensor readings (same values across rows).</summary>
    internal void Stamp(float? cpuTempCelsius, float? packageWattage, float? fanRpm)
    {
        CpuTempCelsius = cpuTempCelsius;
        PackageWattage = packageWattage;
        FanRpm = fanRpm;
    }

    /// <summary>Appends this tick's CPU% to the sparkline — once per snapshot, never on a resync.</summary>
    internal void AppendHistory()
    {
        cpuHistory.Add((float)CpuPercent);
        OnPropertyChanged(nameof(CpuHistory));
    }
}
