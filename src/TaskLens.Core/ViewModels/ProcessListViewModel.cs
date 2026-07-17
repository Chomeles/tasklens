using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskLens.Core.Models;

namespace TaskLens.Core.ViewModels;

/// <summary>Sortable columns of the process list.</summary>
public enum ProcessColumn
{
    Name,
    Pid,
    Cpu,
    Gpu,
    Memory,
    IoRead,
    IoWrite,

    /// <summary>Combined read+write IO rate — the Taskmanager2 "Datenträger" column.</summary>
    Disk,
}

/// <summary>
/// Process list: filter by name (case-insensitive substring), stable sort on any column,
/// totals row aggregated over the visible rows. <see cref="ApplySnapshot"/> is called on the
/// UI thread (the engine posts via <c>IDispatcher</c>) and updates rows in place — surviving
/// processes keep their row object and the <see cref="Rows"/> collection only raises events
/// for genuine adds/removes/moves.
/// </summary>
public sealed partial class ProcessListViewModel : ObservableObject
{
    // All live rows in snapshot order — the baseline that makes sorting stable across ticks.
    private readonly List<ProcessRowViewModel> allRows = [];
    private readonly Dictionary<(int Pid, DateTime StartTimeUtc), ProcessRowViewModel> rowsByKey = [];

    /// <summary>Visible rows: filtered, sorted. Mutated minimally, never rebuilt.</summary>
    public ObservableCollection<ProcessRowViewModel> Rows { get; } = [];

    // Real Task-Manager group order: Apps first, then background, then Windows processes.
    private static readonly ProcessGroup[] GroupOrder =
        [ProcessGroup.Apps, ProcessGroup.Background, ProcessGroup.System];

    /// <summary>Same rows as <see cref="Rows"/>, bucketed into Apps / Hintergrundprozesse /
    /// Windows-Prozesse sections for the Prozesse page's grouped list. Sections are persistent
    /// (reconciled in place) so collapse state and bindings survive ticks.</summary>
    public ObservableCollection<ProcessGroupSection> GroupedRows { get; } =
        [.. GroupOrder.Select(g => new ProcessGroupSection(g))];

    /// <summary>Aggregate totals over the visible rows, updated in place.</summary>
    public ProcessRowViewModel Totals { get; } = new(0, default, "Total");

    [ObservableProperty]
    private string filter = "";

    [ObservableProperty]
    private ProcessColumn sortColumn = ProcessColumn.Cpu;

    [ObservableProperty]
    private bool sortDescending = true;

    /// <summary>System-wide CPU load, as shown big-and-right-aligned above the CPU column header.</summary>
    [ObservableProperty]
    private double systemCpuPercent;

    /// <summary>System-wide memory-used percent, above the Arbeitsspeicher column header.</summary>
    [ObservableProperty]
    private double systemMemoryPercent;

    partial void OnFilterChanged(string value) => RefreshView();

    partial void OnSortColumnChanged(ProcessColumn value) => RefreshView();

    partial void OnSortDescendingChanged(bool value) => RefreshView();

    /// <summary>Column-header click: re-click toggles direction, new column sorts descending (Name: ascending).</summary>
    [RelayCommand]
    private void SortBy(ProcessColumn column)
    {
        if (SortColumn == column)
        {
            SortDescending = !SortDescending;
        }
        else
        {
            SortColumn = column;
            SortDescending = column != ProcessColumn.Name;
        }
    }

    /// <summary>Applies one snapshot as a batch: update surviving rows in place, add new, drop exited.</summary>
    public void ApplySnapshot(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        SystemCpuPercent = snapshot.CpuTotalPercent;
        SystemMemoryPercent = snapshot.MemoryTotalBytes > 0
            ? snapshot.MemoryUsedBytes * 100.0 / snapshot.MemoryTotalBytes
            : 0;

        allRows.Clear();
        var seen = new HashSet<(int Pid, DateTime StartTimeUtc)>();
        foreach (var delta in snapshot.Processes)
        {
            var key = (delta.Sample.Pid, delta.Sample.StartTimeUtc);
            if (!rowsByKey.TryGetValue(key, out var row))
            {
                row = new ProcessRowViewModel(delta.Sample.Pid, delta.Sample.StartTimeUtc, delta.Sample.Name);
                rowsByKey[key] = row;
            }

            row.Update(delta);
            row.MemoryPercent = snapshot.MemoryTotalBytes > 0
                ? delta.Sample.WorkingSetBytes * 100.0 / snapshot.MemoryTotalBytes
                : 0;
            allRows.Add(row);
            seen.Add(key);
        }

        foreach (var key in rowsByKey.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            rowsByKey.Remove(key);
        }

        RefreshView();
    }

    private void RefreshView()
    {
        var target = Sort(allRows.Where(MatchesFilter)).ToList();
        CollectionReconciler.Reconcile(Rows, target);
        UpdateTotals(target);
        RefreshGroups(target);
    }

    private void RefreshGroups(List<ProcessRowViewModel> visible)
    {
        foreach (var section in GroupedRows)
        {
            CollectionReconciler.Reconcile(section, visible.Where(r => r.Group == section.Group).ToList());
        }
    }

    private bool MatchesFilter(ProcessRowViewModel row) =>
        Filter.Length == 0 || row.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase);

    private IEnumerable<ProcessRowViewModel> Sort(IEnumerable<ProcessRowViewModel> rows)
    {
        Comparison<ProcessRowViewModel> compare = SortColumn switch
        {
            ProcessColumn.Name => (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase),
            ProcessColumn.Pid => (a, b) => a.Pid.CompareTo(b.Pid),
            ProcessColumn.Cpu => (a, b) => a.CpuPercent.CompareTo(b.CpuPercent),
            ProcessColumn.Gpu => (a, b) => a.GpuPercent.CompareTo(b.GpuPercent),
            ProcessColumn.Memory => (a, b) => a.WorkingSetBytes.CompareTo(b.WorkingSetBytes),
            ProcessColumn.IoRead => (a, b) => a.IoReadBytesPerSecond.CompareTo(b.IoReadBytesPerSecond),
            ProcessColumn.IoWrite => (a, b) => a.IoWriteBytesPerSecond.CompareTo(b.IoWriteBytesPerSecond),
            ProcessColumn.Disk => (a, b) =>
                (a.IoReadBytesPerSecond + a.IoWriteBytesPerSecond)
                    .CompareTo(b.IoReadBytesPerSecond + b.IoWriteBytesPerSecond),
            _ => throw new ArgumentOutOfRangeException(nameof(SortColumn), SortColumn, "Unknown column."),
        };

        var effective = compare;
        if (SortDescending)
        {
            effective = (a, b) => compare(b, a);
        }

        // OrderBy is a stable sort: equal keys keep snapshot order.
        return rows.OrderBy(r => r, Comparer<ProcessRowViewModel>.Create(effective));
    }

    private void UpdateTotals(List<ProcessRowViewModel> visible)
    {
        Totals.CpuPercent = visible.Sum(r => r.CpuPercent);
        Totals.GpuPercent = visible.Sum(r => r.GpuPercent);
        Totals.WorkingSetBytes = visible.Sum(r => r.WorkingSetBytes);
        Totals.IoReadBytesPerSecond = visible.Sum(r => r.IoReadBytesPerSecond);
        Totals.IoWriteBytesPerSecond = visible.Sum(r => r.IoWriteBytesPerSecond);
    }
}
