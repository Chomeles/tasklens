using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TaskLens.Core.Models;

namespace TaskLens.Core.ViewModels;

/// <summary>
/// One left-rail entry on the Leistung page: name, formatted current value and a history series
/// (mini-graph on the rail, big graph in the main panel). The four system entries (CPU,
/// Arbeitsspeicher, Datenträger, GPU) have no <see cref="Group"/>; sensor entries carry their
/// hardware group so the main panel can list every reading. Values are pushed once per tick by
/// <see cref="Tm2PerformanceViewModel.ApplySnapshot"/>.
/// </summary>
public sealed partial class Tm2PerformanceEntryViewModel : ObservableObject
{
    // ponytail: 60 points ≈ one minute at the default 1 s tick, same as DetailsViewModel/SensorRowViewModel.
    private const int HistoryCapacity = 60;

    private readonly HistoryBuffer<float?> history = new(HistoryCapacity);

    public Tm2PerformanceEntryViewModel(string name, HardwareGroupViewModel? group = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Group = group;
    }

    public string Name { get; }

    /// <summary>The sensor hardware group behind this entry; null for the four system entries.</summary>
    public HardwareGroupViewModel? Group { get; }

    /// <summary>Every sensor row of the group, for the main panel; null for the system entries.</summary>
    public ObservableCollection<SensorRowViewModel>? Sensors => Group?.Sensors;

    [ObservableProperty]
    private string valueText = "—";

    /// <summary>Recent values, oldest first — the graph source. Grows one point per tick.</summary>
    public IReadOnlyList<float?> History => history;

    /// <summary>Per-tick update: appends even when the value is unchanged so the time axis advances.</summary>
    internal void Append(float? value, string valueText)
    {
        history.Add(value);
        ValueText = valueText;
        OnPropertyChanged(nameof(History));
    }
}

/// <summary>
/// The Arbeitsspeicher detail panel under the big graph (plan-tm3 tm3-03): the real Task
/// Manager's value block plus the composition-bar fraction. Texts fall back to em-dashes when
/// the snapshot carries no <see cref="MemoryDetails"/> (platform API failed).
/// </summary>
public sealed partial class Tm2MemoryDetailsViewModel : ObservableObject
{
    private const string None = "—";

    [ObservableProperty]
    private string inUseText = None;

    [ObservableProperty]
    private string availableText = None;

    [ObservableProperty]
    private string committedText = None;

    [ObservableProperty]
    private string cachedText = None;

    [ObservableProperty]
    private string pagedPoolText = None;

    [ObservableProperty]
    private string nonPagedPoolText = None;

    /// <summary>Used share of physical RAM in [0, 1] — drives the Speicherzusammensetzung bar.</summary>
    [ObservableProperty]
    private double usedFraction;

    internal void Update(SystemSnapshot snapshot)
    {
        InUseText = ProcessFormat.Bytes(snapshot.MemoryUsedBytes);
        AvailableText = ProcessFormat.Bytes(Math.Max(0, snapshot.MemoryTotalBytes - snapshot.MemoryUsedBytes));
        UsedFraction = snapshot.MemoryTotalBytes > 0
            ? (double)snapshot.MemoryUsedBytes / snapshot.MemoryTotalBytes
            : 0;

        if (snapshot.Memory is { } details)
        {
            CommittedText = $"{ProcessFormat.Bytes(details.CommittedBytes)} / {ProcessFormat.Bytes(details.CommitLimitBytes)}";
            CachedText = ProcessFormat.Bytes(details.CachedBytes);
            PagedPoolText = ProcessFormat.Bytes(details.PagedPoolBytes);
            NonPagedPoolText = ProcessFormat.Bytes(details.NonPagedPoolBytes);
        }
        else
        {
            CommittedText = None;
            CachedText = None;
            PagedPoolText = None;
            NonPagedPoolText = None;
        }
    }
}

/// <summary>
/// Leistung page: a Windows-11-Task-Manager-style rail of mini-graph entries — CPU,
/// Arbeitsspeicher, Datenträger, GPU <b>plus one entry per sensor hardware group</b> (the density
/// satire) — with <see cref="SelectedEntry"/> driving the main panel. Pure composition over
/// existing building blocks: the wrapped <see cref="SensorsViewModel"/> owns grouping and the
/// degradation banner, the system values come straight from the snapshot; no new sampling logic.
/// The engine must wire <see cref="ApplySnapshot"/> on this VM only — it delegates to
/// <see cref="Sensors"/>, so wiring both would apply sensors twice.
/// </summary>
public sealed partial class Tm2PerformanceViewModel : ObservableObject
{
    private const int SystemEntryCount = 4;

    // Per-adapter network entries sit between the four system entries and the sensor-group tail.
    private readonly Dictionary<string, Tm2PerformanceEntryViewModel> networkByName = [];

    private int HeadCount => SystemEntryCount + networkByName.Count;

