using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TaskLens.Core.ViewModels;

namespace Taskmanager2.App.Views;

/// <summary>
/// Details page: the same Tm2 process join as ProzessePage (shared singleton — shared sort/filter
/// state is Task-Manager-typical) but with EVERY available column, plus the DetailsViewModel
/// system/process history graphs on top. Maximum density — the satire peak. Code-behind is
/// x:Bind wiring only (plan.md MVVM rules).
/// </summary>
public sealed partial class DetailsPage : Page
{
    public DetailsPage()
    {
        ViewModel = App.Services.GetRequiredService<Tm2ProcessListViewModel>();
        Details = App.Services.GetRequiredService<DetailsViewModel>();
        InitializeComponent();
    }

    public Tm2ProcessListViewModel ViewModel { get; }

    public DetailsViewModel Details { get; }

    /// <summary>Header click → Inner.SortBy command; Tag carries the column name (enums can't be x:Bind args).</summary>
    private void OnSortHeaderClick(object sender, RoutedEventArgs e) =>
        ViewModel.Inner.SortByCommand.Execute(Enum.Parse<ProcessColumn>((string)((FrameworkElement)sender).Tag));

    /// <summary>Row click → track that process in the top history graph (DetailsViewModel).</summary>
    private void OnProcessItemClick(object sender, ItemClickEventArgs e)
    {
        var row = (Tm2ProcessRowViewModel)e.ClickedItem;
        Details.SelectProcess(row.Pid, row.StartTimeUtc, row.Name);
    }
}
