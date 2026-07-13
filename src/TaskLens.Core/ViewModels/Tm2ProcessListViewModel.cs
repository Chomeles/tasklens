using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TaskLens.Core.Models;

namespace TaskLens.Core.ViewModels;

/// <summary>
/// Taskmanager2's process list: a thin join layer over the existing <see cref="ProcessListViewModel"/>
/// (<see cref="Inner"/>) — sort, filter and totals all live there and are not reimplemented here.
/// This layer only adds, per tick: a per-row CPU% sparkline and the system-wide CPU temperature /
/// package wattage / fan RPM sensor readings stamped onto every row.
/// </summary>
public sealed partial class Tm2ProcessListViewModel : ObservableObject
{
    private readonly Dictionary<ProcessRowViewModel, Tm2ProcessRowViewModel> joined = [];

    public Tm2ProcessListViewModel()
        : this(new ProcessListViewModel())
    {
    }

    public Tm2ProcessListViewModel(ProcessListViewModel inner)
    {
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <summary>The wrapped process list — bind sort/filter/totals directly to this.</summary>
    public ProcessListViewModel Inner { get; }

    /// <summary>Joined rows, mirroring <c>Inner.Rows</c>' order (filtered, sorted).</summary>
    public ObservableCollection<Tm2ProcessRowViewModel> Rows { get; } = [];

    /// <summary>Applies one snapshot: delegates to <see cref="Inner"/>, then joins sensors + history.</summary>
    public void ApplySnapshot(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Inner.ApplySnapshot(snapshot);

        var (cpuTemp, packageWatt, fanRpm) = ExtractSystemSensors(snapshot.Sensors);

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
            joined[key].Detach();
            joined.Remove(key);
        }

        Reconcile(target);
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

    /// <summary>Mutates <see cref="Rows"/> into <paramref name="target"/> with minimal remove/insert/move events.</summary>
    private void Reconcile(List<Tm2ProcessRowViewModel> target)
    {
        var wanted = new HashSet<Tm2ProcessRowViewModel>(target);
        for (var i = Rows.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(Rows[i]))
            {
                Rows.RemoveAt(i);
            }
        }

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
}
