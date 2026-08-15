using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MiaDock.App.Localization;
using MiaDock.Core.Localization;
using MiaDock.Core.Settings;

namespace MiaDock.App.Services;

public sealed class AppLocalizationService : IAppLocalizationService
{
    private static readonly Lazy<LocalizedStringTables> Tables = new(
        LoadTables,
        LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly ConditionalWeakTable<DependencyObject, Dictionary<string, string>> _originals = new();

    public AppLanguage CurrentLanguage { get; private set; } = AppLanguage.Turkish;

    public CultureInfo CurrentCulture { get; private set; } = new(LocalizedStringTables.SourceCulture);

    public event EventHandler? LanguageChanged;

    public static string CultureNameFor(AppLanguage language) => language switch
    {
        AppLanguage.English => "en-US",
        AppLanguage.Azerbaijani => "az-Latn-AZ",
        AppLanguage.SpanishSpain => "es-ES",
        AppLanguage.SpanishMexico => "es-MX",
        AppLanguage.PortugueseBrazil => "pt-BR",
        _ => LocalizedStringTables.SourceCulture
    };

    public void SetLanguage(AppLanguage language)
    {
        if (!Enum.IsDefined(language))
        {
            language = AppLanguage.Turkish;
        }

        var changed = CurrentLanguage != language;
        CurrentLanguage = language;
        var culture = new CultureInfo(CultureNameFor(language));
        CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        if (changed)
        {
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Translates a pair of literals authored in C#. The Turkish literal is the
    /// lookup key in XamlText.resw, so languages beyond these two arguments are
    /// served from the tables; the English argument stays as the fallback for an
    /// entry that has not been added to the tables yet.
    /// </summary>
    public string Text(string turkish, string english) =>
        Tables.Value.TranslateXamlText(CultureNameFor(CurrentLanguage), turkish)
            ?? (CurrentLanguage == AppLanguage.English ? english : turkish);

    public string Get(string key, params object?[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var value = Tables.Value.GetKeyed(CultureNameFor(CurrentLanguage), key)
            ?? Tables.Value.GetKeyed(LocalizedStringTables.SourceCulture, key);
        if (value is null)
        {
            return key;
        }

        return arguments.Length == 0
            ? value
            : string.Format(CurrentCulture, value, arguments);
    }

    public void Apply(DependencyObject root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var pending = new Stack<DependencyObject>();
        var visited = new HashSet<DependencyObject>(DependencyObjectReferenceComparer.Instance);
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            LocalizeElement(current);
            for (var index = VisualTreeHelper.GetChildrenCount(current) - 1; index >= 0; index--)
            {
                pending.Push(VisualTreeHelper.GetChild(current, index));
            }
        }
    }

    private static LocalizedStringTables LoadTables()
    {
        try
        {
            return LocalizedStringTables.Load();
        }
        catch (Exception)
        {
            // Losing the string tables must not take the app down; untranslated
            // keys are recoverable, a startup crash is not. LocalizationTests
            // keep the packaged tables honest at build time.
            return LocalizedStringTables.Empty;
        }
    }

    private void LocalizeElement(DependencyObject element)
    {
        if (element is TextBlock textBlock &&
            textBlock.ReadLocalValue(TextBlock.TextProperty) is string)
            Localize(element, "Text", textBlock.Text, value => textBlock.Text = value);
        if (element is ContentControl contentControl &&
            contentControl.ReadLocalValue(ContentControl.ContentProperty) is string content)
            Localize(element, "Content", content, value => contentControl.Content = value);
        if (element is ToggleSwitch toggleSwitch)
        {
            if (toggleSwitch.ReadLocalValue(ToggleSwitch.HeaderProperty) is string toggleHeader)
            {
                Localize(element, "ToggleHeader", toggleHeader, value => toggleSwitch.Header = value);
            }
            if (toggleSwitch.ReadLocalValue(ToggleSwitch.OnContentProperty) is string onContent)
            {
                Localize(element, "ToggleOnContent", onContent, value => toggleSwitch.OnContent = value);
            }
            if (toggleSwitch.ReadLocalValue(ToggleSwitch.OffContentProperty) is string offContent)
            {
                Localize(element, "ToggleOffContent", offContent, value => toggleSwitch.OffContent = value);
            }
        }
        if (element is NumberBox numberBox &&
            numberBox.ReadLocalValue(NumberBox.HeaderProperty) is string numberHeader)
            Localize(element, "NumberHeader", numberHeader, value => numberBox.Header = value);
        if (element is Slider slider &&
            slider.ReadLocalValue(Slider.HeaderProperty) is string sliderHeader)
            Localize(element, "SliderHeader", sliderHeader, value => slider.Header = value);
        if (element is InfoBar infoBar)
        {
            Localize(element, "InfoTitle", infoBar.Title, value => infoBar.Title = value);
            Localize(element, "InfoMessage", infoBar.Message, value => infoBar.Message = value);
        }
        if (element is TextBox textBox)
            Localize(element, "TextPlaceholder", textBox.PlaceholderText, value => textBox.PlaceholderText = value);
        if (element is AutoSuggestBox suggestBox)
            Localize(element, "SuggestPlaceholder", suggestBox.PlaceholderText, value => suggestBox.PlaceholderText = value);
        if (element is ComboBox comboBox)
            Localize(element, "ComboPlaceholder", comboBox.PlaceholderText, value => comboBox.PlaceholderText = value);
        if (element is TabViewItem tabViewItem &&
            tabViewItem.ReadLocalValue(TabViewItem.HeaderProperty) is string tabHeader)
            Localize(element, "TabHeader", tabHeader, value => tabViewItem.Header = value);

        if (element.ReadLocalValue(ToolTipService.ToolTipProperty) is string tooltip)
            Localize(element, "ToolTip", tooltip, value => ToolTipService.SetToolTip(element, value));
        if (element.ReadLocalValue(AutomationProperties.NameProperty) is string automationName &&
            !string.IsNullOrWhiteSpace(automationName))
            Localize(element, "AutomationName", automationName, value => AutomationProperties.SetName(element, value));
        if (element.ReadLocalValue(AutomationProperties.HelpTextProperty) is string automationHelpText &&
            !string.IsNullOrWhiteSpace(automationHelpText))
            Localize(element, "AutomationHelpText", automationHelpText, value => AutomationProperties.SetHelpText(element, value));
    }

    private void Localize(DependencyObject owner, string property, string? current, Action<string> setter)
    {
        if (string.IsNullOrWhiteSpace(current))
        {
            return;
        }

        var originals = _originals.GetOrCreateValue(owner);
        if (!originals.TryGetValue(property, out var original))
        {
            original = Tables.Value.FindXamlSource(current);
            if (string.IsNullOrEmpty(original))
            {
                return;
            }
            originals[property] = original;
        }

        setter(Tables.Value.TranslateXamlText(CultureNameFor(CurrentLanguage), original) ?? original);
    }

    private sealed class DependencyObjectReferenceComparer : IEqualityComparer<DependencyObject>
    {
        public static DependencyObjectReferenceComparer Instance { get; } = new();

        public bool Equals(DependencyObject? left, DependencyObject? right) => ReferenceEquals(left, right);

        public int GetHashCode(DependencyObject value) => RuntimeHelpers.GetHashCode(value);
    }
}
