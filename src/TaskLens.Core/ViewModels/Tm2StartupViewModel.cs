using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TaskLens.Core.Models;
using TaskLens.Core.Services;

namespace TaskLens.Core.ViewModels;

/// <summary>
/// One Autostart row. Identity is (name, source) — the same entry name may exist in several
/// Run keys; values update in place across refreshes.
/// </summary>
public sealed partial class Tm2StartupRowViewModel : ObservableObject
{
    public Tm2StartupRowViewModel(string name, string source)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public string Name { get; }

    public string Source { get; }

    [ObservableProperty]
    private string command = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool enabled = true;

    /// <summary>Status column text, Win11 Task Manager wording.</summary>
    public string StatusText => Enabled ? "Aktiviert" : "Deaktiviert";

    internal void Update(StartupItem item)
    {
        Command = item.Command;
        Enabled = item.Enabled;
    }
}

/// <summary>
/// Autostart-Apps page: the configured autostart entries, alphabetical by name. Degradation
/// (access denied) surfaces as an InfoBar state, same pattern as <see cref="Tm2ServicesViewModel"/>.
/// Read-only throughout — no enable/disable anywhere (plan-tm2.md §2). <see cref="ApplySnapshot"/>
/// is wired to the engine tick but re-queries the source only every
/// <see cref="Tm2ServicesViewModel.QueryEveryNthTick"/>th call — autostart entries change even
/// more rarely than services.
/// </summary>
public sealed partial class Tm2StartupViewModel : ObservableObject
{
    private readonly IStartupItemSource source;
    private readonly Dictionary<string, Tm2StartupRowViewModel> rowsByKey = new(StringComparer.OrdinalIgnoreCase);
    private int tick;

    public Tm2StartupViewModel(IStartupItemSource source)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>Visible rows, alphabetical by name (then source, for name collisions).</summary>
    public ObservableCollection<Tm2StartupRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowBanner))]
    [NotifyPropertyChangedFor(nameof(BannerText))]
    private CatalogAvailability availability = CatalogAvailability.Available;

    /// <summary>True whenever the source is degraded and the InfoBar should be visible.</summary>
    public bool ShowBanner => Availability != CatalogAvailability.Available;

    /// <summary>User-facing explanation of the current degradation; empty when available.</summary>
    public string BannerText => Availability switch
    {
        CatalogAvailability.Available => "",
        CatalogAvailability.AccessDenied =>
            "Der Zugriff auf die Autostart-Quellen wurde verweigert — es können keine Autostart-Apps angezeigt werden.",
        _ => throw new ArgumentOutOfRangeException(nameof(Availability), Availability, "Unknown availability."),
    };

    /// <summary>
    /// Called once per engine tick; the snapshot itself carries no autostart data — the source is
    /// queried through <see cref="IStartupItemSource"/>, and only on every
    /// <see cref="Tm2ServicesViewModel.QueryEveryNthTick"/>th call.
    /// </summary>
    public void ApplySnapshot(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (tick++ % Tm2ServicesViewModel.QueryEveryNthTick != 0)
        {
            return;
        }

        var result = source.Query();
        Availability = result.Availability;

        var target = new List<Tm2StartupRowViewModel>(result.Items.Count);
        foreach (var item in result.Items)
        {
            // \n cannot occur in registry value names or file names — a collision-free key.
            var key = item.Source + "\n" + item.Name;
            if (!rowsByKey.TryGetValue(key, out var row))
            {
                row = new Tm2StartupRowViewModel(item.Name, item.Source);
                rowsByKey[key] = row;
            }

            row.Update(item);
            target.Add(row);
        }

        foreach (var stale in rowsByKey.Where(pair => !target.Contains(pair.Value)).Select(pair => pair.Key).ToList())
        {
            rowsByKey.Remove(stale);
        }

        target.Sort((a, b) =>
        {
            var byName = StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
            return byName != 0 ? byName : StringComparer.OrdinalIgnoreCase.Compare(a.Source, b.Source);
        });
        CollectionReconciler.Reconcile(Rows, target);
    }
}
