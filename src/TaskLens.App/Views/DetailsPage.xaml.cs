using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using TaskLens.Core.ViewModels;

namespace TaskLens.App.Views;

/// <summary>Per-process + system history page. Code-behind is x:Bind wiring only (plan.md MVVM rules).</summary>
public sealed partial class DetailsPage : Page
{
    public DetailsPage()
    {
        ViewModel = App.Services.GetRequiredService<DetailsViewModel>();
        InitializeComponent();
    }

    public DetailsViewModel ViewModel { get; }
}
