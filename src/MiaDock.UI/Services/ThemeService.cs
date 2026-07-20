using Microsoft.UI.Xaml;
using MiaDock.Core.Theming;

namespace MiaDock.UI.Services;

public sealed class ThemeService : IThemeService
{
    private ResourceDictionary? _styleDictionary;

    public ThemeStyle CurrentStyle { get; private set; } = ThemeStyle.AppleLike;

    public void Apply(ThemeStyle style)
    {
        var resources = Application.Current.Resources.MergedDictionaries;

        if (_styleDictionary is not null)
        {
            resources.Remove(_styleDictionary);
        }

        _styleDictionary = new ResourceDictionary
        {
            Source = new Uri($"ms-appx:///MiaDock.UI/Themes/{GetFileName(style)}")
        };

        resources.Add(_styleDictionary);
        CurrentStyle = style;
    }

    private static string GetFileName(ThemeStyle style) => style switch
    {
        ThemeStyle.AppleLike => "AppleLikeTheme.xaml",
        ThemeStyle.Windows11 => "Windows11Theme.xaml",
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, null)
    };
}
