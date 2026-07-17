using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    /// <summary>The last observed item — the toggle command hands it back to the manager.</summary>
    internal StartupItem? Item { get; private set; }

    [ObservableProperty]
    private string command = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PublisherText))]
    private string publisher = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool enabled = true;

    /// <summary>Status column text, Win11 Task Manager wording.</summary>
    public string StatusText => Enabled ? "Aktiviert" : "Deaktiviert";

    /// <summary>Herausgeber column: version-info CompanyName, the real TM's honest „—" without.</summary>
    public string PublisherText => Publisher.Length > 0 ? Publisher : "—";

    internal void Update(StartupItem item)
    {
        Item = item;
        Command = item.Command;
        Publisher = item.Publisher;
        Enabled = item.Enabled;
    }
}

/// <summary>
/// Autostart-Apps page: the configured autostart entries, alphabetical by name. Degradation
/// (access denied) surfaces as an InfoBar state, same pattern as <see cref="Tm2ServicesViewModel"/>.
/// Since tm3-06 the selected entry can be toggled through <see cref="IStartupManager"/> — the
/// Win11 „Aktivieren"/„Deaktivieren" button and context menu. <see cref="ApplySnapshot"/>
/// is wired to the engine tick but re-queries the source only every
/// <see cref="Tm2ServicesViewModel.QueryEveryNthTick"/>th call — autostart entries change even
/// more rarely than services.
/// </summary>
public sealed partial class Tm2StartupViewModel : ObservableObject
{
    private readonly IStartupItemSource source;
    private readonly IStartupManager? manager;
    private readonly Dictionary<string, Tm2StartupRowViewModel> rowsByKey = new(StringComparer.OrdinalIgnoreCase);
    private int tick;

    public Tm2StartupViewModel(IStartupItemSource source, IStartupManager? manager = null)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.manager = manager;
    }

    /// <summary>The row the toggle command acts on; cleared when the row leaves the list.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleSelectedCommand))]
    [NotifyPropertyChangedFor(nameof(ToggleButtonText))]
    private Tm2StartupRowViewModel? selectedRow;

    /// <summary>Error text of the last failed toggle; null when the last toggle succeeded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActionError))]
    private string? lastActionError;

    public bool HasActionError => LastActionError is not null;

    /// <summary>Win11 wording: the button offers the state change, not the current state.</summary>
    public string ToggleButtonText => SelectedRow is { Enabled: true } ? "Deaktivieren" : "Aktivieren";

    private bool CanToggle() => manager is not null && SelectedRow?.Item is not null;

    /// <summary>Flips the selected entry; the optimistic row update is confirmed by a forced re-query.</summary>
    [RelayCommand(CanExecute = nameof(CanToggle))]
    private void ToggleSelected()
    {
        if (manager is null || SelectedRow?.Item is not { } item)
        {
            return;
        }

        var target = !SelectedRow.Enabled;
        var result = manager.SetEnabled(item, target);
        if (result.Success)
        {
            SelectedRow.Enabled = target;
            tick = 0; // next ApplySnapshot re-queries instead of waiting out the cadence
        }

        LastActionError = result.Success ? null : result.Error;
        OnPropertyChanged(nameof(ToggleButtonText));
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

        if (SelectedRow is not null && !Rows.Contains(SelectedRow))
        {
            SelectedRow = null;
        }

        // A re-query may have flipped the selected row's state underneath the button label.
        OnPropertyChanged(nameof(ToggleButtonText));
    }
}
