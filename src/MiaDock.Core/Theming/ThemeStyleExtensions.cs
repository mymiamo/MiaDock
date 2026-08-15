namespace MiaDock.Core.Theming;

public static class ThemeStyleExtensions
{
    public static bool IsWindows11Style(this ThemeStyle style) => style is
        ThemeStyle.Windows11Mica or
        ThemeStyle.Windows11MicaAlt or
        ThemeStyle.Windows11Acrylic or
        ThemeStyle.Windows11AcrylicThin or
        ThemeStyle.AdaptiveFluent;

    public static bool UsesMicaBackdrop(this ThemeStyle style) => style is
        ThemeStyle.Windows11Mica or
        ThemeStyle.Windows11MicaAlt or
        ThemeStyle.AdaptiveFluent;

    public static bool UsesAcrylicBackdrop(this ThemeStyle style) => style is
        ThemeStyle.Windows11Acrylic or
        ThemeStyle.Windows11AcrylicThin or
        ThemeStyle.BlurredGlass or
        ThemeStyle.NeutralFrostedGlass;

    public static bool UsesSystemBackdrop(this ThemeStyle style) =>
        style.UsesMicaBackdrop() || style.UsesAcrylicBackdrop();

    public static bool UsesColorlessGlass(this ThemeStyle style) => style is
        ThemeStyle.BlurredGlass or
        ThemeStyle.NeutralFrostedGlass;

    public static bool UsesAdaptiveSolidPalette(this ThemeStyle style) => style is
        ThemeStyle.AppleLike or
        ThemeStyle.CustomSolidColor or
        ThemeStyle.OledBlack or
        ThemeStyle.TozPembe;

    public static ThemeDescriptor Descriptor(this ThemeStyle style) => ThemeCatalog.Get(style);
}
