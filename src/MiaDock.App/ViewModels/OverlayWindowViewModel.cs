using MiaDock.UI.Presentation;
using MiaDock.UI.Services;

namespace MiaDock.App.ViewModels;

public sealed class OverlayWindowViewModel
{
    public OverlayWindowViewModel(IslandViewModel island, IThemeService themeService)
    {
        Island = island;
        ThemeName = themeService.CurrentStyle.ToString();
    }

    public IslandViewModel Island { get; }

    public string ThemeName { get; }
}
