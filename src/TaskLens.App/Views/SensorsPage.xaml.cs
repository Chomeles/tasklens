using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using TaskLens.Core.ViewModels;

namespace TaskLens.App.Views;

/// <summary>Sensor panel page. Code-behind is x:Bind wiring only (plan.md MVVM rules).</summary>
public sealed partial class SensorsPage : Page
{
    public SensorsPage()
    {
        ViewModel = App.Services.GetRequiredService<SensorsViewModel>();
        InitializeComponent();
    }

    public SensorsViewModel ViewModel { get; }
}
