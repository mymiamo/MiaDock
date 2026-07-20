using System.Xml.Linq;

namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class IslandShellTests
{
    [TestMethod]
    public void IslandShell_ProvidesNamedLayoutAndContentHosts()
    {
        var document = LoadControl("IslandShell.xaml");
        var names = document.Descendants()
            .Attributes(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))
            .Select(attribute => attribute.Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.IsTrue(names.Contains("LayoutRoot"));
        Assert.IsTrue(names.Contains("Surface"));
        Assert.IsTrue(names.Contains("ContentHost"));
    }

    [TestMethod]
    public void EveryControlXaml_IsWellFormed()
    {
        var controlDirectory = Path.Combine(AppContext.BaseDirectory, "Controls");
        var files = Directory.GetFiles(controlDirectory, "*.xaml");

        Assert.IsGreaterThanOrEqualTo(10, files.Length);
        foreach (var file in files)
        {
            _ = XDocument.Load(file);
        }
    }

    private static XDocument LoadControl(string fileName) => XDocument.Load(
        Path.Combine(AppContext.BaseDirectory, "Controls", fileName));
}
