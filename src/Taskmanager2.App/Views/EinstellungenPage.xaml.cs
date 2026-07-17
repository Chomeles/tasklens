using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using TaskLens.Core.ViewModels;

namespace Taskmanager2.App.Views;

/// <summary>Einstellungen page, Win11-TM settings look. Code-behind is x:Bind wiring plus the
/// Hoch/Normal/Niedrig ↔ seconds mapping (view vocabulary, not business logic).</summary>
public sealed partial class EinstellungenPage : Page
{
    // Real TM vocabulary: Hoch = 0.5 s, Normal = 1 s, Niedrig = 4 s.
    private static readonly double[] SpeedSeconds = [0.5, 1, 4];

    public EinstellungenPage()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();
        SpeedBox.SelectedIndex = ViewModel.RefreshIntervalSeconds switch
        {
            <= 0.5 => 0,
            <= 1 => 1,
            _ => 2,
        };
    }

    public SettingsViewModel ViewModel { get; }

    private void OnSpeedChanged(object sender, SelectionChangedEventArgs e)
    {
        var index = ((ComboBox)sender).SelectedIndex;
        if (index >= 0 && index < SpeedSeconds.Length)
        {
            ViewModel.RefreshIntervalSeconds = SpeedSeconds[index];
        }
    }
}
