using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TaskLens.Core.ViewModels;

namespace Taskmanager2.App.Views;

/// <summary>Prozesse page: the Tm2 process list. Code-behind is x:Bind wiring only (plan.md MVVM rules).</summary>
public sealed partial class ProzessePage : Page
{
    public ProzessePage()
    {
        ViewModel = App.Services.GetRequiredService<Tm2ProcessListViewModel>();
        InitializeComponent();
    }

    public Tm2ProcessListViewModel ViewModel { get; }

    /// <summary>Header click → Inner.SortBy command; Tag carries the column name (enums can't be x:Bind args).</summary>
    private void OnSortHeaderClick(object sender, RoutedEventArgs e) =>
        ViewModel.Inner.SortByCommand.Execute(Enum.Parse<ProcessColumn>((string)((FrameworkElement)sender).Tag));

    /// <summary>ListView.SelectedItem is object — TwoWay x:Bind can't cast, so mirror it manually.</summary>
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ViewModel.SelectedRow = ((ListView)sender).SelectedItem as Tm2ProcessRowViewModel;

    /// <summary>Context menu acts on the right-clicked row: select it, then run the command.</summary>
    private void OnEndTaskClick(object sender, RoutedEventArgs e) =>
        RunOnRow(sender, entireTree: false);

    private void OnEndTreeClick(object sender, RoutedEventArgs e) =>
        RunOnRow(sender, entireTree: true);

    private void RunOnRow(object sender, bool entireTree)
    {
        ViewModel.SelectedRow = (Tm2ProcessRowViewModel)((FrameworkElement)sender).DataContext;
        var command = entireTree ? ViewModel.EndTreeCommand : ViewModel.EndTaskCommand;
        command.Execute(null);
    }

    private void OnErrorClosed(InfoBar sender, object args) =>
        ViewModel.LastActionError = null;
}
