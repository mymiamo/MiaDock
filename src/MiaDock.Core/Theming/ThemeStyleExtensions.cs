namespace MiaDock.Core.Theming;

public static class ThemeStyleExtensions
{
    public static bool IsWindows11Style(this ThemeStyle style) => style is
        ThemeStyle.Windows11Mica or
        ThemeStyle.Windows11MicaAlt or
        ThemeStyle.Windows11Acrylic or
        ThemeStyle.Windows11AcrylicThin;

    public static bool UsesMicaBackdrop(this ThemeStyle style) => style is
        ThemeStyle.Windows11Mica or
        ThemeStyle.Windows11MicaAlt;

    public static bool UsesAcrylicBackdrop(this ThemeStyle style) => style is
        ThemeStyle.Windows11Acrylic or
        ThemeStyle.Windows11AcrylicThin or
        ThemeStyle.BlurredGlass;

    public static bool UsesSystemBackdrop(this ThemeStyle style) =>
        style.UsesMicaBackdrop() || style.UsesAcrylicBackdrop();
}
