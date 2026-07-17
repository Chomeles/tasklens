using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskLens.Core.Models;
using TaskLens.Core.Services;

namespace TaskLens.Core.ViewModels;

/// <summary>
/// Taskmanager2's process list: a thin join layer over the existing <see cref="ProcessListViewModel"/>
/// (<see cref="Inner"/>) — sort, filter and totals all live there and are not reimplemented here.
/// This layer adds, per tick: a per-row CPU% sparkline and the system-wide CPU temperature /
/// package wattage / fan RPM sensor readings stamped onto every row — plus, since tm3-01, the
/// selection and end-task commands (Task beenden / Prozessstruktur beenden).
/// </summary>
public sealed partial class Tm2ProcessListViewModel : ObservableObject
{
    private readonly Dictionary<ProcessRowViewModel, Tm2ProcessRowViewModel> joined = [];
    private readonly IProcessActionService? actions;

    private float? cpuTemp;
    private float? packageWatt;
    private float? fanRpm;
    private bool applying;

    public Tm2ProcessListViewModel()
        : this(new ProcessListViewModel(), null)
    {
    }

    public Tm2ProcessListViewModel(ProcessListViewModel inner, IProcessActionService? actions = null)
    {
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.actions = actions;
        Inner.Rows.CollectionChanged += OnInnerRowsChanged;
        foreach (var section in Inner.GroupedRows)
        {
            GroupedRows.Add(new Tm2ProcessGroupSection(section));
            section.CollectionChanged += OnInnerRowsChanged;
        }
    }

    /// <summary>Joined rows bucketed into the real TM's Apps / Hintergrundprozesse /
    /// Windows-Prozesse sections, mirroring <c>Inner.GroupedRows</c>.</summary>
    public ObservableCollection<Tm2ProcessGroupSection> GroupedRows { get; } = [];

    /// <summary>The row the end-task commands act on; cleared when the row leaves the list.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EndTaskCommand))]
    [NotifyCanExecuteChangedFor(nameof(EndTreeCommand))]
    [NotifyCanExecuteChangedFor(nameof(EfficiencyCommand))]
    private Tm2ProcessRowViewModel? selectedRow;

    /// <summary>Error text of the last failed action; null when the last action succeeded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActionError))]
    private string? lastActionError;

    public bool HasActionError => LastActionError is not null;

    private bool CanRunAction() => actions is not null && SelectedRow is not null;

    /// <summary>Task beenden — terminates only the selected process.</summary>
    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private void EndTask() => RunAction(entireTree: false);

    /// <summary>Prozessstruktur beenden — terminates the selected process and its descendants.</summary>
    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private void EndTree() => RunAction(entireTree: true);

    /// <summary>Effizienzmodus — EcoQoS throttling for the selected process (tm3-02).</summary>
    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private void Efficiency()
    {
        if (actions is not null && SelectedRow is not null)
        {
            var result = actions.SetEfficiencyMode(SelectedRow.Pid);
            LastActionError = result.Success ? null : result.Error;
        }
    }

    /// <summary>„Neuen Task ausführen" — shell launch from the run dialog (tm3-02).</summary>
    public void RunTask(string command, bool elevated)
    {
        if (actions is not null)
        {
            var result = actions.Launch(command, elevated);
            LastActionError = result.Success ? null : result.Error;
        }
    }

    private void RunAction(bool entireTree)
    {
        if (actions is null || SelectedRow is null)
        {
            return;
        }

        var result = actions.Terminate(SelectedRow.Pid, entireTree);
        LastActionError = result.Success ? null : result.Error;
    }

    /// <summary>The wrapped process list — bind sort/filter/totals directly to this.</summary>
    public ProcessListViewModel Inner { get; }

    /// <summary>Joined rows, mirroring <c>Inner.Rows</c>' order (filtered, sorted).</summary>
    public ObservableCollection<Tm2ProcessRowViewModel> Rows { get; } = [];

    /// <summary>Applies one snapshot: delegates to <see cref="Inner"/>, then joins sensors + history.</summary>
    public void ApplySnapshot(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        (cpuTemp, packageWatt, fanRpm) = ExtractSystemSensors(snapshot.Sensors);

        // Suppress the per-event resyncs Inner's reconcile would trigger; one resync after covers them.
        applying = true;
        try
        {
            Inner.ApplySnapshot(snapshot);
        }
        finally
        {
            applying = false;
        }

        Resync();

        foreach (var row2 in Rows)
        {
            row2.AppendHistory();
        }
    }

    /// <summary>
    /// Inner refilters/resorts between ticks too (filter text, header click) — mirror immediately
    /// instead of waiting for the next snapshot.
    /// </summary>
    private void OnInnerRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // ponytail: full resync per change event, O(n) per event on a filter sweep; diff per event if it ever shows up in a profile
        if (!applying)
        {
            Resync();
        }
    }

    /// <summary>Rebuilds <see cref="Rows"/> from <c>Inner.Rows</c>, stamping cached sensor values; idempotent.</summary>
    private void Resync()
    {
        var target = new List<Tm2ProcessRowViewModel>(Inner.Rows.Count);
        foreach (var row in Inner.Rows)
        {
            if (!joined.TryGetValue(row, out var row2))
            {
                row2 = new Tm2ProcessRowViewModel(row);
                joined[row] = row2;
            }

            row2.Stamp(cpuTemp, packageWatt, fanRpm);
            target.Add(row2);
        }

        // Drop join wrappers for rows no longer visible (filtered out or exited); they are
        // recreated with fresh history if the row becomes visible again — same trade-off as the
        // rest of the app: history only accumulates while a row is on screen.
        var visible = new HashSet<ProcessRowViewModel>(Inner.Rows);
        foreach (var key in joined.Keys.Where(k => !visible.Contains(k)).ToList())
        {
            if (ReferenceEquals(joined[key], SelectedRow))
            {
                SelectedRow = null;
            }

            joined[key].Detach();
            joined.Remove(key);
        }

        CollectionReconciler.Reconcile(Rows, target);

        foreach (var section in GroupedRows)
        {
            // joined is complete for every visible row at this point; TryGetValue is belt-and-braces
            // against a mid-update event replaying before Inner finished its own group refresh.
            var sectionTarget = new List<Tm2ProcessRowViewModel>(section.Inner.Count);
            foreach (var row in section.Inner)
            {
                if (joined.TryGetValue(row, out var row2))
                {
                    sectionTarget.Add(row2);
                }
            }

            CollectionReconciler.Reconcile(section, sectionTarget);
        }
    }

    private static (float? Temp, float? Watt, float? Fan) ExtractSystemSensors(IReadOnlyList<SensorReading> sensors)
    {
        float? temp = null;
        float? watt = null;
        float? fan = null;
        foreach (var sensor in sensors)
        {
            if (temp is null && sensor.Kind == SensorKind.Temperature && sensor.HardwareKind == HardwareKind.Cpu)
            {
                temp = sensor.Value;
            }
            else if (watt is null && sensor.Kind == SensorKind.Power && sensor.HardwareKind == HardwareKind.Cpu)
            {
                watt = sensor.Value;
            }
            else if (fan is null && sensor.Kind == SensorKind.Fan)
            {
                fan = sensor.Value;
            }
        }

        return (temp, watt, fan);
    }
}
