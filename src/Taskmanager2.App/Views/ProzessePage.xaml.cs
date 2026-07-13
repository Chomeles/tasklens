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
}
