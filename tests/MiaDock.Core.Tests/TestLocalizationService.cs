using System.Globalization;
using MiaDock.Core.Localization;
using MiaDock.Core.Settings;

namespace MiaDock.Core.Tests;

internal sealed class TestLocalizationService(
    IReadOnlyDictionary<string, (string Turkish, string English)> values) : ILocalizationService
{
    public AppLanguage CurrentLanguage { get; private set; } = AppLanguage.Turkish;

    public CultureInfo CurrentCulture =>
        new(CurrentLanguage == AppLanguage.English ? "en-US" : "tr-TR");

    public event EventHandler? LanguageChanged;

    public void SetLanguage(AppLanguage language)
    {
        if (CurrentLanguage == language)
        {
            return;
        }

        CurrentLanguage = language;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Get(string key, params object?[] arguments)
    {
        if (!values.TryGetValue(key, out var value))
        {
            return key;
        }

        var text = CurrentLanguage == AppLanguage.English
            ? value.English
            : value.Turkish;
        return arguments.Length == 0
            ? text
            : string.Format(CurrentCulture, text, arguments);
    }
}
