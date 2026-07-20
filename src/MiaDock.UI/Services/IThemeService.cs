using MiaDock.Core.Theming;

namespace MiaDock.UI.Services;

public interface IThemeService
{
    ThemeStyle CurrentStyle { get; }

    void Apply(ThemeStyle style);
}
