using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using TaskLens.Core.ViewModels;

namespace Taskmanager2.App.Views;

/// <summary>App-Verlauf page: the per-name history table. Code-behind is x:Bind wiring only (plan.md MVVM rules).</summary>
public sealed partial class AppVerlaufPage : Page
{
    public AppVerlaufPage()
    {
        ViewModel = App.Services.GetRequiredService<Tm2AppHistoryViewModel>();
        InitializeComponent();
    }

    public Tm2AppHistoryViewModel ViewModel { get; }
}
