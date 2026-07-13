using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using TaskLens.Core.ViewModels;

namespace Taskmanager2.App.Views;

/// <summary>Benutzer page: the read-only session table. Code-behind is x:Bind wiring only (plan.md MVVM rules).</summary>
public sealed partial class BenutzerPage : Page
{
    public BenutzerPage()
    {
        ViewModel = App.Services.GetRequiredService<Tm2UsersViewModel>();
        InitializeComponent();
    }

    public Tm2UsersViewModel ViewModel { get; }
}
