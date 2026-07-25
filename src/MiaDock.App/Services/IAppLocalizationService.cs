using Microsoft.UI.Xaml;
using MiaDock.Core.Settings;

namespace MiaDock.App.Services;

public interface IAppLocalizationService
{
    AppLanguage CurrentLanguage { get; }

    event EventHandler? LanguageChanged;

    void SetLanguage(AppLanguage language);

    string Text(string turkish, string english);

    void Apply(DependencyObject root);
}
