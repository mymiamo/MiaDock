using System.Reflection;
using System.Xml.Linq;

namespace MiaDock.App.Localization;

/// <summary>
/// The .resw string tables shipped with the app. Cultures are discovered from
/// the embedded resource names, so adding a language means adding a folder
/// under Strings plus the matching AppLanguage entry - nothing here changes.
/// </summary>
internal sealed class LocalizedStringTables
{
    /// <summary>
    /// The culture the XAML markup is authored in. Its XamlText values are the
    /// lookup keys every other culture translates.
    /// </summary>
    public const string SourceCulture = "tr-TR";

    private const string ResourcePrefix = "MiaDock.App.Strings/";
    private const string KeyedTableName = "Resources.resw";
    private const string XamlTableName = "XamlText.resw";

    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _keyed;
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _xamlText;
    private readonly IReadOnlyDictionary<string, string> _xamlSources;

    private LocalizedStringTables(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> keyed,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> xamlText,
        IReadOnlyDictionary<string, string> xamlSources)
    {
        _keyed = keyed;
        _xamlText = xamlText;
        _xamlSources = xamlSources;
    }

    public static LocalizedStringTables Empty { get; } = new(
        new Dictionary<string, IReadOnlyDictionary<string, string>>(),
        new Dictionary<string, IReadOnlyDictionary<string, string>>(),
        new Dictionary<string, string>());

    public IReadOnlyCollection<string> Cultures => (IReadOnlyCollection<string>)_keyed.Keys;

    public static LocalizedStringTables Load() => Load(typeof(LocalizedStringTables).Assembly);

    public static LocalizedStringTables Load(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var keyed = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var xamlByCulture = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (culture, table, resourceName) in EnumerateTables(assembly))
        {
            var entries = ReadTable(assembly, resourceName);
            if (string.Equals(table, KeyedTableName, StringComparison.OrdinalIgnoreCase))
            {
                keyed[culture] = entries;
            }
            else if (string.Equals(table, XamlTableName, StringComparison.OrdinalIgnoreCase))
            {
                xamlByCulture[culture] = entries;
            }
        }

        return new LocalizedStringTables(keyed, BuildXamlText(xamlByCulture, out var sources), sources);
    }

    public string? GetKeyed(string culture, string key) =>
        _keyed.TryGetValue(culture, out var entries) && entries.TryGetValue(key, out var value)
            ? value
            : null;

    /// <summary>
    /// Translates a string authored in <see cref="SourceCulture"/> XAML markup.
    /// </summary>
    public string? TranslateXamlText(string culture, string sourceText) =>
        _xamlText.TryGetValue(culture, out var entries) && entries.TryGetValue(sourceText, out var value)
            ? value
            : null;

    /// <summary>
    /// Maps text currently on screen - in any shipped language - back to the
    /// string the XAML was authored with.
    /// </summary>
    public string? FindXamlSource(string displayedText) =>
        _xamlSources.TryGetValue(displayedText, out var source) ? source : null;

    private static IEnumerable<(string Culture, string Table, string ResourceName)> EnumerateTables(Assembly assembly)
    {
        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var segments = resourceName[ResourcePrefix.Length..]
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 2)
            {
                yield return (segments[0], segments[1], resourceName);
            }
        }
    }

    private static IReadOnlyDictionary<string, string> ReadTable(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The embedded resource '{resourceName}' is missing.");

        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var element in XDocument.Load(stream).Root?.Elements("data") ?? [])
        {
            var name = element.Attribute("name")?.Value;
            var value = element.Element("value")?.Value;
            if (!string.IsNullOrEmpty(name) && value is not null)
            {
                entries[name] = value;
            }
        }

        return entries;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> BuildXamlText(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> xamlByCulture,
        out IReadOnlyDictionary<string, string> sources)
    {
        var sourceIndex = new Dictionary<string, string>(StringComparer.Ordinal);
        var translations = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (!xamlByCulture.TryGetValue(SourceCulture, out var sourceTable))
        {
            sources = sourceIndex;
            return translations;
        }

        // The source culture goes first so a translation that happens to equal
        // an authored string never shadows that string's own identity mapping.
        var ordered = xamlByCulture.OrderByDescending(pair =>
            string.Equals(pair.Key, SourceCulture, StringComparison.OrdinalIgnoreCase));
        foreach (var (culture, table) in ordered)
        {
            var bySourceText = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (name, sourceText) in sourceTable)
            {
                if (!table.TryGetValue(name, out var translated))
                {
                    continue;
                }

                bySourceText[sourceText] = translated;
                // A translation can coincide across entries; the first one wins
                // so the mapping back to the authored string stays stable.
                sourceIndex.TryAdd(translated, sourceText);
            }

            translations[culture] = bySourceText;
        }

        sources = sourceIndex;
        return translations;
    }
}