    private readonly Tm2PerformanceEntryViewModel cpu = new("CPU");
    private readonly Tm2PerformanceEntryViewModel memory = new("Arbeitsspeicher");
    private readonly Tm2PerformanceEntryViewModel disk = new("Datenträger");
    private readonly Tm2PerformanceEntryViewModel gpu = new("GPU");

    [ObservableProperty]
    private Tm2PerformanceEntryViewModel selectedEntry;

    public Tm2PerformanceViewModel()
        : this(new SensorsViewModel())
    {
    }

    public Tm2PerformanceViewModel(SensorsViewModel sensors)
    {
        Sensors = sensors ?? throw new ArgumentNullException(nameof(sensors));
        Entries = [cpu, memory, disk, gpu];
        selectedEntry = cpu;
    }

    /// <summary>The wrapped sensor panel — grouping, unit and the degradation banner live there.</summary>
    public SensorsViewModel Sensors { get; }

    /// <summary>Rail entries: the four system ones first, then one per sensor hardware group.</summary>
    public ObservableCollection<Tm2PerformanceEntryViewModel> Entries { get; }

    /// <summary>The Arbeitsspeicher detail panel values, updated every tick.</summary>
    public Tm2MemoryDetailsViewModel MemoryDetails { get; } = new();

    /// <summary>True while the Arbeitsspeicher rail entry is selected — shows the detail panel.</summary>
    public bool IsMemorySelected => ReferenceEquals(SelectedEntry, memory);

    /// <summary>True while the CPU rail entry is selected — shows Betriebszeit/Prozessoren facts.</summary>
    public bool IsCpuSelected => ReferenceEquals(SelectedEntry, cpu);

    /// <summary>True while the Datenträger rail entry is selected — shows the disk facts (tm3-05).</summary>
    public bool IsDiskSelected => ReferenceEquals(SelectedEntry, disk);

    /// <summary>"1,5 KB/s" — system read rate (sum of per-process IO, the honest value).</summary>
    [ObservableProperty]
    private string diskReadText = "—";

    /// <summary>"0,3 KB/s" — system write rate.</summary>
    [ObservableProperty]
    private string diskWriteText = "—";

    /// <summary>"12,3 %" — physical-disk active time; dash without PDH counters.</summary>
    [ObservableProperty]
    private string diskActiveText = "—";

    /// <summary>"1,4 ms" — average transfer response; dash without PDH counters.</summary>
    [ObservableProperty]
    private string diskResponseText = "—";

    /// <summary>Total capacity of all fixed drives — real DriveInfo data, computed once.</summary>
    public string DiskCapacityText { get; } = ComputeDiskCapacity();

    private static string ComputeDiskCapacity()
    {
        try
        {
            var total = DriveInfo.GetDrives()
                .Where(d => d is { DriveType: DriveType.Fixed, IsReady: true })
                .Sum(d => d.TotalSize);
            return total > 0 ? ProcessFormat.Bytes(total) : "—";
        }
        catch (IOException)
        {
            return "—";
        }
    }

    /// <summary>"0:05:37:12" — time since boot (d:hh:mm:ss), ticking like the real TM.</summary>
    [ObservableProperty]
    private string uptimeText = "—";

    /// <summary>Logical processor count — constant, real data.</summary>
    public string LogicalProcessorsText { get; } =
        Environment.ProcessorCount.ToString(ProcessFormat.DisplayCulture);

    partial void OnSelectedEntryChanged(Tm2PerformanceEntryViewModel value)
    {
        OnPropertyChanged(nameof(IsMemorySelected));
        OnPropertyChanged(nameof(IsCpuSelected));
        OnPropertyChanged(nameof(IsDiskSelected));
    }

