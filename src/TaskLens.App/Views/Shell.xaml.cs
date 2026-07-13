using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TaskLens.Core.ViewModels;

namespace TaskLens.App.Views;

/// <summary>NavigationView shell. Code-behind is navigation wiring only (plan.md MVVM rules).</summary>
public sealed partial class Shell : Window
{
    public PawnIoBannerViewModel BannerViewModel { get; } = App.Services.GetRequiredService<PawnIoBannerViewModel>();

    public Shell()
    {
        InitializeComponent();
        Nav.SelectedItem = Nav.MenuItems[0]; // triggers OnSelectionChanged -> ProcessesPage
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var pageType = args.IsSettingsSelected
            ? typeof(SettingsPage)
            : (args.SelectedItemContainer?.Tag as string) switch
            {
                "Sensors" => typeof(SensorsPage),
                "Details" => typeof(DetailsPage),
                _ => typeof(ProcessesPage),
            };

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}
