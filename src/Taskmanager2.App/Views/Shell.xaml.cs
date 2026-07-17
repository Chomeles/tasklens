using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TaskLens.Core.ViewModels;

namespace Taskmanager2.App.Views;

/// <summary>NavigationView shell with all 7 Task-Manager-look nav items. Code-behind is navigation
/// wiring only (plan.md MVVM rules) — pages carry their own ViewModels once tm2-02+ lands.</summary>
public sealed partial class Shell : Window
{
    public PawnIoBannerViewModel BannerViewModel { get; } = App.Services.GetRequiredService<PawnIoBannerViewModel>();

    public Shell()
    {
        InitializeComponent();
        Nav.SelectedItem = Nav.MenuItems[0]; // triggers OnSelectionChanged -> ProzessePage
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        // Einstellungen is a visual-only stub (task spec) — no page behind it, so no navigation.
        if (args.IsSettingsSelected)
        {
            return;
        }

        var pageType = (args.SelectedItemContainer?.Tag as string) switch
        {
            "Leistung" => typeof(LeistungPage),
            "AppVerlauf" => typeof(AppVerlaufPage),
            "Autostart" => typeof(AutostartPage),
            "Benutzer" => typeof(BenutzerPage),
            "Details" => typeof(DetailsPage),
            "Dienste" => typeof(DienstePage),
            _ => typeof(ProzessePage),
        };

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}
