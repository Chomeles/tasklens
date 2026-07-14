using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TaskLens.Core.Models;
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
        SelectRowThen(sender, ViewModel.EndTaskCommand.Execute);

    private void OnEndTreeClick(object sender, RoutedEventArgs e) =>
        SelectRowThen(sender, ViewModel.EndTreeCommand.Execute);

    private void OnEfficiencyModeClick(object sender, RoutedEventArgs e) =>
        SelectRowThen(sender, ViewModel.EnableEfficiencyModeCommand.Execute);

    private void OnOpenFileLocationClick(object sender, RoutedEventArgs e) =>
        SelectRowThen(sender, ViewModel.OpenFileLocationCommand.Execute);

    /// <summary>Priority submenu: Tag carries the ProcessPriority name (enums can't be XAML args).</summary>
    private void OnSetPriorityClick(object sender, RoutedEventArgs e) =>
        SelectRowThen(sender, _ => ViewModel.SetPriorityCommand.Execute(
            Enum.Parse<ProcessPriority>((string)((FrameworkElement)sender).Tag)));

    private void SelectRowThen(object sender, Action<object?> execute)
    {
        ViewModel.SelectedRow = (Tm2ProcessRowViewModel)((FrameworkElement)sender).DataContext;
        execute(null);
    }

    /// <summary>„Neuen Task ausführen": minimal run box; the command line goes to the VM on confirm.</summary>
    private async void OnRunNewTaskClick(object sender, RoutedEventArgs e)
    {
        var input = new TextBox
        {
            PlaceholderText = "Geben Sie den Namen eines Programms, Ordners, Dokuments oder einer Internetressource an.",
        };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Neuen Task erstellen",
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    input,
                    new TextBlock
                    {
                        Text = "Der Task wird mit Administratorrechten erstellt, da Taskmanager2 erhöht ausgeführt wird.",
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = 0.7,
                    },
                },
            },
            PrimaryButtonText = "OK",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            ViewModel.RunNewTask(input.Text);
        }
    }

    private void OnErrorClosed(InfoBar sender, object args) =>
        ViewModel.LastActionError = null;
}
