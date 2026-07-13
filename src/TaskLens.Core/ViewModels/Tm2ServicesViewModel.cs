using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TaskLens.Core.Models;
using TaskLens.Core.Services;

namespace TaskLens.Core.ViewModels;

/// <summary>One Dienste row. Identity is the service name; values update in place across refreshes.</summary>
public sealed partial class Tm2ServiceRowViewModel : ObservableObject
{
    public Tm2ServiceRowViewModel(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public string Name { get; }

    [ObservableProperty]
    private string displayName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PidText))]
    private int? pid;

    [ObservableProperty]
    private string description = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool running;

    /// <summary>PID column text; empty for stopped services — they have no process.</summary>
    public string PidText => Pid?.ToString() ?? "";

    /// <summary>Status column text, Win11 Task Manager wording.</summary>
    public string StatusText => Running ? "Wird ausgeführt" : "Beendet";

    internal void Update(ServiceEntry entry)
    {
        DisplayName = entry.DisplayName;
        Pid = entry.Pid;
        Description = entry.Description;
        Running = entry.Running;
    }
}

/// <summary>
/// Dienste page: the installed services with a case-insensitive filter over name, display name and
/// description, grouped by status (Wird ausgeführt before Beendet) and alphabetical within each
/// group. Degradation (access denied) surfaces as an InfoBar state, same pattern as
/// <see cref="SensorsViewModel"/>. Read-only throughout — no start/stop anywhere (plan-tm2.md §2).
/// <see cref="ApplySnapshot"/> is wired to the engine tick but re-queries the catalog only every
/// <see cref="QueryEveryNthTick"/>th call: services change rarely, and enumerating the service
/// control manager every second would be waste.
/// </summary>
public sealed partial class Tm2ServicesViewModel : ObservableObject
{
    // ponytail: tick divider instead of an own timer — 5 s at the default 1 s engine interval,
    // zero new infrastructure. A dedicated slow timer only if the page ever needs a refresh rate
    // decoupled from the engine.
    /// <summary>The catalog is re-enumerated on every Nth <see cref="ApplySnapshot"/> tick.</summary>
    public const int QueryEveryNthTick = 5;

    private readonly IServiceCatalog catalog;

    // All rows in catalog order — refiltered/regrouped without re-querying, ProcessListViewModel style.
    private readonly List<Tm2ServiceRowViewModel> allRows = [];
    private readonly Dictionary<string, Tm2ServiceRowViewModel> rowsByName = new(StringComparer.OrdinalIgnoreCase);
    private int tick;

    public Tm2ServicesViewModel(IServiceCatalog catalog)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    /// <summary>Visible rows: filtered, running first, alphabetical within each status group.</summary>
    public ObservableCollection<Tm2ServiceRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    private string filter = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowBanner))]
    [NotifyPropertyChangedFor(nameof(BannerText))]
    private ServiceCatalogAvailability availability = ServiceCatalogAvailability.Available;

    partial void OnFilterChanged(string value) => RefreshView();

    /// <summary>True whenever the catalog is degraded and the InfoBar should be visible.</summary>
    public bool ShowBanner => Availability != ServiceCatalogAvailability.Available;

    /// <summary>User-facing explanation of the current degradation; empty when available.</summary>
    public string BannerText => Availability switch
    {
        ServiceCatalogAvailability.Available => "",
        ServiceCatalogAvailability.AccessDenied =>
            "Der Dienststeuerungs-Manager hat die Abfrage verweigert — es können keine Dienste angezeigt werden.",
        _ => throw new ArgumentOutOfRangeException(nameof(Availability), Availability, "Unknown availability."),
    };

    /// <summary>
    /// Called once per engine tick; the snapshot itself carries no service data — the catalog is
    /// queried through <see cref="IServiceCatalog"/>, and only on every <see cref="QueryEveryNthTick"/>th call.
    /// </summary>
    public void ApplySnapshot(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (tick++ % QueryEveryNthTick != 0)
        {
            return;
        }

        // ponytail: synchronous SCM query on the UI thread (~ms range every 5th tick);
        // move to Task.Run + posted result if it ever shows up as jank.
        var result = catalog.Query();
        Availability = result.Availability;

        allRows.Clear();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in result.Services)
        {
            if (!rowsByName.TryGetValue(entry.Name, out var row))
            {
                row = new Tm2ServiceRowViewModel(entry.Name);
                rowsByName[entry.Name] = row;
            }

            row.Update(entry);
            allRows.Add(row);
            seen.Add(entry.Name);
        }

        foreach (var name in rowsByName.Keys.Where(n => !seen.Contains(n)).ToList())
        {
            rowsByName.Remove(name);
        }

        RefreshView();
    }

    private void RefreshView()
    {
        var target = allRows
            .Where(MatchesFilter)
            .OrderByDescending(row => row.Running) // status group: Wird ausgeführt first
            .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        CollectionReconciler.Reconcile(Rows, target);
    }

    private bool MatchesFilter(Tm2ServiceRowViewModel row) =>
        Filter.Length == 0
        || row.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase)
        || row.DisplayName.Contains(Filter, StringComparison.OrdinalIgnoreCase)
        || row.Description.Contains(Filter, StringComparison.OrdinalIgnoreCase);
}
