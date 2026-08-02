namespace MiaDock.Core.Theming;

public sealed record ThemeDescriptor(
    ThemeStyle Style,
    string DisplayNameKey,
    string ResourceFileName,
    ThemeCapabilities Capabilities);
