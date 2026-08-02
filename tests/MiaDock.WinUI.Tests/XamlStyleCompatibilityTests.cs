using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed partial class XamlStyleCompatibilityTests
{
    private static readonly XNamespace XamlNamespace =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [TestMethod]
    public void StaticStyles_AreAppliedOnlyToTheirDeclaredControlType()
    {
        var root = FindRepositoryRoot();
        var sourceFiles = Directory.EnumerateFiles(
                Path.Combine(root, "src"),
                "*.xaml",
                SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(path))
            .ToArray();
        var targets = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in sourceFiles)
        {
            var document = XDocument.Load(file);
            foreach (var style in document.Descendants()
                         .Where(element => element.Name.LocalName == "Style"))
            {
                var key = style.Attribute(XamlNamespace + "Key")?.Value;
                var target = NormalizeType(style.Attribute("TargetType")?.Value);
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(target))
                {
                    targets[key] = target;
                }
            }
        }

        var mismatches = new List<string>();
        foreach (var file in sourceFiles.Where(path =>
                     path.Contains($"{Path.DirectorySeparatorChar}MiaDock.App{Path.DirectorySeparatorChar}")))
        {
            var document = XDocument.Load(file);
            foreach (var element in document.Descendants())
            {
                var styleValue = element.Attribute("Style")?.Value;
                var match = StaticResourcePattern().Match(styleValue ?? string.Empty);
                if (!match.Success || !targets.TryGetValue(match.Groups[1].Value, out var target))
                {
                    continue;
                }

                var actual = NormalizeType(element.Name.LocalName);
                if (!string.Equals(actual, target, StringComparison.Ordinal))
                {
                    mismatches.Add(
                        $"{Path.GetRelativePath(root, file)}: {actual} uses {match.Groups[1].Value} ({target})");
                }
            }
        }

        Assert.IsEmpty(mismatches, string.Join(Environment.NewLine, mismatches));
    }

    private static bool IsGeneratedPath(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}");

    private static string NormalizeType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var separator = value.LastIndexOf(':');
        return separator >= 0 ? value[(separator + 1)..] : value;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "MiaDock.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    [GeneratedRegex("^\\{StaticResource\\s+([^}]+)\\}$", RegexOptions.CultureInvariant)]
    private static partial Regex StaticResourcePattern();
}
