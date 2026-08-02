using MiaDock.Core.Theming;
using MiaDock.Core.Settings;

namespace MiaDock.UI.Services;

public interface IThemeService
{
    ThemeStyle CurrentStyle { get; }

    ThemeDescriptor CurrentDescriptor { get; }

    IReadOnlyList<ThemeDescriptor> AvailableThemes { get; }

    event EventHandler? ThemeEnvironmentChanged;

    void Apply(ThemeStyle style);

    void Apply(AppearanceSettings appearance);
}
