using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace TaskLens.Core.ViewModels;

/// <summary>
/// One group section ("Apps (7)", "Hintergrundprozesse (42)", "Windows-Prozesse (58)") for the
/// grouped Prozesse-page list. Sections are persistent — created once per group and reconciled in
/// place each tick — so the user's collapse state (<see cref="IsExpanded"/>) survives refreshes,
/// like the real Task Manager's group chevrons.
/// </summary>
public sealed class ProcessGroupSection : ObservableCollection<ProcessRowViewModel>
{
    private bool isExpanded = true;

    public ProcessGroupSection(ProcessGroup group)
    {
        Group = group;
    }

    public ProcessGroup Group { get; }

    /// <summary>"Apps (7)" — real Task-Manager group header text.</summary>
    public string Header => $"{ProcessClassification.Label(Group)} ({Count})";

    /// <summary>Empty groups are hidden entirely, like the real app.</summary>
    public bool HasItems => Count > 0;

    /// <summary>Chevron state; toggled from the group header, persists across ticks.</summary>
    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (isExpanded != value)
            {
                isExpanded = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnCollectionChanged(e);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Header)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasItems)));
    }
}
