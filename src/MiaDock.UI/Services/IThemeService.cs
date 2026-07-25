using MiaDock.Core.Theming;
using MiaDock.Core.Settings;

namespace MiaDock.UI.Services;

public interface IThemeService
{
    ThemeStyle CurrentStyle { get; }

    void Apply(ThemeStyle style);

    void Apply(AppearanceSettings appearance);
}
