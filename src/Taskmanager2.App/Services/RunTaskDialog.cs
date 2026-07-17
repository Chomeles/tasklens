using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TaskLens.Core.ViewModels;

namespace Taskmanager2.App.Services;

/// <summary>
/// The real TM's „Neuen Task erstellen" dialog, built in code so every page can offer the button
/// (tm3-10) without duplicating XAML. Launch + error surface run through the shared
/// <see cref="Tm2ProcessListViewModel"/> (its InfoBar on the Prozesse page shows failures).
/// </summary>
internal static class RunTaskDialog
{
    internal static async System.Threading.Tasks.Task ShowAsync(XamlRoot xamlRoot)
    {
        var command = new TextBox { PlaceholderText = "Öffnen" };
        var elevated = new CheckBox { Content = "Diesen Task mit Administratorrechten erstellen" };
        var dialog = new ContentDialog
        {
            Title = "Neuen Task erstellen",
            PrimaryButtonText = "OK",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
            Content = new StackPanel
            {
                Width = 380,
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        Text = "Geben Sie den Namen eines Programms, Ordners, Dokuments oder einer Internetressource an.",
                    },
                    command,
                    elevated,
                },
            },
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary && command.Text.Length > 0)
        {
            App.Services.GetRequiredService<Tm2ProcessListViewModel>()
                .RunTask(command.Text, elevated.IsChecked == true);
        }
    }
}
