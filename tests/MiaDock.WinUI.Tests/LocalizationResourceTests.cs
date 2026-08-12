using System.Text.RegularExpressions;
using System.Xml.Linq;
using MiaDock.App.Localization;

namespace MiaDock.WinUI.Tests;

/// <summary>
/// Shared access to the shipped .resw tables. The loader under test is the very
/// one the app runs, and the tables are embedded here under the same logical
/// names, so a missing or malformed file fails the build rather than the app.
/// </summary>
internal static class LocalizationTables
{
    public static LocalizedStringTables Tables { get; } =
        LocalizedStringTables.Load(typeof(LocalizationTables).Assembly);

    public static string StringsDirectory { get; } =
        Path.Combine(AppContext.BaseDirectory, "Strings");

    public static IReadOnlyList<string> Cultures { get; } =
        Directory.GetDirectories(StringsDirectory)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    public static IReadOnlyDictionary<string, string> Read(string culture, string table)
    {
        var document = XDocument.Load(Path.Combine(StringsDirectory, culture, table));
        return document.Root!
            .Elements("data")
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")!.Value,
                StringComparer.Ordinal);
    }

    /// <summary>
    /// Returns the markup literals in a copied XAML folder that no shipped
    /// language can translate.
    /// </summary>
    public static string[] FindUntranslatedMarkupText(
        string folder,
        string pattern,
        params string[] ignored)
    {
        var skip = new HashSet<string>(ignored, StringComparer.Ordinal);
        return Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, folder), "*.xaml")
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), pattern)
                .Select(match => match.Groups[1].Value))
            .Where(value => !value.StartsWith("{Binding", StringComparison.Ordinal))
            .Where(value => !skip.Contains(value))
            .Distinct(StringComparer.Ordinal)
            .Where(value => Tables.FindXamlSource(value) is null)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }
}

[TestClass]
public sealed class LocalizationResourceTests
{
    private const string SourceCulture = "tr-TR";

    [TestMethod]
    public void EveryCulture_ShipsBothStringTables()
    {
        Assert.Contains(SourceCulture, LocalizationTables.Cultures);
        Assert.IsGreaterThan(1, LocalizationTables.Cultures.Count);

        foreach (var culture in LocalizationTables.Cultures)
        {
            foreach (var table in new[] { "Resources.resw", "XamlText.resw" })
            {
                Assert.IsTrue(
                    File.Exists(Path.Combine(LocalizationTables.StringsDirectory, culture, table)),
                    $"{culture} is missing {table}.");
            }
        }
    }

    [TestMethod]
    public void EveryCulture_TranslatesTheSameKeysWithoutBlanks()
    {
        foreach (var table in new[] { "Resources.resw", "XamlText.resw" })
        {
            var source = LocalizationTables.Read(SourceCulture, table);
            Assert.IsGreaterThan(0, source.Count);

            foreach (var culture in LocalizationTables.Cultures)
            {
                var translated = LocalizationTables.Read(culture, table);
                var missing = source.Keys.Except(translated.Keys, StringComparer.Ordinal).ToArray();
                var extra = translated.Keys.Except(source.Keys, StringComparer.Ordinal).ToArray();
                var blank = translated.Where(pair => string.IsNullOrWhiteSpace(pair.Value))
                    .Select(pair => pair.Key)
                    .ToArray();

                Assert.HasCount(0, missing, $"{culture}/{table} is missing: {string.Join(", ", missing)}");
                Assert.HasCount(0, extra, $"{culture}/{table} has unknown keys: {string.Join(", ", extra)}");
                Assert.HasCount(0, blank, $"{culture}/{table} has blank values: {string.Join(", ", blank)}");
            }
        }
    }

    [TestMethod]
    public void EveryTranslation_KeepsTheFormatPlaceholdersOfTheSourceText()
    {
        var source = LocalizationTables.Read(SourceCulture, "Resources.resw");
        var mismatched = new List<string>();

        foreach (var culture in LocalizationTables.Cultures.Where(name => name != SourceCulture))
        {
            var translated = LocalizationTables.Read(culture, "Resources.resw");
            foreach (var (key, value) in source)
            {
                if (!Placeholders(value).SetEquals(Placeholders(translated[key])))
                {
                    mismatched.Add($"{culture}/{key}");
                }
            }
        }

        Assert.HasCount(
            0,
            mismatched,
            $"Placeholders differ from the source text: {string.Join(", ", mismatched)}");
    }

    private static HashSet<string> Placeholders(string value) =>
        Regex.Matches(value, "\\{\\d+(?::[^}]*)?\\}")
            .Select(match => match.Value)
            .ToHashSet(StringComparer.Ordinal);

    [TestMethod]
    public void Loader_ResolvesKeysAndRoundTripsMarkupTextThroughEveryCulture()
    {
        var tables = LocalizationTables.Tables;
        Assert.AreEqual("Saat {0}, {1}", tables.GetKeyed(SourceCulture, "Dock.Clock"));
        Assert.AreEqual("Time {0}, {1}", tables.GetKeyed("en-US", "Dock.Clock"));
        Assert.AreEqual("Saat {0}, {1}", tables.GetKeyed("az-Latn-AZ", "Dock.Clock"));
        Assert.IsFalse(string.IsNullOrWhiteSpace(tables.GetKeyed("es-ES", "Dock.Clock")));
        Assert.IsFalse(string.IsNullOrWhiteSpace(tables.GetKeyed("es-MX", "Dock.Clock")));
        Assert.IsFalse(string.IsNullOrWhiteSpace(tables.GetKeyed("pt-BR", "Dock.Clock")));
        Assert.Contains("az-Latn-AZ", LocalizationTables.Cultures);
        Assert.Contains("es-ES", LocalizationTables.Cultures);
        Assert.Contains("es-MX", LocalizationTables.Cultures);
        Assert.Contains("pt-BR", LocalizationTables.Cultures);
        Assert.IsNull(tables.GetKeyed(SourceCulture, "Key.That.Does.Not.Exist"));

        foreach (var sourceText in LocalizationTables.Read(SourceCulture, "XamlText.resw").Values)
        {
            foreach (var culture in LocalizationTables.Cultures)
            {
                var translated = tables.TranslateXamlText(culture, sourceText);
                Assert.IsNotNull(translated, $"{culture} cannot translate '{sourceText}'.");

                // Reverse lookup is many-to-one when two source strings share a
                // translation. What matters for live language switching is that
                // the recovered source still produces the same display string.
                var recovered = tables.FindXamlSource(translated);
                Assert.IsNotNull(recovered, $"'{translated}' ({culture}) has no source mapping.");
                Assert.AreEqual(
                    translated,
                    tables.TranslateXamlText(culture, recovered),
                    $"'{translated}' ({culture}) is not stable through FindXamlSource.");
            }
        }
    }

    [TestMethod]
    public void LocalizationService_ReadsTheTablesInsteadOfHardCodedDictionaries()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Localization",
            "AppLocalizationService.cs"));

        StringAssert.Contains(source, "LocalizedStringTables");
        Assert.DoesNotContain(
            "IReadOnlyDictionary<string, (string Turkish, string English)>",
            source,
            StringComparison.Ordinal);
        Assert.IsFalse(
            Regex.IsMatch(source, "(?m)^\\s*\\[\"[^\"]+\"\\]\\s*=\\s*[\"(]"),
            "String tables belong in Strings\\<culture>\\*.resw, not in the service.");
    }
}
