using CommunityToolkit.Mvvm.ComponentModel;
using MiaDock.Core.Theming;
using MiaDock.UI.Presentation;
using MiaDock.UI.Services;

namespace MiaDock.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IThemeService _themeService;

    public MainWindowViewModel(IslandViewModel island, IThemeService themeService)
    {
        Island = island;
        _themeService = themeService;
        SelectedTheme = themeService.CurrentStyle;
        Themes = Enum.GetValues<ThemeStyle>();
    }

    [ObservableProperty]
    public partial ThemeStyle SelectedTheme { get; set; }

    public IslandViewModel Island { get; }

    public IReadOnlyList<ThemeStyle> Themes { get; }

    public string SelectedThemeName => SelectedTheme.ToString();

    partial void OnSelectedThemeChanged(ThemeStyle value)
    {
        _themeService.Apply(value);
        OnPropertyChanged(nameof(SelectedThemeName));
    }
}
