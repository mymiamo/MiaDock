namespace MiaDock.Core.Theming;

public static class ThemeCatalog
{
    public static IReadOnlyList<ThemeDescriptor> All { get; } =
    [
        Create(ThemeStyle.AppleLike, "Theme.AppleLike", "AppleLikeTheme.xaml", ThemeBackdropKind.None, true, true, false, false, true),
        Create(ThemeStyle.OledBlack, "Theme.OledBlack", "OledBlackTheme.xaml", ThemeBackdropKind.None, false, true, false, false, true),
        Create(ThemeStyle.Windows11Mica, "Theme.Windows11Mica", "Windows11Theme.xaml", ThemeBackdropKind.Mica, true, true, false, false, false),
        Create(ThemeStyle.Windows11MicaAlt, "Theme.Windows11MicaAlt", "Windows11Theme.xaml", ThemeBackdropKind.MicaAlt, true, true, false, false, false),
        Create(ThemeStyle.Windows11Acrylic, "Theme.Windows11Acrylic", "Windows11Theme.xaml", ThemeBackdropKind.Acrylic, true, true, false, true, false),
        Create(ThemeStyle.Windows11AcrylicThin, "Theme.Windows11AcrylicThin", "Windows11Theme.xaml", ThemeBackdropKind.AcrylicThin, true, true, false, true, false),
        Create(ThemeStyle.BlurredGlass, "Theme.BlurredGlass", "BlurredGlassTheme.xaml", ThemeBackdropKind.ColorlessAcrylic, false, true, false, true, true),
        Create(ThemeStyle.NeutralFrostedGlass, "Theme.NeutralFrostedGlass", "NeutralFrostedGlassTheme.xaml", ThemeBackdropKind.ColorlessAcrylic, false, false, false, true, true),
        Create(ThemeStyle.AdaptiveFluent, "Theme.AdaptiveFluent", "AdaptiveFluentTheme.xaml", ThemeBackdropKind.Mica, false, false, true, false, false),
        Create(ThemeStyle.TozPembe, "Theme.TozPembe", "TozPembeTheme.xaml", ThemeBackdropKind.None, true, true, false, false, false),
        Create(ThemeStyle.CustomSolidColor, "Theme.CustomSolidColor", "AppleLikeTheme.xaml", ThemeBackdropKind.None, true, true, false, false, false)
    ];

    public static ThemeDescriptor Get(ThemeStyle style) =>
        All.FirstOrDefault(item => item.Style == style) ?? All[0];

    private static ThemeDescriptor Create(
        ThemeStyle style,
        string displayNameKey,
        string resourceFileName,
        ThemeBackdropKind backdrop,
        bool supportsBackground,
        bool supportsAccent,
        bool followsSystem,
        bool transparent,
        bool dark) => new(
            style,
            displayNameKey,
            resourceFileName,
            new ThemeCapabilities(
                backdrop,
                supportsBackground,
                supportsAccent,
                followsSystem,
                transparent,
                dark));
}
