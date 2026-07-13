using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using TaskLens.Core.ViewModels;

namespace Taskmanager2.App.Views;

/// <summary>Dienste page: the read-only service table. Code-behind is x:Bind wiring only (plan.md MVVM rules).</summary>
public sealed partial class DienstePage : Page
{
    public DienstePage()
    {
        ViewModel = App.Services.GetRequiredService<Tm2ServicesViewModel>();
        InitializeComponent();
    }

    public Tm2ServicesViewModel ViewModel { get; }
}
