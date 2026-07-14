using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TaskLens.Core.ViewModels;

namespace Taskmanager2.App.Views;

/// <summary>Autostart-Apps page: startup table + Aktivieren/Deaktivieren (tm3-06). Code-behind is x:Bind wiring only (plan.md MVVM rules).</summary>
public sealed partial class AutostartPage : Page
{
    public AutostartPage()
    {
        ViewModel = App.Services.GetRequiredService<Tm2StartupViewModel>();
        InitializeComponent();
    }

    public Tm2StartupViewModel ViewModel { get; }

    /// <summary>ListView.SelectedItem is object — TwoWay x:Bind can't cast, so mirror it manually.</summary>
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ViewModel.SelectedRow = ((ListView)sender).SelectedItem as Tm2StartupRowViewModel;

    /// <summary>Context menu acts on the right-clicked row: select it, then run the command.</summary>
    private void OnToggleClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedRow = (Tm2StartupRowViewModel)((FrameworkElement)sender).DataContext;
        ViewModel.ToggleSelectedCommand.Execute(null);
    }

    private void OnErrorClosed(InfoBar sender, object args) =>
        ViewModel.LastActionError = null;
}
