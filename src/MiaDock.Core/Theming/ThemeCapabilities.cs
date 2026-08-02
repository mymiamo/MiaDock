namespace MiaDock.Core.Theming;

public sealed record ThemeCapabilities(
    ThemeBackdropKind Backdrop,
    bool SupportsBackgroundColor,
    bool SupportsAccentColor,
    bool FollowsSystemTheme,
    bool UsesTransparentSurface,
    bool PrefersDarkContent);
