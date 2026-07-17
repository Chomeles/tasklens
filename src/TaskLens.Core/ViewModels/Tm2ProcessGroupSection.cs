using System.Collections.ObjectModel;
using System.ComponentModel;

namespace TaskLens.Core.ViewModels;

/// <summary>
/// Taskmanager2's view of one <see cref="ProcessGroupSection"/>: same group, same collapse state
/// (delegated to the inner section, single source of truth), but holding the joined
/// <see cref="Tm2ProcessRowViewModel"/> rows so the grouped list can bind sensors + sparkline.
/// Persistent like its inner section; contents are reconciled per resync.
/// </summary>
public sealed class Tm2ProcessGroupSection : ObservableCollection<Tm2ProcessRowViewModel>
{
    public Tm2ProcessGroupSection(ProcessGroupSection inner)
    {
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        ((INotifyPropertyChanged)Inner).PropertyChanged += (_, e) =>
        {
            // Header/HasItems/IsExpanded are computed off Inner — mirror its notifications.
            if (e.PropertyName is nameof(Header) or nameof(HasItems) or nameof(IsExpanded))
            {
                OnPropertyChanged(new PropertyChangedEventArgs(e.PropertyName));
            }
        };
    }

    public ProcessGroupSection Inner { get; }

    /// <summary>"Apps (7)" — real Task-Manager group header text.</summary>
    public string Header => Inner.Header;

    /// <summary>Empty groups are hidden entirely, like the real app.</summary>
    public bool HasItems => Inner.HasItems;

    /// <summary>Chevron state, shared with the inner section.</summary>
    public bool IsExpanded
    {
        get => Inner.IsExpanded;
        set => Inner.IsExpanded = value;
    }
}
