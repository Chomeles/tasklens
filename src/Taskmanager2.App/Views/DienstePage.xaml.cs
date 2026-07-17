using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TaskLens.Core.ViewModels;

namespace Taskmanager2.App.Views;

/// <summary>Dienste page: service table with Starten/Beenden/Neu starten (tm3-07). Code-behind is
/// x:Bind wiring only (plan.md MVVM rules).</summary>
public sealed partial class DienstePage : Page
{
    public DienstePage()
    {
        ViewModel = App.Services.GetRequiredService<Tm2ServicesViewModel>();
        InitializeComponent();
    }

    public Tm2ServicesViewModel ViewModel { get; }

    /// <summary>ListView.SelectedItem is object — TwoWay x:Bind can't cast, so mirror it manually.</summary>
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ViewModel.SelectedRow = ((ListView)sender).SelectedItem as Tm2ServiceRowViewModel;

    /// <summary>Context menu acts on the right-clicked row: select it, then run the command.</summary>
    private void OnStartClick(object sender, RoutedEventArgs e) =>
        RunOnRow(sender, vm => vm.StartSelectedCommand.Execute(null));

    private void OnStopClick(object sender, RoutedEventArgs e) =>
        RunOnRow(sender, vm => vm.StopSelectedCommand.Execute(null));

    private void OnRestartClick(object sender, RoutedEventArgs e) =>
        RunOnRow(sender, vm => vm.RestartSelectedCommand.Execute(null));

    private void RunOnRow(object sender, Action<Tm2ServicesViewModel> run)
    {
        ViewModel.SelectedRow = (Tm2ServiceRowViewModel)((FrameworkElement)sender).DataContext;
        run(ViewModel);
    }

    private void OnErrorClosed(InfoBar sender, object args) =>
        ViewModel.LastActionError = null;
}
