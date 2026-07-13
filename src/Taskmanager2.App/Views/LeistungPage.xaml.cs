using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using TaskLens.Core.ViewModels;

namespace Taskmanager2.App.Views;

/// <summary>Leistung page: rail + main panel over Tm2PerformanceViewModel. Code-behind is x:Bind wiring only.</summary>
public sealed partial class LeistungPage : Page
{
    public LeistungPage()
    {
        ViewModel = App.Services.GetRequiredService<Tm2PerformanceViewModel>();
        InitializeComponent();
        Rail.SelectedItem = ViewModel.SelectedEntry;
    }

    public Tm2PerformanceViewModel ViewModel { get; }

    /// <summary>Rail selection → VM (x:Bind can't TwoWay the object-typed SelectedItem to a typed property).</summary>
    private void OnRailSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 1 && e.AddedItems[0] is Tm2PerformanceEntryViewModel entry)
        {
            ViewModel.SelectedEntry = entry;
        }
    }
}
