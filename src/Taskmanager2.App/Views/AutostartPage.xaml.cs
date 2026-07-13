using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using TaskLens.Core.ViewModels;

namespace Taskmanager2.App.Views;

/// <summary>Autostart-Apps page: the read-only startup table. Code-behind is x:Bind wiring only (plan.md MVVM rules).</summary>
public sealed partial class AutostartPage : Page
{
    public AutostartPage()
    {
        ViewModel = App.Services.GetRequiredService<Tm2StartupViewModel>();
        InitializeComponent();
    }

    public Tm2StartupViewModel ViewModel { get; }
}
