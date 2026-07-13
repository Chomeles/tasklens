using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TaskLens.Core.ViewModels;

namespace TaskLens.App.Views;

/// <summary>Process list page. Code-behind is x:Bind wiring only (plan.md MVVM rules).</summary>
public sealed partial class ProcessesPage : Page
{
    public ProcessesPage()
    {
        ViewModel = App.Services.GetRequiredService<ProcessListViewModel>();
        InitializeComponent();
    }

    public ProcessListViewModel ViewModel { get; }

    /// <summary>Header click → SortBy command; Tag carries the column name (enums can't be x:Bind args).</summary>
    private void OnSortHeaderClick(object sender, RoutedEventArgs e) =>
        ViewModel.SortByCommand.Execute(Enum.Parse<ProcessColumn>((string)((FrameworkElement)sender).Tag));

    /// <summary>Row click → select the process in DetailsViewModel and navigate there.</summary>
    private void OnProcessItemClick(object sender, ItemClickEventArgs e)
    {
        var row = (ProcessRowViewModel)e.ClickedItem;
        App.Services.GetRequiredService<DetailsViewModel>().SelectProcess(row.Pid, row.StartTimeUtc, row.Name);
        Frame.Navigate(typeof(DetailsPage));
    }
}
