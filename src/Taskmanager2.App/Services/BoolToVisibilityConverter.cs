using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Taskmanager2.App.Services;

/// <summary>true → Visible; pass ConverterParameter="invert" to flip. For the group-collapse
/// chevron bindings, where x:Bind's built-in bool→Visibility cast is out of reach (ElementName
/// bindings inside a DataTemplate must use classic Binding).</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var visible = value is true;
        if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase))
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
