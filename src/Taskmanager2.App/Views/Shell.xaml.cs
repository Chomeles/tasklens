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

    /// <summary>Global search target: the real TM's search box filters the process list from the header.</summary>
    public ProcessListViewModel ProcessList { get; } = App.Services.GetRequiredService<ProcessListViewModel>();

    public Shell()
    {
        InitializeComponent();
        // Real TM: the search box sits in the title bar itself. The left spacer is the drag
        // surface, so the box stays clickable; system caption buttons overlay the right edge.
        // ponytail: SetTitleBar nimmt genau ein Element — nur die linke Fläche zieht. Rechts vom
        // Suchfeld zieht nichts; InputNonClientPointerSource-Passthrough-Regionen, falls das stört.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragRegionLeft);
        Nav.SelectedItem = Nav.MenuItems[0]; // triggers OnSelectionChanged -> ProzessePage
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            if (ContentFrame.CurrentSourcePageType != typeof(EinstellungenPage))
            {
                ContentFrame.Navigate(typeof(EinstellungenPage));
            }

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
