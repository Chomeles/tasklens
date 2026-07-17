using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TaskLens.Core.ViewModels;

namespace Taskmanager2.App.Views;

/// <summary>Benutzer page: session table with Verbindung trennen / Abmelden (tm3-08). Code-behind
/// is x:Bind wiring only (plan.md MVVM rules).</summary>
public sealed partial class BenutzerPage : Page
{
    public BenutzerPage()
    {
        ViewModel = App.Services.GetRequiredService<Tm2UsersViewModel>();
        InitializeComponent();
    }

    public Tm2UsersViewModel ViewModel { get; }

    /// <summary>ListView.SelectedItem is object — TwoWay x:Bind can't cast, so mirror it manually.</summary>
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ViewModel.SelectedRow = ((ListView)sender).SelectedItem as Tm2UserRowViewModel;

    /// <summary>Context menu acts on the right-clicked row: select it, then run the command.</summary>
    private void OnDisconnectClick(object sender, RoutedEventArgs e) =>
        RunOnRow(sender, vm => vm.DisconnectSelectedCommand.Execute(null));

    private void OnLogoffClick(object sender, RoutedEventArgs e) =>
        RunOnRow(sender, vm => vm.LogoffSelectedCommand.Execute(null));

    private void RunOnRow(object sender, Action<Tm2UsersViewModel> run)
    {
        ViewModel.SelectedRow = (Tm2UserRowViewModel)((FrameworkElement)sender).DataContext;
        run(ViewModel);
    }

    private void OnErrorClosed(InfoBar sender, object args) =>
        ViewModel.LastActionError = null;
}
