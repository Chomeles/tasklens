using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TaskLens.Core.Models;

namespace TaskLens.Core.ViewModels;

/// <summary>
/// One App-Verlauf row: all processes sharing a name (Win11 groups app history per app), with the
/// CPU time and IO bytes they accumulated since Taskmanager2 started. Rows never disappear — app
/// history is historical, exited processes keep their contribution via the retired accumulator.
/// </summary>
public sealed partial class Tm2AppHistoryRowViewModel : ObservableObject
{
    public Tm2AppHistoryRowViewModel(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public string Name { get; }

    [ObservableProperty]
    private TimeSpan cpuTime;

    [ObservableProperty]
    private long ioReadBytes;

    [ObservableProperty]
    private long ioWriteBytes;

    /// <summary>Contributions of exited process identities of this name — grows, never shrinks.</summary>
    internal TimeSpan RetiredCpuTime { get; private set; }

    internal long RetiredIoReadBytes { get; private set; }

    internal long RetiredIoWriteBytes { get; private set; }

    internal void Retire(TimeSpan cpuTime, long ioReadBytes, long ioWriteBytes)
    {
        RetiredCpuTime += cpuTime;
        RetiredIoReadBytes += ioReadBytes;
        RetiredIoWriteBytes += ioWriteBytes;
    }
}

/// <summary>
/// App-Verlauf: per-name aggregation of CPU time and IO read/write bytes, sorted by CPU time
/// descending (the Win11 default; fixed, no sort UI).
///
/// <c>ProcessSample.TotalCpuTime</c>/<c>IoReadBytes</c>/<c>IoWriteBytes</c> are monotonic counters
/// since <em>process</em> start (kernel+user time and the SYSTEM_PROCESS_INFORMATION transfer
/// counts; the engine derives its per-second rates from their deltas). The page promises values
/// "seit dem Start von Taskmanager2", so the counters are windowed to make that literally true:
/// identities already alive in the first snapshot are baselined against their first-seen counter
/// values; identities appearing later are new processes whose whole counter already lies inside
/// the window (baseline zero — a first-seen baseline would erase short-lived processes entirely).
///
/// Identity is (Pid, StartTimeUtc) — PID reuse starts a fresh identity that adds on top. When an
/// identity disappears, its last contribution is folded into the per-name retired accumulator so
/// history never shrinks.
/// </summary>
public sealed class Tm2AppHistoryViewModel
{
    private readonly Dictionary<(int Pid, DateTime StartTimeUtc), IdentityState> live = [];
    private readonly Dictionary<string, Tm2AppHistoryRowViewModel> rowsByName = new(StringComparer.OrdinalIgnoreCase);
    private bool firstSnapshot = true;

    /// <summary>Every app ever seen, sorted by CPU time descending; rows are never removed.</summary>
    public ObservableCollection<Tm2AppHistoryRowViewModel> Rows { get; } = [];

    /// <summary>Applies one snapshot: update identities, retire the vanished, re-aggregate per name.</summary>
    public void ApplySnapshot(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var seen = new HashSet<(int Pid, DateTime StartTimeUtc)>();
        foreach (var delta in snapshot.Processes)
        {
            var sample = delta.Sample;
            var key = (sample.Pid, sample.StartTimeUtc);
            if (live.TryGetValue(key, out var state))
            {
                state.Update(sample);
            }
            else
            {
                live[key] = new IdentityState(sample, windowToNow: firstSnapshot);
            }

            seen.Add(key);
        }

        firstSnapshot = false;

        // Exited identities fold their last contribution into their name's retired accumulator —
        // a row must never lose what a dead process contributed.
        foreach (var (key, state) in live.Where(pair => !seen.Contains(pair.Key)).ToList())
        {
            Row(state.Name).Retire(state.CpuTime, state.IoReadBytes, state.IoWriteBytes);
            live.Remove(key);
        }

        var liveSums = new Dictionary<string, (TimeSpan Cpu, long Read, long Write)>(StringComparer.OrdinalIgnoreCase);
        foreach (var state in live.Values)
        {
            _ = Row(state.Name);
            liveSums.TryGetValue(state.Name, out var sum);
            liveSums[state.Name] = (sum.Cpu + state.CpuTime, sum.Read + state.IoReadBytes, sum.Write + state.IoWriteBytes);
        }

        foreach (var row in rowsByName.Values)
        {
            liveSums.TryGetValue(row.Name, out var sum);
            row.CpuTime = row.RetiredCpuTime + sum.Cpu;
            row.IoReadBytes = row.RetiredIoReadBytes + sum.Read;
            row.IoWriteBytes = row.RetiredIoWriteBytes + sum.Write;
        }

        var target = rowsByName.Values
            .OrderByDescending(row => row.CpuTime)
            .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        CollectionReconciler.Reconcile(Rows, target);
    }

    private Tm2AppHistoryRowViewModel Row(string name)
    {
        if (!rowsByName.TryGetValue(name, out var row))
        {
            row = new Tm2AppHistoryRowViewModel(name);
            rowsByName[name] = row;
        }

        return row;
    }

    /// <summary>One process identity's windowed counters: contribution = last seen − baseline.</summary>
    private sealed class IdentityState
    {
        private readonly TimeSpan cpuBaseline;
        private readonly long readBaseline;
        private readonly long writeBaseline;

        public IdentityState(ProcessSample sample, bool windowToNow)
        {
            Name = sample.Name;
            if (windowToNow)
            {
                cpuBaseline = sample.TotalCpuTime;
                readBaseline = sample.IoReadBytes;
                writeBaseline = sample.IoWriteBytes;
            }

            Update(sample);
        }

        public string Name { get; }

        public TimeSpan CpuTime { get; private set; }

        public long IoReadBytes { get; private set; }

        public long IoWriteBytes { get; private set; }

        public void Update(ProcessSample sample)
        {
            CpuTime = sample.TotalCpuTime - cpuBaseline;
            IoReadBytes = sample.IoReadBytes - readBaseline;
            IoWriteBytes = sample.IoWriteBytes - writeBaseline;
        }
    }
}
