using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TaskLens.Core.ViewModels;

namespace Taskmanager2.App.Views;

/// <summary>Prozesse page: the Tm2 process list. Code-behind is x:Bind wiring only (plan.md MVVM rules).</summary>
public sealed partial class ProzessePage : Page
{
    private ListView? selectionOwner;

    public ProzessePage()
    {
        ViewModel = App.Services.GetRequiredService<Tm2ProcessListViewModel>();
        InitializeComponent();
    }

    public Tm2ProcessListViewModel ViewModel { get; }

    /// <summary>Header click → Inner.SortBy command; Tag carries the column name (enums can't be x:Bind args).</summary>
    private void OnSortHeaderClick(object sender, RoutedEventArgs e) =>
        ViewModel.Inner.SortByCommand.Execute(Enum.Parse<ProcessColumn>((string)((FrameworkElement)sender).Tag));

    /// <summary>„Neuen Task ausführen": show the run dialog, launch on OK (tm3-02).</summary>
    private async void OnRunTaskClick(object sender, RoutedEventArgs e)
    {
        RunTaskCommand.Text = string.Empty;
        RunTaskElevated.IsChecked = false;
        RunTaskDialog.XamlRoot = XamlRoot;
        if (await RunTaskDialog.ShowAsync() == ContentDialogResult.Primary && RunTaskCommand.Text.Length > 0)
        {
            ViewModel.RunTask(RunTaskCommand.Text, RunTaskElevated.IsChecked == true);
        }
    }

    /// <summary>App-row chevron: flip the row's window-child visibility.</summary>
    private void OnRowToggleClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is Tm2ProcessRowViewModel row)
        {
            row.IsExpanded = !row.IsExpanded;
        }
    }

    /// <summary>Group-header chevron: flip the section's collapse state.</summary>
    private void OnGroupToggleClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is Tm2ProcessGroupSection section)
        {
            section.IsExpanded = !section.IsExpanded;
        }
    }

    /// <summary>
    /// One ListView per group, one logical selection: mirror into the VM and clear the previous
    /// group's selection when a different group takes over.
    /// </summary>
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var list = (ListView)sender;
        if (list.SelectedItem is not Tm2ProcessRowViewModel row)
        {
            if (ReferenceEquals(selectionOwner, list))
            {
                ViewModel.SelectedRow = null;
            }

            return;
        }

        if (selectionOwner is not null && !ReferenceEquals(selectionOwner, list))
        {
            selectionOwner.SelectedItem = null;
        }

        selectionOwner = list;
        ViewModel.SelectedRow = row;
    }

    /// <summary>Context menu acts on the right-clicked row: select it, then run the command.</summary>
    private void OnEndTaskClick(object sender, RoutedEventArgs e) =>
        RunOnRow(sender, entireTree: false);

    private void OnEndTreeClick(object sender, RoutedEventArgs e) =>
        RunOnRow(sender, entireTree: true);

    private void OnEfficiencyClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedRow = (Tm2ProcessRowViewModel)((FrameworkElement)sender).DataContext;
        ViewModel.EfficiencyCommand.Execute(null);
    }

    private void RunOnRow(object sender, bool entireTree)
    {
        ViewModel.SelectedRow = (Tm2ProcessRowViewModel)((FrameworkElement)sender).DataContext;
        var command = entireTree ? ViewModel.EndTreeCommand : ViewModel.EndTaskCommand;
        command.Execute(null);
    }

    private void OnErrorClosed(InfoBar sender, object args) =>
        ViewModel.LastActionError = null;
}
