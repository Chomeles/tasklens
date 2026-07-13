using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TaskLens.Core.Models;
using TaskLens.Core.Services;

namespace TaskLens.Core.ViewModels;

/// <summary>One Benutzer row. Identity is the session id; values update in place across refreshes.</summary>
public sealed partial class Tm2UserRowViewModel : ObservableObject
{
    public Tm2UserRowViewModel(int sessionId)
    {
        SessionId = sessionId;
    }

    public int SessionId { get; }

    /// <summary>Sitzungs-ID column text.</summary>
    public string SessionIdText => SessionId.ToString();

    [ObservableProperty]
    private string userName = "";

    [ObservableProperty]
    private string state = "";

    internal void Update(UserSession session)
    {
        UserName = session.UserName;
        State = session.State;
    }
}

/// <summary>
/// Benutzer page: the interactive logon sessions, ordered by session id. Degradation
/// (access denied) surfaces as an InfoBar state, same pattern as <see cref="Tm2ServicesViewModel"/>.
/// Read-only throughout — no disconnect or logoff anywhere (plan-tm2.md §2).
/// <see cref="ApplySnapshot"/> is wired to the engine tick but re-queries the source only every
/// <see cref="Tm2ServicesViewModel.QueryEveryNthTick"/>th call — sessions change rarely.
/// </summary>
public sealed partial class Tm2UsersViewModel : ObservableObject
{
    private readonly IUserSessionSource source;
    private readonly Dictionary<int, Tm2UserRowViewModel> rowsById = [];
    private int tick;

    public Tm2UsersViewModel(IUserSessionSource source)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>Visible rows, ordered by session id.</summary>
    public ObservableCollection<Tm2UserRowViewModel> Rows { get; } = [];

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
            "Die Sitzungsabfrage wurde verweigert — es können keine Benutzer angezeigt werden.",
        _ => throw new ArgumentOutOfRangeException(nameof(Availability), Availability, "Unknown availability."),
    };

    /// <summary>
    /// Called once per engine tick; the snapshot itself carries no session data — the source is
    /// queried through <see cref="IUserSessionSource"/>, and only on every
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

        var target = new List<Tm2UserRowViewModel>(result.Sessions.Count);
        foreach (var session in result.Sessions)
        {
            if (!rowsById.TryGetValue(session.SessionId, out var row))
            {
                row = new Tm2UserRowViewModel(session.SessionId);
                rowsById[session.SessionId] = row;
            }

            row.Update(session);
            target.Add(row);
        }

        foreach (var stale in rowsById.Where(pair => !target.Contains(pair.Value)).Select(pair => pair.Key).ToList())
        {
            rowsById.Remove(stale);
        }

        target.Sort((a, b) => a.SessionId.CompareTo(b.SessionId));
        CollectionReconciler.Reconcile(Rows, target);
    }
}
