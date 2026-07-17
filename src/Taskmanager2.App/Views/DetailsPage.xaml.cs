using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TaskLens.Core.ViewModels;

namespace Taskmanager2.App.Views;

/// <summary>
/// Details page: the same shared Core process list as ProzessePage (shared singleton — shared
/// sort/filter state is Task-Manager-typical), with every real-data column. Code-behind is x:Bind
/// wiring only (plan.md MVVM rules).
/// </summary>
public sealed partial class DetailsPage : Page
{
    public DetailsPage()
    {
        ViewModel = App.Services.GetRequiredService<ProcessListViewModel>();
        InitializeComponent();
    }

    public ProcessListViewModel ViewModel { get; }

    /// <summary>Header click → SortBy command; Tag carries the column name (enums can't be x:Bind args).</summary>
    private void OnSortHeaderClick(object sender, RoutedEventArgs e) =>
        ViewModel.SortByCommand.Execute(Enum.Parse<ProcessColumn>((string)((FrameworkElement)sender).Tag));
    /// <summary>„Neuen Task ausführen": shared run dialog, on every page like the real TM (tm3-10).</summary>
    private async void OnRunTaskClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        await Taskmanager2.App.Services.RunTaskDialog.ShowAsync(XamlRoot);
}
