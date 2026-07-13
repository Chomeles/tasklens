using CommunityToolkit.Mvvm.ComponentModel;
using TaskLens.Core.Models;

namespace TaskLens.Core.ViewModels;

/// <summary>
/// One selected process's CPU% history plus system-wide CPU% and memory% history, as sparkline
/// series (<see cref="Services.Sparkline.MapPoints"/> maps them to Polyline points — no chart
/// library, plan.md task 13). <see cref="SelectProcess"/> picks the process (and resets its
/// history); <see cref="ApplySnapshot"/> is called once per tick with the engine's snapshot.
/// </summary>
public sealed partial class DetailsViewModel : ObservableObject
{
    // ponytail: 60 points ≈ one minute at the default 1 s tick, same as SensorRowViewModel/engine.
    private const int HistoryCapacity = 60;

    private readonly HistoryBuffer<float?> processCpuHistory = new(HistoryCapacity);
    private readonly HistoryBuffer<float?> systemCpuHistory = new(HistoryCapacity);
    private readonly HistoryBuffer<float?> systemMemoryHistory = new(HistoryCapacity);

    private int? selectedPid;
    private DateTime selectedStartTimeUtc;

    [ObservableProperty]
    private string processName = "No process selected";

    [ObservableProperty]
    private float? processCpuPercent;

    [ObservableProperty]
    private float? systemCpuPercent;

    [ObservableProperty]
    private float? systemMemoryPercent;

    /// <summary>Recent process CPU% values, oldest first; <c>null</c> for ticks the process was absent.</summary>
    public IReadOnlyList<float?> ProcessCpuHistory => processCpuHistory;

    /// <summary>Recent system-wide CPU% values, oldest first.</summary>
    public IReadOnlyList<float?> SystemCpuHistory => systemCpuHistory;

    /// <summary>Recent system-wide memory-used% values, oldest first.</summary>
    public IReadOnlyList<float?> SystemMemoryHistory => systemMemoryHistory;

    /// <summary>Selects the process to track and clears its history (new identity, fresh series).</summary>
    public void SelectProcess(int pid, DateTime startTimeUtc, string name)
    {
        selectedPid = pid;
        selectedStartTimeUtc = startTimeUtc;
        ProcessName = name ?? throw new ArgumentNullException(nameof(name));
        processCpuHistory.Clear();
        ProcessCpuPercent = null;
        OnPropertyChanged(nameof(ProcessCpuHistory));
    }

    /// <summary>
    /// Appends one tick to every series: system CPU/memory always; the process series gets the
    /// matching delta's CPU%, or <c>null</c> (process exited/not selected) — the gap keeps the
    /// time axis aligned, same convention as sensor history.
    /// </summary>
    public void ApplySnapshot(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var memoryPercent = snapshot.MemoryTotalBytes > 0
            ? (float)(snapshot.MemoryUsedBytes * 100.0 / snapshot.MemoryTotalBytes)
            : (float?)null;
        SystemCpuPercent = (float)snapshot.CpuTotalPercent;
        SystemMemoryPercent = memoryPercent;
        systemCpuHistory.Add(SystemCpuPercent);
        systemMemoryHistory.Add(memoryPercent);
        OnPropertyChanged(nameof(SystemCpuHistory));
        OnPropertyChanged(nameof(SystemMemoryHistory));

        if (selectedPid is not { } pid)
        {
            return;
        }

        float? cpu = null;
        foreach (var delta in snapshot.Processes)
        {
            if (delta.Sample.Pid == pid && delta.Sample.StartTimeUtc == selectedStartTimeUtc)
            {
                cpu = (float)delta.CpuPercent;
                break;
            }
        }

        ProcessCpuPercent = cpu;
        processCpuHistory.Add(cpu);
        OnPropertyChanged(nameof(ProcessCpuHistory));
    }
}