    /// <summary>Applies one snapshot: delegates to <see cref="Sensors"/>, then composes the rail.</summary>
    public void ApplySnapshot(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Sensors.ApplySnapshot(snapshot);
        UptimeText = ProcessFormat.Uptime(TimeSpan.FromMilliseconds(Environment.TickCount64));
        SyncGroupEntries();

        var cpuPercent = (float)snapshot.CpuTotalPercent;
        cpu.Append(cpuPercent, SensorRowViewModel.Format(SensorKind.Load, cpuPercent));

        var memoryPercent = snapshot.MemoryTotalBytes > 0
            ? (float)(snapshot.MemoryUsedBytes * 100.0 / snapshot.MemoryTotalBytes)
            : (float?)null;
        memory.Append(
            memoryPercent,
            $"{ProcessFormat.Bytes(snapshot.MemoryUsedBytes)} / {ProcessFormat.Bytes(snapshot.MemoryTotalBytes)}");
        MemoryDetails.Update(snapshot);

        double readRate = 0;
        double writeRate = 0;
        double gpuSum = 0;
        foreach (var delta in snapshot.Processes)
        {
            readRate += delta.IoReadBytesPerSecond;
            writeRate += delta.IoWriteBytesPerSecond;
            gpuSum += delta.GpuPercent;
        }

        // SystemSnapshot has no system-wide disk counter — the sum of the per-process IO rates is
        // the honest system value (it only misses IO not attributed to any process).
        // With PDH disk counters the graph shows Aktive Zeit % like the real TM; without them it
        // falls back to the per-process IO-rate sum (honest, but a rate, not a percent).
        if (snapshot.Disk is { } diskDetails)
        {
            disk.Append((float)diskDetails.ActiveTimePercent, SensorRowViewModel.Format(SensorKind.Load, (float)diskDetails.ActiveTimePercent));
            DiskActiveText = SensorRowViewModel.Format(SensorKind.Load, (float)diskDetails.ActiveTimePercent);
            DiskResponseText = ProcessFormat.Milliseconds(diskDetails.AverageResponseSeconds);
        }
        else
        {
            disk.Append((float)(readRate + writeRate), ProcessFormat.DiskRate(readRate, writeRate));
            DiskActiveText = "—";
            DiskResponseText = "—";
        }

        DiskReadText = ProcessFormat.Rate(readRate);
        DiskWriteText = ProcessFormat.Rate(writeRate);

        // Honest system GPU value: prefer a real GPU load sensor when one exists; without one
        // (no admin, VM) fall back to the sum of per-process GPU% — that sum can exceed 100 when
        // processes peak on different engines, which is the price of not inventing data.
        var gpuPercent = FirstGpuLoad(snapshot.Sensors) ?? (float)gpuSum;
        gpu.Append(gpuPercent, SensorRowViewModel.Format(SensorKind.Load, gpuPercent));

        SyncNetworkEntries(snapshot.Network);
    }

    /// <summary>One rail entry per up-adapter, like the real TM's Ethernet/WLAN entries (tm3-04).
    /// Graph = utilization % of link speed; headline = send/receive bit rates.</summary>
    private void SyncNetworkEntries(IReadOnlyList<NetworkAdapterRate> adapters)
    {
        foreach (var adapter in adapters)
        {
            if (!networkByName.TryGetValue(adapter.Name, out var entry))
            {
                entry = new Tm2PerformanceEntryViewModel(adapter.Name);
                networkByName[adapter.Name] = entry;
                Entries.Insert(SystemEntryCount + networkByName.Count - 1, entry);
            }

            entry.Append(
                (float)adapter.UtilizationPercent,
                $"S: {ProcessFormat.BitRate(adapter.SentBytesPerSecond)}  E: {ProcessFormat.BitRate(adapter.ReceivedBytesPerSecond)}");
        }

        // ponytail: vanished adapters keep their (frozen) rail entry — adapters disappear so
        // rarely that removal handling is not worth the index bookkeeping; restart clears them.
    }

    /// <summary>
    /// Mirrors <c>Sensors.Groups</c> into the rail tail. Groups rebuild only on structural change
    /// (first snapshot in practice, see <see cref="SensorsViewModel.ApplySnapshot"/>), so entry
    /// instances — and the selection — survive ordinary ticks.
    /// </summary>
    private void SyncGroupEntries()
    {
        var groups = Sensors.Groups;
        if (!TailMatches(groups))
        {
            while (Entries.Count > HeadCount)
            {
                Entries.RemoveAt(Entries.Count - 1);
            }

            foreach (var group in groups)
            {
                Entries.Add(new Tm2PerformanceEntryViewModel(group.Name, group));
            }

            if (!Entries.Contains(SelectedEntry))
            {
                SelectedEntry = Entries[0];
            }
        }

        for (var i = HeadCount; i < Entries.Count; i++)
        {
            var headline = Headline(Entries[i].Group!);
            Entries[i].Append(headline.Value, headline.ValueText);
        }
    }

    private bool TailMatches(ObservableCollection<HardwareGroupViewModel> groups)
    {
        if (Entries.Count != HeadCount + groups.Count)
        {
            return false;
        }

        for (var i = 0; i < groups.Count; i++)
        {
            if (!ReferenceEquals(Entries[HeadCount + i].Group, groups[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Rail headline of a group: its first load sensor (utilization, like the real rail), else its first sensor.</summary>
    private static SensorRowViewModel Headline(HardwareGroupViewModel group) =>
        group.Sensors.FirstOrDefault(s => s.Kind == SensorKind.Load) ?? group.Sensors[0];

    private static float? FirstGpuLoad(IReadOnlyList<SensorReading> sensors)
    {
        // LHM lists several GPU load sensors (Core, Memory Controller, Video Engine …);
        // "GPU Core" is the utilization headline, anything else only a fallback.
        float? fallback = null;
        foreach (var sensor in sensors)
        {
            if (sensor.Kind == SensorKind.Load && sensor.HardwareKind == HardwareKind.Gpu && sensor.Value is { } value)
            {
                if (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                {
                    return value;
                }

                fallback ??= value;
            }
        }

        return fallback;
    }
}
