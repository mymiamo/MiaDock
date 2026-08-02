using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using MiaDock.App.Services;

namespace MiaDock.App.Controls;

public sealed class FocusColorBrushConverter : IValueConverter
{
    public object Convert(
        object value,
        Type targetType,
        object parameter,
        string language)
    {
        var color = value as string ?? "#0EA5E9";
        return new SolidColorBrush(ColorParser.ParseRgb(color));
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        string language) =>
        throw new NotSupportedException();
}
