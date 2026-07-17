using CommunityToolkit.Mvvm.ComponentModel;
using TaskLens.Core.Models;

namespace TaskLens.Core.ViewModels;

/// <summary>
/// One mutable row in the process list. Identity is <c>(Pid, StartTimeUtc)</c> and never changes;
/// metric properties are updated in place per snapshot so the row object (and its bindings) survive
/// across ticks — only changed values raise <c>PropertyChanged</c>.
/// </summary>
public sealed partial class ProcessRowViewModel : ObservableObject
{
    public ProcessRowViewModel(int pid, DateTime startTimeUtc, string name)
    {
        Pid = pid;
        StartTimeUtc = startTimeUtc;
        this.name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public int Pid { get; }

    public DateTime StartTimeUtc { get; }

    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private double cpuPercent;

    [ObservableProperty]
    private double gpuPercent;

    [ObservableProperty]
    private long workingSetBytes;

    [ObservableProperty]
    private double ioReadBytesPerSecond;

    [ObservableProperty]
    private double ioWriteBytesPerSecond;

    [ObservableProperty]
    private ProcessGroup group;

    /// <summary>Working set as percent of total RAM — drives the Arbeitsspeicher cell tint, like
    /// the real Task Manager. Set by the list VM, which knows the snapshot's memory total.</summary>
    [ObservableProperty]
    private double memoryPercent;

    /// <summary>Title of the first visible top-level window; null for windowless processes.
    /// Feeds the real TM's expandable app rows (app → indented window-title child).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWindow))]
    private string? windowTitle;

    public bool HasWindow => WindowTitle is not null;

    /// <summary>App-row chevron state; persists across ticks because the row object does.</summary>
    [ObservableProperty]
    private bool isExpanded;

    public void Update(ProcessDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        Name = delta.Sample.Name;
        CpuPercent = delta.CpuPercent;
        GpuPercent = delta.GpuPercent;
        WorkingSetBytes = delta.Sample.WorkingSetBytes;
        IoReadBytesPerSecond = delta.IoReadBytesPerSecond;
        IoWriteBytesPerSecond = delta.IoWriteBytesPerSecond;
        Group = ProcessClassification.Classify(delta.Sample);
        WindowTitle = delta.Sample.WindowTitle;
    }
}
