using System.Globalization;
using MiaDock.Core.Settings;

namespace MiaDock.Core.Localization;

public interface ILocalizationService
{
    AppLanguage CurrentLanguage { get; }

    CultureInfo CurrentCulture { get; }

    event EventHandler? LanguageChanged;

    void SetLanguage(AppLanguage language);

    string Get(string key, params object?[] arguments);
}
