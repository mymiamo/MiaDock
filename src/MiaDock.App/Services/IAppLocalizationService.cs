using Microsoft.UI.Xaml;
using MiaDock.Core.Localization;
using MiaDock.Core.Settings;

namespace MiaDock.App.Services;

public interface IAppLocalizationService : ILocalizationService
{
    string Text(string turkish, string english);

    void Apply(DependencyObject root);
}
