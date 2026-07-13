using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using TaskLens.Core.ViewModels;

namespace TaskLens.App.Views;

/// <summary>Settings page. Code-behind is x:Bind wiring only (plan.md MVVM rules).</summary>
public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; }
}
