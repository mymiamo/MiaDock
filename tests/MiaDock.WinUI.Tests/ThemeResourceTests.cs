using System.Xml.Linq;

namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class ThemeResourceTests
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    [TestMethod]
    public void PrimitiveTokens_ContainRequiredIslandDimensions()
    {
        var keys = ReadResourceKeys("PrimitiveTokens.xaml");

        CollectionAssert.IsSubsetOf(
            new[]
            {
                "IslandCollapsedWidth",
                "IslandCollapsedHeight",
                "IslandExpandedWidth",
                "IslandExpandedHeight",
                "IslandCornerRadius"
            },
            keys.ToArray());
    }

    [TestMethod]
    public void SemanticTokens_DefineDefaultLightAndHighContrastThemes()
    {
        var document = LoadTheme("SemanticTokens.xaml");
        var themeKeys = document.Descendants()
            .Attributes(XamlNamespace + "Key")
            .Select(attribute => attribute.Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.IsTrue(themeKeys.Contains("Default"));
        Assert.IsTrue(themeKeys.Contains("Light"));
        Assert.IsTrue(themeKeys.Contains("HighContrast"));
    }

    [DataRow("AppleLikeTheme.xaml")]
    [DataRow("Windows11Theme.xaml")]
    [TestMethod]
    public void StyleTheme_ProvidesRequiredSemanticOverrides(string fileName)
    {
        var keys = ReadResourceKeys(fileName);

        Assert.IsTrue(keys.Contains("IslandStyleSurfaceBrush"));
        Assert.IsTrue(keys.Contains("IslandStyleControlBrush"));
        Assert.IsTrue(keys.Contains("IslandStyleAccentBrush"));
    }

    private static HashSet<string> ReadResourceKeys(string fileName) => LoadTheme(fileName)
        .Descendants()
        .Attributes(XamlNamespace + "Key")
        .Select(attribute => attribute.Value)
        .ToHashSet(StringComparer.Ordinal);

    private static XDocument LoadTheme(string fileName) => XDocument.Load(
        Path.Combine(AppContext.BaseDirectory, "Themes", fileName));
}
