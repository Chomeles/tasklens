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

    /// <summary>Aggregate totals over the visible rows, updated in place.</summary>
    public ProcessRowViewModel Totals { get; } = new(0, default, "Total");

    [ObservableProperty]
    private string filter = "";

    [ObservableProperty]
    private ProcessColumn sortColumn = ProcessColumn.Cpu;

    [ObservableProperty]
    private bool sortDescending = true;

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
        Reconcile(target);
        UpdateTotals(target);
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

    /// <summary>Mutates <see cref="Rows"/> into <paramref name="target"/> with minimal remove/insert/move events.</summary>
    private void Reconcile(List<ProcessRowViewModel> target)
    {
        var wanted = new HashSet<ProcessRowViewModel>(target);
        for (var i = Rows.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(Rows[i]))
            {
                Rows.RemoveAt(i);
            }
        }

        // ponytail: IndexOf makes this O(n²) worst case; fine for hundreds of rows,
        // switch to an index map if profiling ever says otherwise.
        for (var i = 0; i < target.Count; i++)
        {
            if (i < Rows.Count && ReferenceEquals(Rows[i], target[i]))
            {
                continue;
            }

            var j = Rows.IndexOf(target[i]);
            if (j < 0)
            {
                Rows.Insert(i, target[i]);
            }
            else
            {
                Rows.Move(j, i);
            }
        }
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
