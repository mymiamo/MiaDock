using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace MiaDock.App.Controls;

public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(
        object value,
        Type targetType,
        object parameter,
        string language)
    {
        var visible = value is true;
        if (string.Equals(parameter as string, "Invert", StringComparison.Ordinal))
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        string language) =>
        throw new NotSupportedException();
}
